using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Abstractions.Storage;
using CloudDocumentPipeline.Application.Exceptions;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace CloudDocumentPipeline.Application.Jobs;

// Coordinates job use cases without depending on concrete storage, database, or broker providers.
public sealed class JobService
{
    public const string DocumentToPdfJobType = "DocumentToPdf";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ICorrelationContextAccessor _correlationContextAccessor;
    private readonly IFileStorage _fileStorage;
    private readonly IJobRepository _jobRepository;
    private readonly IJobMetrics _jobMetrics;
    private readonly ILogger<JobService> _logger;
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    public JobService(
        ICorrelationContextAccessor correlationContextAccessor,
        IFileStorage fileStorage,
        IJobRepository jobRepository,
        IJobMetrics jobMetrics,
        ILogger<JobService> logger,
        IOutboxMessageRepository outboxMessageRepository)
    {
        _correlationContextAccessor = correlationContextAccessor;
        _fileStorage = fileStorage;
        _jobRepository = jobRepository;
        _jobMetrics = jobMetrics;
        _logger = logger;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task<Guid> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = CloudDocumentPipelineTracing.ActivitySource.StartActivity("job.create", ActivityKind.Internal);
        activity?.SetTag("job.type", request.Type);
        activity?.SetTag("job.name", request.Name);
        activity?.SetTag("correlation.id", _correlationContextAccessor.GetCorrelationId());

        var job = new Job(request.Name, request.Type, request.PayloadJson);

        // Persist the job and its integration event in the same unit of work.
        // The outbox publisher sends the event after the database commit succeeds.
        await _jobRepository.AddAsync(job, cancellationToken);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);
        _jobMetrics.JobCreated(job.Type);
        activity?.SetTag("job.id", job.Id);

        _logger.LogInformation(
            "Job created. JobId={JobId}, JobType={JobType}, JobName={JobName}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            job.Name,
            _correlationContextAccessor.GetCorrelationId());

        return job.Id;
    }

    public async Task<Guid> CreateDocumentToPdfAsync(
        string? name,
        string originalFileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        // Store file bytes outside the database and keep only logical storage keys in the job payload.
        var inputStorageKey = await _fileStorage.SaveAsync(
            "uploads",
            originalFileName,
            fileBytes,
            cancellationToken);

        var payload = new DocumentToPdfJobPayload
        {
            OriginalFileName = originalFileName,
            ContentType = contentType,
            InputStorageKey = inputStorageKey
        };

        var request = new CreateJobRequest
        {
            Name = string.IsNullOrWhiteSpace(name) ? originalFileName : name,
            Type = DocumentToPdfJobType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializerOptions)
        };

        return await CreateAsync(request, cancellationToken);
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<List<JobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task<JobResultFileDto?> GetResultFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Result files are downloadable only after the worker marks the job as succeeded.
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null ||
            job.Type != DocumentToPdfJobType ||
            job.Status != JobStatus.Succeeded ||
            string.IsNullOrWhiteSpace(job.ResultJson))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<DocumentToPdfJobResult>(job.ResultJson, JsonSerializerOptions);
        if (result is null || string.IsNullOrWhiteSpace(result.OutputStorageKey))
        {
            return null;
        }

        var content = await _fileStorage.ReadAsync(result.OutputStorageKey, cancellationToken);
        if (content is null)
        {
            return null;
        }

        return new JobResultFileDto
        {
            FileName = result.OutputFileName,
            ContentType = "application/pdf",
            Content = content
        };
    }

    public async Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.MarkProcessing);
        await _jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(Guid jobId, string resultJson, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkSucceeded(resultJson));
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job succeeded. JobId={JobId}, JobType={JobType}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            _correlationContextAccessor.GetCorrelationId());
    }

    public async Task MarkFailedAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, () => job.MarkFailed(errorMessage));
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Job failed. JobId={JobId}, JobType={JobType}, CorrelationId={CorrelationId}, ErrorMessage={ErrorMessage}",
            job.Id,
            job.Type,
            _correlationContextAccessor.GetCorrelationId(),
            errorMessage);
    }

    public async Task RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Retrying moves the domain state back to Pending and emits a new outbox message.
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new JobNotFoundException(jobId);

        TryChangeState(jobId, job.Retry);
        await AddOutboxMessageAsync(job, _correlationContextAccessor.GetCorrelationId(), cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);
        _jobMetrics.JobRetried(job.Type);

        _logger.LogInformation(
            "Job retried. JobId={JobId}, JobType={JobType}, RetryCount={RetryCount}, CorrelationId={CorrelationId}",
            job.Id,
            job.Type,
            job.RetryCount,
            _correlationContextAccessor.GetCorrelationId());
    }

    private async Task AddOutboxMessageAsync(Job job, string correlationId, CancellationToken cancellationToken)
    {
        // The idempotency key stays stable for a job so consumers can safely deduplicate replays.
        var integrationMessage = new JobCreatedIntegrationMessage
        {
            MessageId = Guid.NewGuid(),
            JobId = job.Id,
            CorrelationId = correlationId,
            IdempotencyKey = $"job:{job.Id}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(integrationMessage, JsonSerializerOptions);
        var outboxMessage = new OutboxMessage(nameof(JobCreatedIntegrationMessage), payload);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
    }

    private static void TryChangeState(Guid jobId, Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidJobStateException(jobId, exception.Message, exception);
        }
    }

    private static JobDto Map(Job job)
    {
        return new JobDto
        {
            Id = job.Id,
            Name = job.Name,
            Type = job.Type,
            Status = job.Status.ToString(),
            RetryCount = job.RetryCount,
            CreatedAtUtc = job.CreatedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            ErrorMessage = job.ErrorMessage
        };
    }
}
