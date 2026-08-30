using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace QuizArena.Admin.Services;

/// <summary>
/// BFF forwarders (contracts/bff-endpoints.md):
/// - REST catch-all: /bff/{**catch-all} → http://oroclash-api/api/{**catch-all} with the
///   access_token from the OIDC cookie attached server-side as Authorization: Bearer.
/// - SignalR hub: /hubs/game → http://oroclash-api/hubs/game (negotiate + WebSockets proxied).
/// The browser never sees the API origin nor the token (Constitution H, FR-030, SC-003).
/// </summary>
public static class BffForwarderExtensions
{
    public const string ApiServiceName = "http://oroclash-api";

    public static IApplicationBuilder UseBffForwarder(this WebApplication app)
    {
        app.MapBffForwarder();
        app.MapGameHubForwarder();
        return app;
    }

    public static void MapBffForwarder(this WebApplication app)
    {
        app.MapForwarder("/bff/{**catch-all}", ApiServiceName, static transformBuilder =>
        {
            // /bff/{rest} → /api/{rest}
            transformBuilder.AddPathRemovePrefix("/bff");
            transformBuilder.AddPathPrefix("/api");
            AddBearerTransform(transformBuilder);
        }).RequireAuthorization();
    }

    public static void MapGameHubForwarder(this WebApplication app)
    {
        app.MapForwarder("/hubs/game", ApiServiceName, static transformBuilder =>
        {
            AddBearerTransform(transformBuilder);
        }).RequireAuthorization();
    }

    private static void AddBearerTransform(TransformBuilderContext transformBuilder)
    {
        transformBuilder.AddRequestTransform(async transformContext =>
        {
            var http = transformContext.HttpContext;
            var accessToken = await http.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                var sid = http.User?.FindFirstValue(OidcBffEndpointExtensions.TokenStorageClaim);
                if (!string.IsNullOrEmpty(sid))
                {
                    var store = http.RequestServices.GetRequiredService<BuildingBlocks.ServiceDefaults.TokenStorage.RedisTokenStore>();
                    accessToken = await store.GetAccessTokenAsync(sid);
                }
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
            }
        });
    }
}
