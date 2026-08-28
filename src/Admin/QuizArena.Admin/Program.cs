using BuildingBlocks.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using QuizArena.Admin.Client;
using QuizArena.Admin.Client.Services;
using QuizArena.Admin.Components;
using QuizArena.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// OTel, health checks (/health, /alive), resilience — BuildingBlocks.ServiceDefaults.
builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Authentication: OIDC authorization_code + refresh_token against OroIdentityServer
// (Constitution VI — sole identity authority). Tokens live ONLY in the server
// sign-in cookie (BFF pattern); the browser never sees them (FR-030, SC-003).
// ---------------------------------------------------------------------------

builder.Services.AddAuthentication(AuthenticationEndpoints.OidcScheme)
    .AddOpenIdConnect(AuthenticationEndpoints.OidcScheme, oidcOptions =>
    {
        oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        oidcOptions.Authority = builder.Configuration["Identity:Authority"];
        oidcOptions.ClientId = builder.Configuration["Identity:ClientId"] ?? "quizarena-admin";
        oidcOptions.ClientSecret = builder.Configuration["Identity:ClientSecret"];
        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
        oidcOptions.RequireHttpsMetadata = false; // Aspire internal http endpoint; TLS terminated at proxy
        oidcOptions.MapInboundClaims = false;
        oidcOptions.GetClaimsFromUserInfoEndpoint = true;
        oidcOptions.TokenValidationParameters.NameClaimType = "name";
        oidcOptions.TokenValidationParameters.RoleClaimType = "roles";
        oidcOptions.TokenValidationParameters.ValidateIssuer = false;
        oidcOptions.TokenValidationParameters.ValidateAudience = false;

        var apiScope = builder.Configuration["Identity:ApiScope"];
        if (!string.IsNullOrWhiteSpace(apiScope))
        {
            oidcOptions.Scope.Add(apiScope);
        }
        // Callback paths default to /signin-oidc, /signout-callback-oidc, /signout-oidc
        // and must match the quizarena-admin client registration in OroIdentityServer.
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

// Non-interactive refresh: reissue the cookie with a fresh access_token when it nears
// expiry; sign the user out if the refresh_token can no longer be exchanged.
builder.Services.ConfigureCookieOidc(CookieAuthenticationDefaults.AuthenticationScheme, AuthenticationEndpoints.OidcScheme);

// Local policies mirror QuizArena.Api SecurityPolicies (contracts/oidc-config.md §6).
// The API remains the final authority (403) — the UI only hides what is not allowed.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminPolicies.AdminOnly, policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(c => (c.Type == "roles" || c.Type == "role") && c.Value == AdminRoles.Admin)));
    options.AddPolicy(AdminPolicies.AdminOrGameManager, policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(c => (c.Type == "roles" || c.Type == "role") &&
            (c.Value == AdminRoles.Admin || c.Value == AdminRoles.GameManager))));
    options.AddPolicy(AdminPolicies.RewardManagerOrAdmin, policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(c => (c.Type == "roles" || c.Type == "role") &&
            (c.Value == AdminRoles.Admin || c.Value == AdminRoles.RewardManager))));
    // Dashboard/Reports are visible to all three roles (research R8: REWARD_MANAGER sees
    // Dashboard, Rewards and Reports).
    options.AddPolicy(AdminPolicies.AnyAdminRole, policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(c => (c.Type == "roles" || c.Type == "role") &&
            (c.Value == AdminRoles.Admin || c.Value == AdminRoles.GameManager || c.Value == AdminRoles.RewardManager))));
});

// Flow the AuthenticationState (claims only — never tokens) server → client.
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

// BFF: YARP forwarder + Aspire service discovery; Server*Services attach the Bearer
// token per request from the ambient HttpContext.
builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddAdminServerServices();
builder.Services.AddAdminApiHttpClient<IGamesAdminService, ServerGamesAdminService>();
builder.Services.AddAdminApiHttpClient<ICategoriesService, ServerCategoriesService>();
builder.Services.AddAdminApiHttpClient<IQuestionsService, ServerQuestionsService>();
builder.Services.AddAdminApiHttpClient<IPlayersService, ServerPlayersService>();
builder.Services.AddAdminApiHttpClient<IRewardsService, ServerRewardsService>();
builder.Services.AddAdminApiHttpClient<IRedemptionsService, ServerRedemptionsService>();
builder.Services.AddAdminApiHttpClient<IReportsService, ServerReportsService>();
builder.Services.AddAdminApiHttpClient<IAuditService, ServerAuditService>();
builder.Services.AddTransient<IDashboardService, ServerDashboardService>();
builder.Services.AddAdminApiHttpClient<ILiveGamesService, ServerLiveGamesService>();
builder.Services.AddAdminApiHttpClient<QuizArena.Admin.Client.Services.IGameConfigurationService, ServerGameConfigurationService>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(QuizArena.Admin.Client._Imports).Assembly);

// BFF forwarders: /bff/{**} → oroclash-api /api/{**} and /hubs/game (both RequireAuthorization).
app.MapBffForwarder();
app.MapGameHubForwarder();

// OIDC challenge / sign-out endpoints (no credential forms — Constitution VI).
app.MapGroup("/authentication").MapLoginAndLogout();

app.MapDefaultEndpoints();

app.Run();
