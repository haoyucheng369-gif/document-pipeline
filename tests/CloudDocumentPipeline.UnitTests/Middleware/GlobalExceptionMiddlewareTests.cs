using System.Text.Json;
using CloudDocumentPipeline.Api.Middleware;
using CloudDocumentPipeline.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudDocumentPipeline.UnitTests.Middleware;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MapsJobNotFoundExceptionTo404ProblemDetails()
    {
        var context = CreateContext();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new JobNotFoundException(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Job not found", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvokeAsync_MapsInvalidJobStateExceptionTo409ProblemDetails()
    {
        var context = CreateContext();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidJobStateException(Guid.NewGuid(), "Only failed jobs can be retried."),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("Invalid job state", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("Only failed jobs can be retried.", document.RootElement.GetProperty("detail").GetString());
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }
}
