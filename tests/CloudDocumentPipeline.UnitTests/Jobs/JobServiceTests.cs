using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Abstractions.Storage;
using CloudDocumentPipeline.Application.Exceptions;
using CloudDocumentPipeline.Application.Jobs;
using CloudDocumentPipeline.Application.Messaging;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CloudDocumentPipeline.UnitTests.Jobs;

public sealed class JobServiceTests
{
    [Fact]
    public async Task CreateDocumentToPdfAsync_SavesInputFileAndStoresStorageKeyInPayload()
    {
        var repository = new InMemoryJobRepository();
        var outboxRepository = new RecordingOutboxRepository();
        var fileStorage = new StubFileStorage();
        var service = new JobService(
            new StubCorrelationContextAccessor(),
            fileStorage,
            repository,
            new StubJobMetrics(),
            NullLogger<JobService>.Instance,
            outboxRepository);

        var fileBytes = new byte[] { 1, 2, 3, 4 };

        var jobId = await service.CreateDocumentToPdfAsync(
            "demo-file",
            "sample.txt",
            "text/plain",
            fileBytes);

        var job = await repository.GetByIdAsync(jobId);

        Assert.NotNull(job);
        Assert.Single(fileStorage.SavedFiles);
        Assert.Equal("uploads/sample.txt", fileStorage.SavedFiles.Single().StorageKey);

        var payload = JsonSerializer.Deserialize<DocumentToPdfJobPayload>(job!.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal("sample.txt", payload!.OriginalFileName);
        Assert.Equal("text/plain", payload.ContentType);
        Assert.Equal("uploads/sample.txt", payload.InputStorageKey);
        Assert.Single(outboxRepository.Messages);
    }

    [Fact]
    public async Task GetResultFileAsync_WhenSucceededDocumentJobExists_ReturnsStoredPdf()
    {
        var repository = new InMemoryJobRepository();
        var fileStorage = new StubFileStorage();
        var service = new JobService(
            new StubCorrelationContextAccessor(),
            fileStorage,
            repository,
            new StubJobMetrics(),
            NullLogger<JobService>.Instance,
            new InMemoryOutboxRepository());

        var resultBytes = new byte[] { 9, 8, 7, 6 };
        await fileStorage.SeedAsync("results/output.pdf", resultBytes);

        var job = new Job(
            "demo",
            JobService.DocumentToPdfJobType,
            JsonSerializer.Serialize(new DocumentToPdfJobPayload
            {
                OriginalFileName = "sample.txt",
                ContentType = "text/plain",
                InputStorageKey = "uploads/sample.txt"
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        job.MarkProcessing();
        job.MarkSucceeded(JsonSerializer.Serialize(new DocumentToPdfJobResult
        {
            OutputFileName = "output.pdf",
            OutputStorageKey = "results/output.pdf",
            GeneratedAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await repository.AddAsync(job);

        var file = await service.GetResultFileAsync(job.Id);

        Assert.NotNull(file);
        Assert.Equal("output.pdf", file!.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(resultBytes, file.Content);
    }

    [Fact]
    public async Task RetryAsync_WhenJobMissing_ThrowsJobNotFoundException()
    {
        var service = new JobService(
            new StubCorrelationContextAccessor(),
            new StubFileStorage(),
            new InMemoryJobRepository(),
            new StubJobMetrics(),
            NullLogger<JobService>.Instance,
            new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<JobNotFoundException>(() => service.RetryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RetryAsync_WhenJobIsNotFailed_ThrowsInvalidJobStateException()
    {
        var repository = new InMemoryJobRepository();
        var job = new Job("demo", "pdf", "{}");
        await repository.AddAsync(job);

        var service = new JobService(
            new StubCorrelationContextAccessor(),
            new StubFileStorage(),
            repository,
            new StubJobMetrics(),
            NullLogger<JobService>.Instance,
            new InMemoryOutboxRepository());

        await Assert.ThrowsAsync<InvalidJobStateException>(() => service.RetryAsync(job.Id));
    }

    [Fact]
    public async Task CreateAsync_UsesStableJobBasedIdempotencyKey()
    {
        var repository = new InMemoryJobRepository();
        var outboxRepository = new RecordingOutboxRepository();
        var service = new JobService(
            new StubCorrelationContextAccessor(),
            new StubFileStorage(),
            repository,
            new StubJobMetrics(),
            NullLogger<JobService>.Instance,
            outboxRepository);

        var jobId = await service.CreateAsync(new CreateJobRequest
        {
            Name = "demo",
            Type = "pdf",
            PayloadJson = "{}"
        });

        var processingPayload = JsonSerializer.Deserialize<JobCreatedIntegrationMessage>(
            outboxRepository.Messages.Single(x => x.Type == nameof(JobCreatedIntegrationMessage)).PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Single(outboxRepository.Messages);
        Assert.Equal(jobId, processingPayload.JobId);
        Assert.Equal($"job:{jobId}", processingPayload.IdempotencyKey);
        Assert.Equal("corr-123", processingPayload.CorrelationId);
    }

    private sealed class InMemoryJobRepository : IJobRepository
    {
        private readonly List<Job> _jobs = [];

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            _jobs.Add(job);
            return Task.CompletedTask;
        }

        public Task<List<Job>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_jobs.OrderByDescending(x => x.CreatedAtUtc).ToList());
        }

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_jobs.FirstOrDefault(x => x.Id == id));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubCorrelationContextAccessor : ICorrelationContextAccessor
    {
        public string GetCorrelationId()
        {
            return "corr-123";
        }
    }

    private sealed class StubJobMetrics : IJobMetrics
    {
        public void JobCreated(string jobType)
        {
        }

        public void JobFailed(string jobType)
        {
        }

        public void JobRetried(string jobType)
        {
        }

        public void JobSucceeded(string jobType)
        {
        }

        public void RecordProcessingDuration(string jobType, double durationSeconds)
        {
        }
    }

    private sealed class StubFileStorage : IFileStorage
    {
        public List<SavedFile> SavedFiles { get; } = [];

        public Task<byte[]?> ReadAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SavedFiles.FirstOrDefault(x => x.StorageKey == storagePath)?.Content);
        }

        public Task<string> SaveAsync(
            string category,
            string fileName,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            var storageKey = $"{category}/{fileName}";
            SavedFiles.Add(new SavedFile(storageKey, content));
            return Task.FromResult(storageKey);
        }

        public Task SeedAsync(string storageKey, byte[] content)
        {
            SavedFiles.Add(new SavedFile(storageKey, content));
            return Task.CompletedTask;
        }
    }

    private sealed record SavedFile(string StorageKey, byte[] Content);

    private sealed class InMemoryOutboxRepository : IOutboxMessageRepository
    {
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<OutboxMessage>());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRepository : IOutboxMessageRepository
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<OutboxMessage>());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
