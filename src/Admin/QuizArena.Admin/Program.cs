using System.Security.Claims;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.TokenStorage;
using Microsoft.AspNetCore.Authentication;
using QuizArena.Admin;
using QuizArena.Admin.Client;
using QuizArena.Admin.Client.Services;
using QuizArena.Admin.Components;
using QuizArena.Admin.Extensions;
using QuizArena.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var identityAuthority = builder.Configuration["Oidc:Authority"] ?? builder.Configuration["Identity:Authority"] ?? "http://localhost:5080";
var webClientId = builder.Configuration["Oidc:ClientId"] ?? builder.Configuration["Identity:ClientId"] ?? "quizarena-admin";

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

builder.Services.AddCascadingAuthenticationState();

builder.AddAuthenticationServerService();
builder.AddAuthorizationServerService();

builder.AddIdentityServerOpenIddict(identityAuthority, webClientId);

builder.Services.AddRedisTokenStore();
builder.Services.AddServiceDiscovery();

// BFF: YARP forwarder + Aspire service discovery; Server*Services attach the Bearer
// token per request from the ambient HttpContext via Redis.
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
builder.Services.AddAdminApiHttpClient<ILiveGameService, ServerLiveGameService>();
builder.Services.AddAdminApiHttpClient<ILiveGameOperationsService, ServerLiveGameOperationsService>();
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

// Capture access_token for the Blazor circuit (where HttpContext is null)
// EduCoreWeb BFF: token lives in cookie+Redis, circuit needs it via scoped holder
app.Use(async (ctx, next) =>
{
    var holder = ctx.RequestServices.GetRequiredService<AccessTokenHolder>();
    // Try cookie first
    var token = await ctx.GetTokenAsync("access_token");
    if (string.IsNullOrEmpty(token))
    {
        var sid = ctx.User.FindFirstValue(QuizArena.Admin.OidcBffEndpointExtensions.TokenStorageClaim);
        if (!string.IsNullOrEmpty(sid))
        {
            var store = ctx.RequestServices.GetRequiredService<RedisTokenStore>();
            token = await store.GetAccessTokenAsync(sid);
        }
    }
    holder.Token = token;
    holder.Sid = ctx.User.FindFirstValue(QuizArena.Admin.OidcBffEndpointExtensions.TokenStorageClaim);
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();

// BFF y OIDC deben mapearse ANTES del fallback Razor para que /bff/* y /hubs/* no sean capturados por UI (404)
// YARP forwarder tiene prioridad sobre Razor fallback
app.MapOidcBffEndpoints(builder.Configuration);
// Keep /authentication/login as alias to /Account/Login for WASM compatibility (spec 017)
app.MapGroup("/authentication").MapLoginAndLogout();

// DEBUG: token check for 401 diagnosis
app.MapGet("/debug/token", async (HttpContext ctx, BuildingBlocks.ServiceDefaults.TokenStorage.RedisTokenStore store) =>
{
    var cookieToken = await ctx.GetTokenAsync("access_token");
    var sid = ctx.User.FindFirstValue(QuizArena.Admin.OidcBffEndpointExtensions.TokenStorageClaim);
    string? redisToken = null;
    if (!string.IsNullOrEmpty(sid)) redisToken = await store.GetAccessTokenAsync(sid);
    return Results.Json(new { hasCookieToken = !string.IsNullOrEmpty(cookieToken), cookiePrefix = cookieToken?.Substring(0, Math.Min(20, cookieToken.Length)), hasRedisToken = !string.IsNullOrEmpty(redisToken), redisPrefix = redisToken?.Substring(0, Math.Min(20, redisToken.Length)), user = ctx.User.Identity?.Name, roles = string.Join(",", ctx.User.Claims.Where(c => c.Type == "roles" || c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)), sid });
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(QuizArena.Admin.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
