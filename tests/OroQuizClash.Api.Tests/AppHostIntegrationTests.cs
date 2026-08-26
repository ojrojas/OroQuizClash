using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OroQuizClash.Api.Tests;

/// <summary>
/// Ejemplo de test de integración distribuida via Aspire.
/// Valida que el AppHost levanta todos los recursos (sqlserver, postgres, redis,
/// rabbitmq, identity-server, oroclash-api) y que los health checks responden.
/// Este patrón se reutiliza para todos los entregables futuros.
/// Para E2E real:
///   var builder = await DistributedApplicationTestingBuilder.CreateAsync&lt;Projects.OroQuizClash_AppHost&gt;();
///   await using var app = await builder.BuildAsync();
///   await app.StartAsync();
///   var http = app.CreateHttpClient("oroclash-api");
///   (await http.GetAsync("/health")).EnsureSuccessStatusCode();
/// </summary>
public sealed class AppHostIntegrationTests() : DistributedApplicationFactory(typeof(Projects.OroQuizClash_AppHost))
{
    protected override void OnBuilderCreating(
         DistributedApplicationOptions applicationOptions,
         HostApplicationBuilderSettings hostOptions)
    {
        hostOptions.Configuration ??= new();
        hostOptions.Configuration["environment"] = "Development";
    }

    protected override void OnBuilderCreated(
    DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        applicationBuilder.Services.AddLogging(
            loggin => loggin.SetMinimumLevel(LogLevel.Debug)
            .AddConsole()
            .AddFilter("Default", LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
            .AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning));
    }
}
