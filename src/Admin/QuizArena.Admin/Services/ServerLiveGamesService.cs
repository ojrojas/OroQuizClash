using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR.Client;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

/// <summary>
/// InteractiveServer Live Games: connects to the API hub using the Aspire-resolved origin
/// (services:oroclash-api configuration injected by WithReference) and the operator's
/// access_token captured from the ambient HttpContext at subscription time.
/// </summary>
public sealed class ServerLiveGamesService(
    HttpClient httpClient,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
    : LiveGamesServiceCore(httpClient, "api")
{
    public override async Task<LiveGameSubscription> SubscribeAsync(Guid gameId, CancellationToken ct = default)
    {
        var baseAddress = ResolveApiBaseAddress();
        var accessToken = httpContextAccessor.HttpContext is { } context
            ? await context.GetTokenAsync("access_token")
            : null;

        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseAddress.TrimEnd('/')}/hubs/game", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        var subscription = new SignalRLiveGameSubscription(connection, gameId);
        await subscription.StartAsync(ct);
        return subscription;
    }

    private string ResolveApiBaseAddress()
    {
        // Aspire service discovery via configuration (injected by WithReference(api)).
        var discovered = configuration.GetSection("services:oroclash-api").GetChildren()
            .SelectMany(endpoint => endpoint.GetChildren())
            .FirstOrDefault(child => child.Value is not null)?.Value;
        return discovered ?? BffForwarderExtensions.ApiServiceName;
    }
}
