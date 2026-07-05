using CloudDocumentPipeline.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudDocumentPipeline.IntegrationTests;

// Integration-test host that runs the real API pipeline with in-memory persistence and local storage.
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _databaseName = default!;
    private string _storageRootPath = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _databaseName = $"docflowcloud-it-{Guid.NewGuid():N}";
        _storageRootPath = Path.Combine(Path.GetTempPath(), "docflowcloud-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRootPath);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "Local",
                ["Storage:Local:RootPath"] = _storageRootPath
            });
        });

        builder.ConfigureServices(services =>
        {
            // Integration tests cover HTTP -> application -> persistence; hosted broker consumers stay disabled.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_storageRootPath) && Directory.Exists(_storageRootPath))
        {
            Directory.Delete(_storageRootPath, recursive: true);
        }

        await base.DisposeAsync();
    }
}
