namespace CloudDocumentPipeline.Api.Contracts;

// Safe runtime environment summary returned to the frontend.
// This intentionally excludes secrets and full connection strings.
public sealed class SystemEnvironmentDto
{
    public string ApiEnvironment { get; init; } = string.Empty;

    public string DatabaseServer { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string MessagingProvider { get; init; } = string.Empty;

    public string MessagingTarget { get; init; } = string.Empty;

    public string StorageProvider { get; init; } = string.Empty;
}
