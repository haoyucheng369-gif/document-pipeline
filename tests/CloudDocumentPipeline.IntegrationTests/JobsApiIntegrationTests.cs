using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace CloudDocumentPipeline.IntegrationTests;

// 鏈€灏?API 闆嗘垚娴嬭瘯锛?
// 閲嶇偣楠岃瘉 HTTP 鎺ュ彛銆佹湰鍦版枃浠跺瓨鍌ㄣ€佹暟鎹簱钀藉簱涓夎€呮槸鍚﹁兘涓€璧峰伐浣溿€?
public sealed class JobsApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JobsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDocumentToPdf_CreatesJobAndOutboxMessage()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Integration test document"), "name");
        form.Add(new ByteArrayContent("Hello from integration test"u8.ToArray()), "file", "sample.txt");

        var response = await client.PostAsync("/api/jobs/document-to-pdf", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateJobResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.JobId);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await dbContext.Jobs.FindAsync(body.JobId);
        Assert.NotNull(job);
        Assert.Equal("Integration test document", job!.Name);
        Assert.Equal("DocumentToPdf", job.Type);
        Assert.Contains("inputStorageKey", job.PayloadJson, StringComparison.OrdinalIgnoreCase);

        Assert.Single(dbContext.OutboxMessages);
        var outbox = dbContext.OutboxMessages.Single();
        Assert.Equal(nameof(CloudDocumentPipeline.Application.Messaging.JobCreatedIntegrationMessage), outbox.Type);
    }

    [Fact]
    public async Task GetJobById_ReturnsCreatedJob()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Lookup test document"), "name");
        form.Add(new ByteArrayContent("# heading"u8.ToArray()), "file", "sample.md");

        var createResponse = await client.PostAsync("/api/jobs/document-to-pdf", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateJobResponse>();

        var getResponse = await client.GetAsync($"/api/jobs/{created!.JobId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var job = await getResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal(created.JobId, job!.Id);
        Assert.Equal("Lookup test document", job.Name);
        Assert.Equal("DocumentToPdf", job.Type);
        Assert.Equal("Pending", job.Status);
    }

    private sealed class CreateJobResponse
    {
        public Guid JobId { get; set; }
        public string CorrelationId { get; set; } = default!;
    }

    private sealed class JobResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string Status { get; set; } = default!;
    }
}
