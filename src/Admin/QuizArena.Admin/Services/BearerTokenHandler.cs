using System.Net.Http.Headers;
using System.Security.Claims;
using BuildingBlocks.ServiceDefaults.TokenStorage;
using Microsoft.AspNetCore.Authentication;

namespace QuizArena.Admin.Services;

/// <summary>
/// Attaches the OIDC access_token as Bearer to outgoing HttpClient requests (InteractiveServer).
/// EduCoreWeb adaptation: tries cookie token first, then Redis fallback via oidc_sid claim,
/// so /callback dual-store (cookie + Redis) works regardless of storage mode.
/// </summary>
public sealed class BearerTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    RedisTokenStore tokenStore,
    AccessTokenHolder tokenHolder,
    IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var accessToken = null as string;
        var httpContext = httpContextAccessor.HttpContext;

        // 1) Try HttpContext (initial SSR request). Redis is consulted FIRST (with expiry
        //    + refresh) so an expired token still living in the cookie does not bypass the
        //    refresh path and cause a 401 loop; the cookie is only a fallback.
        if (httpContext is not null)
        {
            var sid = httpContext.User?.FindFirstValue(OidcBffEndpointExtensions.TokenStorageClaim);
            if (!string.IsNullOrEmpty(sid))
            {
                accessToken = await tokenStore.GetAccessTokenAsync(sid);
                accessToken = await TryRefreshIfNeededAsync(httpContext, sid, accessToken, cancellationToken);
            }
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = await httpContext.GetTokenAsync("access_token");
            }
            // Capture for the circuit (when HttpContext will be null)
            if (!string.IsNullOrEmpty(accessToken))
            {
                tokenHolder.Token = accessToken;
                tokenHolder.Sid = sid;
            }
        }

        // 2) Fallback to scoped holder (InteractiveServer circuit, HttpContext is null)
        if (string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(tokenHolder.Token))
        {
            accessToken = tokenHolder.Token;
            // Also try refresh via holder's Sid if available
            if (!string.IsNullOrEmpty(tokenHolder.Sid))
            {
                var sid = tokenHolder.Sid;
                var expiry = await tokenStore.GetExpiryAsync(sid);
                if (expiry is not null && expiry.Value <= DateTimeOffset.UtcNow.AddSeconds(30))
                {
                    var refreshToken = await tokenStore.GetRefreshTokenAsync(sid);
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        try
                        {
                            var oidcService = serviceProvider.GetRequiredService<OpenIddict.Client.OpenIddictClientService>();
                            var result = await oidcService.AuthenticateWithRefreshTokenAsync(
                                new OpenIddict.Client.OpenIddictClientModels.RefreshTokenAuthenticationRequest
                                {
                                    RegistrationId = "OroIdentityServer",
                                    RefreshToken = refreshToken,
                                    CancellationToken = cancellationToken
                                });
                            if (!string.IsNullOrEmpty(result.AccessToken))
                            {
                                await tokenStore.SaveAsync(sid, result.AccessToken, result.RefreshToken ?? refreshToken, result.AccessTokenExpirationDate);
                                accessToken = result.AccessToken;
                                tokenHolder.Token = accessToken;
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        // 3) Last resort: try to get token from AuthenticationStateProvider (WASM-prerender)
        if (string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var authStateProvider = serviceProvider.GetService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
                if (authStateProvider is not null)
                {
                    var authState = await authStateProvider.GetAuthenticationStateAsync();
                    var sid = authState.User.FindFirstValue(OidcBffEndpointExtensions.TokenStorageClaim);
                    if (!string.IsNullOrEmpty(sid))
                    {
                        accessToken = await tokenStore.GetAccessTokenAsync(sid);
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> TryRefreshIfNeededAsync(HttpContext httpContext, string sid, string? accessToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(accessToken)) return accessToken;
        var expiry = await tokenStore.GetExpiryAsync(sid);
        if (expiry is null || expiry.Value > DateTimeOffset.UtcNow.AddSeconds(30)) return accessToken;
        var refreshToken = await tokenStore.GetRefreshTokenAsync(sid);
        if (string.IsNullOrEmpty(refreshToken)) return accessToken;
        try
        {
            var oidcService = httpContext.RequestServices.GetRequiredService<OpenIddict.Client.OpenIddictClientService>();
            var result = await oidcService.AuthenticateWithRefreshTokenAsync(
                new OpenIddict.Client.OpenIddictClientModels.RefreshTokenAuthenticationRequest
                {
                    RegistrationId = "OroIdentityServer",
                    RefreshToken = refreshToken,
                    CancellationToken = ct
                });
            if (!string.IsNullOrEmpty(result.AccessToken))
            {
                await tokenStore.SaveAsync(sid, result.AccessToken, result.RefreshToken ?? refreshToken, result.AccessTokenExpirationDate);
                return result.AccessToken;
            }
        }
        catch { }
        return accessToken;
    }
}
