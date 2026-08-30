using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QuizArena.Admin.Client;
using QuizArena.Admin.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Authorization from deserialized claims (server → client). The client NEVER receives
// tokens — only the AuthenticationState (claims) flows across (BFF, oidc-config §4).
// EduCoreWeb adaptation: client mirrors server policies so [Authorize(Policy=...)] works in WASM.
// Must match AuthenticationServerConfiguration.cs exactly (roles/role/ClaimTypes.Role + IsInRole)
builder.Services.AddAuthorizationCore(options =>
{
    static bool HasRole(System.Security.Claims.ClaimsPrincipal user, string role) =>
        user.HasClaim(c => (c.Type == "roles" || c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase))
        || user.IsInRole(role);

    options.AddPolicy(AdminPolicies.AdminOnly, p => p.RequireAssertion(ctx => HasRole(ctx.User, AdminRoles.Admin)));
    options.AddPolicy(AdminPolicies.AdminOrGameManager, p => p.RequireAssertion(ctx => HasRole(ctx.User, AdminRoles.Admin) || HasRole(ctx.User, AdminRoles.GameManager)));
    options.AddPolicy(AdminPolicies.RewardManagerOrAdmin, p => p.RequireAssertion(ctx => HasRole(ctx.User, AdminRoles.Admin) || HasRole(ctx.User, AdminRoles.RewardManager)));
    options.AddPolicy(AdminPolicies.AnyAdminRole, p => p.RequireAssertion(ctx => HasRole(ctx.User, AdminRoles.Admin) || HasRole(ctx.User, AdminRoles.GameManager) || HasRole(ctx.User, AdminRoles.RewardManager)));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// 401 → re-authenticate via the OIDC challenge (FR-005).
builder.Services.AddTransient<SessionExpiredHandler>();

// Client*Services call the admin server's own /bff/* routes (same origin; the session
// cookie travels automatically and the YARP forwarder attaches the Bearer server-side).
builder.Services.AddHttpClient<IGamesAdminService, ClientGamesAdminService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<ICategoriesService, ClientCategoriesService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IQuestionsService, ClientQuestionsService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IPlayersService, ClientPlayersService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IRewardsService, ClientRewardsService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IRedemptionsService, ClientRedemptionsService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IReportsService, ClientReportsService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<IAuditService, ClientAuditService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<ILiveGamesService, ClientLiveGamesService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<ILiveGameService, ClientLiveGameService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddHttpClient<ILiveGameOperationsService, ClientLiveGameOperationsService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddTransient<IDashboardService, ClientDashboardService>();
builder.Services.AddHttpClient<IGameConfigurationService, ClientGameConfigurationService>(client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiredHandler>();
builder.Services.AddSingleton<ToastService>();

await builder.Build().RunAsync();
