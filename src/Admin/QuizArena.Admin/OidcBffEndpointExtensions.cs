using System.Security.Claims;
using BuildingBlocks.ServiceDefaults.TokenStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using Yarp.ReverseProxy.Transforms;

namespace QuizArena.Admin;

public static class OidcBffEndpointExtensions
{
    public const string TokenStorageClaim = "oidc_sid";

    public static IEndpointRouteBuilder MapOidcBffEndpoints(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Api:BaseUrl"] ?? "http://oroclash-api";

        app.MapForwarder("/bff/{**catch-all}", apiBaseUrl, transformBuilder =>
        {
            transformBuilder.AddPathRemovePrefix("/bff");
            transformBuilder.AddPathPrefix("/api");
            transformBuilder.AddRequestTransform(async transformContext =>
            {
                var http = transformContext.HttpContext;
                string? accessToken = null;
                // 1) Redis via sid — authoritative store, carries expiry + refresh logic.
                //    Always consulted (even when a cookie token exists) so an expired
                //    cookie token does not block the refresh path and cause a 401 loop.
                var sid = http.User?.FindFirstValue(TokenStorageClaim);
                if (!string.IsNullOrEmpty(sid))
                {
                    var store = http.RequestServices.GetRequiredService<RedisTokenStore>();
                    accessToken = await store.GetAccessTokenAsync(sid);
                    if (!string.IsNullOrEmpty(accessToken) && IsExpiringSoon(await store.GetExpiryAsync(sid)))
                    {
                        var refreshToken = await store.GetRefreshTokenAsync(sid);
                        if (!string.IsNullOrEmpty(refreshToken))
                            accessToken = await TryRefreshAsync(http, store, sid, refreshToken) ?? accessToken;
                    }
                }
                // 2) Cookie fallback (e.g. Redis flushed between requests)
                if (string.IsNullOrEmpty(accessToken))
                {
                    try { accessToken = await http.GetTokenAsync("access_token"); } catch { }
                }
                // 3) Scoped holder (Blazor circuit)
                if (string.IsNullOrEmpty(accessToken))
                {
                    try
                    {
                        var holder = http.RequestServices.GetService<QuizArena.Admin.Services.AccessTokenHolder>();
                        if (!string.IsNullOrEmpty(holder?.Token)) accessToken = holder.Token;
                    }
                    catch { }
                }

                // BFF ya validó cookie (RequireAuthorization), marcar que la request viene autenticada vía BFF
                // aunque el Bearer falte; el Api puede hacer fallback a DB sin devolver 401 hard.
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-BFF-Proxied", "true");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-BFF-User", http.User.Identity?.Name ?? "unknown");
                if (!string.IsNullOrEmpty(sid)) transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-BFF-Sid", sid);

                if (!string.IsNullOrEmpty(accessToken))
                {
                    transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
                }
                else
                {
                    var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("QuizArena.Admin.BffForwarder");
                    logger.LogWarning("BFF forwarder: no access_token for {Path} user={User} sid={Sid} — se reenvía sin Bearer, el Api hará fallback a GamePlayers", http.Request.Path, http.User.Identity?.Name ?? "anon", http.User.FindFirstValue(TokenStorageClaim) ?? "none");
                }
            });
        }).RequireAuthorization();

        app.MapForwarder("/hubs/{**catch-all}", apiBaseUrl, transformBuilder =>
        {
            transformBuilder.AddRequestTransform(async transformContext =>
            {
                var http = transformContext.HttpContext;
                var sid = http.User?.FindFirstValue(TokenStorageClaim);
                if (string.IsNullOrEmpty(sid)) return;
                var store = http.RequestServices.GetRequiredService<RedisTokenStore>();
                var accessToken = await store.GetAccessTokenAsync(sid);
                if (!string.IsNullOrEmpty(accessToken))
                {
                    transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
                }
            });
        }).RequireAuthorization();

