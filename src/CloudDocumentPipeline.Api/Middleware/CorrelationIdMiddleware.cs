using CloudDocumentPipeline.Application.Abstractions.Observability;
using Serilog.Context;

namespace CloudDocumentPipeline.Api.Middleware;

// 鐩稿叧鎬ф爣璇嗕腑闂翠欢锛?
// 缁欐瘡涓?HTTP 璇锋眰鍒嗛厤涓€涓?CorrelationId锛屽苟鎶婂畠璐┛鍒版棩蹇楀拰鍝嶅簲澶撮噷銆?
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 浼樺厛娌跨敤涓婃父浼犳潵鐨?CorrelationId锛涙病鏈夊氨鑷繁鐢熸垚涓€涓€?
        var correlationId = GetOrCreateCorrelationId(context);

        // 鏀惧埌 HttpContext.Items 閲岋紝鍚庣画鏈嶅姟鍜屼腑闂翠欢鍙互缁熶竴璇诲彇銆?
        context.Items[CorrelationConstants.HeaderName] = correlationId;
        // 鍥炲啓鍒板搷搴斿ご锛屼究浜庡墠绔垨璋冪敤鏂规嬁鍒拌繖鏉￠摼璺紪鍙枫€?
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        // 鍘嬪叆鏃ュ織涓婁笅鏂囷紝鍚庣画鏈璇锋眰鍐呯殑鏃ュ織閮借兘鑷姩甯︿笂杩欎釜灞炴€с€?
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // 濡傛灉涓婃父宸茬粡浼犱簡 X-Correlation-Id锛屽氨娌跨敤瀹冦€?
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values))
        {
            var incoming = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming;
            }
        }

        // 鍚﹀垯閫€鍥炲埌 ASP.NET Core 鑷甫鐨?TraceIdentifier銆?
        return context.TraceIdentifier;
    }
}
