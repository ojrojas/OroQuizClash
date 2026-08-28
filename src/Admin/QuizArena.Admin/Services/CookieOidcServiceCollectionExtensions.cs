using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using QuizArena.Admin.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Attaches the cookie OnValidatePrincipal callback that refreshes the access_token with the
/// refresh_token (non-interactive) and configures OIDC options to save tokens and request
/// the offline_access scope. Pattern from the official BlazorWebAppOidcBffAutoYarpAspire sample.
/// </summary>
internal static class CookieOidcServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCookieOidc(this IServiceCollection services, string cookieScheme, string oidcScheme)
    {
        services.AddSingleton<CookieOidcRefresher>();
        services.AddOptions<CookieAuthenticationOptions>(cookieScheme).Configure<CookieOidcRefresher>((cookieOptions, refresher) =>
        {
            cookieOptions.Events.OnValidatePrincipal = context => refresher.ValidateOrRefreshCookieAsync(context, oidcScheme);
        });
        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
            // Request a refresh_token.
            oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);
            // Store the access/refresh tokens in the sign-in cookie (server-side only — BFF).
            oidcOptions.SaveTokens = true;
        });
        return services;
    }
}
