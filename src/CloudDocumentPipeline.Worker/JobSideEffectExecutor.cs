using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Processing;
using CloudDocumentPipeline.Application.Abstractions.Storage;
using CloudDocumentPipeline.Application.Jobs;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CloudDocumentPipeline.Worker;

// 鍓綔鐢ㄦ墽琛屽櫒锛?
// 鐪熸鎵胯浇鈥滄枃妗ｈ浆 PDF鈥濈殑涓氬姟閫昏緫銆?
// 褰撳墠鏀规垚鍏堜粠瀛樺偍灞傝鍙栧師鏂囦欢锛屽啀鎶?PDF 缁撴灉鍐欏洖瀛樺偍灞傘€?
public sealed class JobSideEffectExecutor : IJobSideEffectExecutor
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<JobSideEffectExecutor> _logger;

    public JobSideEffectExecutor(IFileStorage fileStorage, ILogger<JobSideEffectExecutor> logger)
    {
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        Guid jobId,
        string jobType,
        string payloadJson,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        using var activity = CloudDocumentPipelineTracing.ActivitySource.StartActivity("job.side-effect.execute", ActivityKind.Internal);
        activity?.SetTag("job.id", jobId);
        activity?.SetTag("job.type", jobType);
        activity?.SetTag("idempotency.key", idempotencyKey);
        activity?.SetTag("correlation.id", correlationId);

        _logger.LogInformation(
            "Executing external side effect. JobId: {JobId}, JobType: {JobType}, IdempotencyKey: {IdempotencyKey}, CorrelationId: {CorrelationId}",
            jobId,
            jobType,
            idempotencyKey,
            correlationId);

        var outboundHeaders = new Dictionary<string, string>
        {
            [CorrelationConstants.HeaderName] = correlationId
        };

        if (!string.Equals(jobType, JobService.DocumentToPdfJobType, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported job type '{jobType}'.");
        }

        var payload = JsonSerializer.Deserialize<DocumentToPdfJobPayload>(payloadJson, JsonSerializerOptions)
            ?? throw new JsonException("DocumentToPdf payload is invalid.");

        // 澶勭悊闃舵浠庤緭鍏?storage key 璇诲彇鍘熸枃浠讹紝鍐嶆寜鏂囦欢绫诲瀷鐢熸垚 PDF銆?
        var fileBytes = await _fileStorage.ReadAsync(payload.InputStorageKey, cancellationToken)
            ?? throw new FileNotFoundException(
                $"Input file '{payload.InputStorageKey}' was not found in storage.");

        var pdfBytes = GeneratePdf(payload, fileBytes);
        var outputFileName = $"{Path.GetFileNameWithoutExtension(payload.OriginalFileName)}.pdf";
        var outputStorageKey = await _fileStorage.SaveAsync(
            "results",
            outputFileName,
            pdfBytes,
            cancellationToken);

        var result = new DocumentToPdfJobResult
        {
            OutputFileName = outputFileName,
            OutputStorageKey = outputStorageKey,
            GeneratedAtUtc = DateTime.UtcNow
        };
        activity?.SetTag("storage.output.key", outputStorageKey);
        activity?.SetTag("storage.output.file_name", outputFileName);

        _logger.LogInformation(
            "Document to PDF conversion completed. JobId: {JobId}, OutputFileName: {OutputFileName}",
            jobId,
            result.OutputFileName);

        var resultJson = JsonSerializer.Serialize(new
        {
            generatedAtUtc = result.GeneratedAtUtc,
            status = "OK",
            jobType = JobService.DocumentToPdfJobType,
            idempotencyKey,
            correlationId,
            outboundHeaders,
            outputFileName = result.OutputFileName,
            outputStorageKey = result.OutputStorageKey
        }, JsonSerializerOptions);

        return resultJson;
    }

    private static byte[] GeneratePdf(DocumentToPdfJobPayload payload, byte[] fileBytes)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(12));

                if (IsImage(payload))
                {
                    page.Content().Image(fileBytes).FitArea();
                    return;
                }

                var text = ExtractText(payload, fileBytes);
                page.Content().Column(column =>
                {
                    column.Item().Text(payload.OriginalFileName).Bold().FontSize(16);
                    column.Item().PaddingTop(12).Text(text);
                });
            });
        }).GeneratePdf();
    }

    private static bool IsImage(DocumentToPdfJobPayload payload)
    {
        return payload.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || IsExtension(payload, ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp");
    }

    private static string ExtractText(DocumentToPdfJobPayload payload, byte[] fileBytes)
    {
        var rawText = Encoding.UTF8.GetString(fileBytes);

        if (payload.ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
            IsExtension(payload, ".html", ".htm"))
        {
            return ConvertHtmlToPlainText(rawText);
        }

        if (payload.ContentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase) ||
            IsExtension(payload, ".md"))
        {
            return ConvertMarkdownToPlainText(rawText);
        }

        return rawText;
    }

    private static string ConvertHtmlToPlainText(string html)
    {
        var withLineBreaks = Regex.Replace(html, @"<(br|/p|/div|/li|/h[1-6])\b[^>]*>", Environment.NewLine, RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<[^>]+>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private static string ConvertMarkdownToPlainText(string markdown)
    {
        var text = markdown
            .Replace("### ", string.Empty)
            .Replace("## ", string.Empty)
            .Replace("# ", string.Empty)
            .Replace("**", string.Empty)
            .Replace("__", string.Empty)
            .Replace("`", string.Empty);

        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]+\)", string.Empty);
        text = Regex.Replace(text, @"\[(?<text>[^\]]+)\]\([^)]+\)", "${text}");
        text = Regex.Replace(text, @"^\s*[-*+]\s+", string.Empty, RegexOptions.Multiline);
        return text.Trim();
    }

    private static bool IsExtension(DocumentToPdfJobPayload payload, params string[] extensions)
    {
        var extension = Path.GetExtension(payload.OriginalFileName);
        return extensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
    }
}
