using Application.Interfaces;
using Infrastructure.Database;
using Infrastructure.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Common;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private HttpClient? _testHttpClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<KycKybDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<KycKybDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Substitui IWebhookDispatcher para usar o client de teste em memória
            var dispatcherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWebhookDispatcher));
            if (dispatcherDescriptor != null)
            {
                services.Remove(dispatcherDescriptor);
            }

            services.AddScoped<IWebhookDispatcher>(sp =>
            {
                _testHttpClient ??= CreateClient();
                var repo = sp.GetRequiredService<Domain.Repositories.IWebhookSubscriptionRepository>();
                var logger = sp.GetRequiredService<ILogger<WebhookDispatcher>>();
                return new WebhookDispatcher(_testHttpClient, repo, logger);
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }
}
