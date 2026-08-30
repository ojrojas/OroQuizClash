using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace QuizArena.Admin.Extensions;

public static class AuthenticationServerConfiguration
{
    public static TBuilder AddAuthenticationServerService<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddAuthentication(options => options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromDays(1);
            options.SlidingExpiration = true;
        });
        return builder;
    }

    public static TBuilder AddAuthorizationServerService<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("CookieAuthenticationPolicy", policy =>
            {
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });

            // EduCoreWeb-like: normalize all role claim types (roles/role/ClaimTypes.Role) + IsInRole
            // Mirrors OroQuizClash.Api SecurityPolicies and AdminUserState.FromPrincipal
            static bool HasRole(ClaimsPrincipal user, string role) =>
                user.HasClaim(c => (c.Type == "roles" || c.Type == "role" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase))
                || user.IsInRole(role);

            options.AddPolicy(QuizArena.Admin.Client.AdminPolicies.AdminOnly, policy => policy.RequireAssertion(ctx =>
                HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.Admin)));
            options.AddPolicy(QuizArena.Admin.Client.AdminPolicies.AdminOrGameManager, policy => policy.RequireAssertion(ctx =>
                HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.Admin) || HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.GameManager)));
            options.AddPolicy(QuizArena.Admin.Client.AdminPolicies.RewardManagerOrAdmin, policy => policy.RequireAssertion(ctx =>
                HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.Admin) || HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.RewardManager)));
            options.AddPolicy(QuizArena.Admin.Client.AdminPolicies.AnyAdminRole, policy => policy.RequireAssertion(ctx =>
                HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.Admin) || HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.GameManager) || HasRole(ctx.User, QuizArena.Admin.Client.AdminRoles.RewardManager)));
        });

        return builder;
    }
}
