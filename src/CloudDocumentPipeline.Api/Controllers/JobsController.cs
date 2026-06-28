using CloudDocumentPipeline.Api.Contracts;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace CloudDocumentPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JobsController : ControllerBase
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
        ".txt", ".md", ".html", ".htm"
    ];

    private readonly ICorrelationContextAccessor _correlationContextAccessor;
    private readonly JobService _jobService;

    public JobsController(JobService jobService, ICorrelationContextAccessor correlationContextAccessor)
    {
        _jobService = jobService;
        _correlationContextAccessor = correlationContextAccessor;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        var jobId = await _jobService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = jobId },
            new
            {
                jobId,
                correlationId = _correlationContextAccessor.GetCorrelationId()
            });
    }

    [HttpPost("document-to-pdf")]
    [HttpPost("image-to-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateDocumentToPdf([FromForm] CreateDocumentToPdfForm request, CancellationToken cancellationToken)
    {
        var file = request.File;

        if (file.Length == 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = ["The uploaded file is empty."]
            }));
        }

        if (!IsSupported(file))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = ["Unsupported file type. Supported inputs are image, txt, md, and html files."]
            }));
        }

        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        var jobId = await _jobService.CreateDocumentToPdfAsync(
            request.Name,
            file.FileName,
            file.ContentType,
            memoryStream.ToArray(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = jobId },
            new
            {
                jobId,
                correlationId = _correlationContextAccessor.GetCorrelationId()
            });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var jobs = await _jobService.GetAllAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{id:guid}/result-file")]
    public async Task<IActionResult> DownloadResultFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await _jobService.GetResultFileAsync(id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        await _jobService.RetryAsync(id, cancellationToken);
        return NoContent();
    }

    private static bool IsSupported(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (SupportedExtensions.Contains(extension))
        {
            return true;
        }

        return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