        app.MapGet("/Account/Login", (string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme])).AllowAnonymous();

        app.MapMethods("/Account/Logout", ["GET", "POST"], async (HttpContext context, RedisTokenStore tokenStore) =>
        {
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (result is not { Succeeded: true })
            {
                return Results.Redirect("/");
            }

            var sid = result.Principal?.FindFirstValue(TokenStorageClaim);
            if (!string.IsNullOrEmpty(sid))
            {
                await tokenStore.RemoveAsync(sid);
            }

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictClientAspNetCoreConstants.Properties.IdentityTokenHint] =
                    result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken)
            })
            {
                RedirectUri = "/"
            };

            return Results.SignOut(properties, [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
        });

        app.MapMethods("/callback", ["GET", "POST"], async (HttpContext context, RedisTokenStore tokenStore) =>
        {
            var result = await context.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            if (result is not { Succeeded: true, Principal.Identity.IsAuthenticated: true })
            {
                var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("QuizArena.Admin.OidcBffEndpointExtensions");
                var failure = result.Failure?.ToString() ?? "unknown error";
                var inner = result.Failure?.InnerException?.ToString() ?? "(no inner)";
                var error = result.Properties?.Items.TryGetValue("error", out var e) == true ? e : "(none)";
                var errorDescription = result.Properties?.Items.TryGetValue("error_description", out var d) == true ? d : "(none)";
                var errorUri = result.Properties?.Items.TryGetValue("error_uri", out var u) == true ? u : "(none)";
                logger.LogError(result.Failure, "OIDC callback authentication failed. Failure: {Failure} Inner: {Inner} error={Error} desc={Desc} uri={Uri} props={Props}",
                    failure, inner, error, errorDescription, errorUri, string.Join(";", result.Properties?.Items.Select(kv => $"{kv.Key}={kv.Value}") ?? []));

                // Don't throw 500 — redirect to login with error so the user can retry (EduCoreWeb pattern)
                // Preserve the original returnUrl if available
                var returnUrl = result.Properties?.RedirectUri ?? "/";
                // If the failure is due to state/expires, clear the corrupted state and retry login
                context.Response.Cookies.Delete(".AspNetCore.Correlation.OpenIddictClientAspNetCore.*");
                return Results.Redirect($"/authentication/login?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
            }

            var identity = new ClaimsIdentity(
                authenticationType: "ExternalLogin",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            identity.SetClaim(ClaimTypes.NameIdentifier, result.Principal.GetClaim(ClaimTypes.NameIdentifier))
                    .SetClaim(ClaimTypes.Name, result.Principal.GetClaim(ClaimTypes.Name))
                    .SetClaim(ClaimTypes.Email, result.Principal.GetClaim(ClaimTypes.Email));

            // Preserve must_change_password for Routes.razor gating (EduCoreWeb pattern)
            var mustChange = result.Principal.GetClaim("must_change_password");
            if (!string.IsNullOrEmpty(mustChange))
            {
                identity.SetClaim("must_change_password", mustChange);
            }

            // EduCoreWeb adaptation: emit roles in all 3 claim types so server + client policies match
            // regardless of downstream evaluation (HasClaim roles/role vs IsInRole/ClaimTypes.Role)
            static string NormRole(string raw)
            {
                raw = raw.Trim();
                if (raw.StartsWith("{", StringComparison.Ordinal))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(raw);
                        if (doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                            return v.GetString() ?? raw;
                        if (doc.RootElement.TryGetProperty("Value", out var v2) && v2.ValueKind == System.Text.Json.JsonValueKind.String)
                            return v2.GetString() ?? raw;
                    }
                    catch { }
                }
                return raw;
            }

            var roleValues = result.Principal.Claims
                .Where(claim => claim.Type is "role" or "roles" or ClaimTypes.Role)
                .Select(c => NormRole(c.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var role in roleValues)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                identity.AddClaim(new Claim("roles", role));
                identity.AddClaim(new Claim("role", role));
            }

            var sessionId = Guid.NewGuid().ToString("N");
            identity.SetClaim(TokenStorageClaim, sessionId);

            var accessToken = result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken);
            var refreshToken = result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken);
            var idToken = result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken);
            var expires = result.Properties.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessTokenExpirationDate);
            var expiresUtc = DateTimeOffset.TryParse(expires, out var exp) ? (DateTimeOffset?)exp : null;

            await tokenStore.SaveAsync(sessionId, accessToken ?? string.Empty, refreshToken, expiresUtc);

            var properties = new AuthenticationProperties(result.Properties.Items)
            {
                RedirectUri = SafeReturnUrl(result.Properties.RedirectUri)
            };

            properties.Items[TokenStorageClaim] = sessionId;
            // Keep tokens in cookie for BearerTokenHandler fallback (EduCoreWeb dual store)
            // Also persist for server-side HttpClient -> Api
            if (!string.IsNullOrEmpty(accessToken))
            {
                properties.StoreTokens([
                    new AuthenticationToken { Name = "access_token", Value = accessToken },
                    new AuthenticationToken { Name = "refresh_token", Value = refreshToken ?? string.Empty },
                    new AuthenticationToken { Name = "id_token", Value = idToken ?? string.Empty },
                    new AuthenticationToken { Name = "expires_at", Value = expiresUtc?.ToString("o") ?? expires ?? string.Empty }
                ]);
            }

            return Results.SignIn(new ClaimsPrincipal(identity), properties, CookieAuthenticationDefaults.AuthenticationScheme);
        }).AllowAnonymous();

        app.MapMethods("/logout-callback", ["GET", "POST"], async (HttpContext context) =>
        {
            var result = await context.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            return Results.Redirect(result?.Properties?.RedirectUri ?? "/");
        }).AllowAnonymous();

        return app;
    }

    internal static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
        // Allow absolute URLs to same host (e.g. https://localhost:7172/admin/dashboard from WASM)
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
        {
            // Only allow same-host redirects to prevent open redirect
            if (abs.Host == "localhost" && (abs.Port == 7172 || abs.Port == 5008 || abs.Port == 5086 || abs.Port == 5080))
                return abs.PathAndQuery;
            return "/";
        }
        if (!returnUrl.StartsWith('/')) return "/";
        if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.Contains('\\')) return "/";
        return Uri.TryCreate(returnUrl, UriKind.Relative, out _) ? returnUrl : "/";
    }

    private static bool IsExpiringSoon(DateTimeOffset? expiresUtc) =>
        expiresUtc is null || expiresUtc.Value <= DateTimeOffset.UtcNow.AddSeconds(30);

    private static async Task<string?> TryRefreshAsync(
        HttpContext http, RedisTokenStore store, string sid, string refreshToken)
    {
        try
        {
            var service = http.RequestServices.GetRequiredService<OpenIddict.Client.OpenIddictClientService>();
            var result = await service.AuthenticateWithRefreshTokenAsync(
                new OpenIddict.Client.OpenIddictClientModels.RefreshTokenAuthenticationRequest
                {
                    RegistrationId = "OroIdentityServer",
                    RefreshToken = refreshToken,
                    CancellationToken = http.RequestAborted
                });

            if (string.IsNullOrEmpty(result.AccessToken))
            {
                return null;
            }

            await store.SaveAsync(
                sid, result.AccessToken, result.RefreshToken ?? refreshToken, result.AccessTokenExpirationDate);

            return result.AccessToken;
        }
        catch (Exception ex)
        {
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("QuizArena.Admin.OidcBffEndpointExtensions");
            logger.LogWarning(ex, "Access token refresh failed for session {Sid}; falling back to the current token.", sid);
            return null;
        }
    }
}
