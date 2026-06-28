namespace CloudDocumentPipeline.Api.Contracts;

// 鐜鎽樿 DTO锛?
// 鍙繑鍥炲墠绔仈璋冨拰鐜璇嗗埆闇€瑕佺殑瀹夊叏鎽樿锛屼笉杩斿洖瀹屾暣 secrets 鎴栬繛鎺ヤ覆銆?
public sealed class SystemEnvironmentDto
{
    public string ApiEnvironment { get; init; } = string.Empty;

    public string DatabaseServer { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string MessagingProvider { get; init; } = string.Empty;

    public string MessagingTarget { get; init; } = string.Empty;

    public string StorageProvider { get; init; } = string.Empty;
}
