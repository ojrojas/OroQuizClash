using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.Admin.Services;

/// <summary>
/// /authentication/login + /authentication/logout endpoints (OIDC challenge / sign-out).
/// No credential forms exist in this app (Constitution VI): login and password change are
/// owned by OroIdentityServer (/connect/authorize, /Account/*).
/// </summary>
public static class AuthenticationEndpoints
{
    public const string OidcScheme = "OroIdentityServer";

    public static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");

        group.MapGet("/login", (string? returnUrl) => TypedResults.Challenge(GetAuthProperties(returnUrl)))
            .AllowAnonymous();
        group.MapPost("/login", (string? returnUrl) => TypedResults.Challenge(GetAuthProperties(returnUrl)))
            .AllowAnonymous();

        // Sign out of both the Cookie and OIDC handlers; otherwise the user would be
        // silently signed back in on the next authorized page visit.
        group.MapGet("/logout", (string? returnUrl) => TypedResults.SignOut(
            GetAuthProperties(returnUrl),
            [CookieAuthenticationDefaults.AuthenticationScheme, OidcScheme]));
        group.MapPost("/logout", ([FromForm] string? returnUrl) => TypedResults.SignOut(
            GetAuthProperties(returnUrl),
            [CookieAuthenticationDefaults.AuthenticationScheme, OidcScheme]));

        return group;
    }

    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            returnUrl = $"{pathBase}{returnUrl}";
        }

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}
