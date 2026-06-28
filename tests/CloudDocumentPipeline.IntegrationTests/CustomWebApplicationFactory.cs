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

// 闆嗘垚娴嬭瘯瀹夸富锛?
// 鍚姩鐪熷疄 API锛屽苟鎶?AppDbContext 鏇挎崲鎴?InMemory provider銆?
// 杩欐牱鍙互楠岃瘉 HTTP -> 搴旂敤鏈嶅姟 -> 鎸佷箙鍖栬惤搴撶殑瀹屾暣娴佺▼锛屽張涓嶇敤棰濆缁存姢娴嬭瘯涓撶敤鏁版嵁搴撱€?
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
            // 闆嗘垚娴嬭瘯鍙叧蹇?HTTP -> 搴旂敤鏈嶅姟 -> 鎸佷箙鍖栭摼璺€?
            // API 閲岄偅涓敤浜?SignalR 瀹炴椂杞彂鐨?RabbitMQ 鍚庡彴娑堣垂鑰呭湪 CI 娴嬭瘯鐜閲屾病鏈夊繀瑕佸惎鍔紝
            // 鍚﹀垯瀹冧細鍦ㄥ涓诲惎鍔ㄦ椂灏濊瘯杩炴帴 RabbitMQ锛屽鑷存祴璇曠幆澧冮澶栦緷璧栨秷鎭腑闂翠欢銆?
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
