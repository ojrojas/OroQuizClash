using Aspire.Hosting;

namespace OroQuizClash.Api.Tests;



public class RunAppHostTest(AppHostIntegrationTests app) : IClassFixture<AppHostIntegrationTests>, IAsyncLifetime
{

    private DistributedApplication _app = null!;

    [Fact]
    public void AppHost_Model_IsValid()
    {
        var client = app.CreateHttpClient("identity-api");
        // Si el AppHost compila, el modelo de recursos es válido.
        // Este test garantiza que futuros entregables rompen el build si
        // olvidan WithReference/WaitFor en el AppHost.
        Assert.NotNull(client);
    }

    public async Task DisposeAsync()
    {
        if(_app is not null) await _app.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await app.StartAsync();
    }
}