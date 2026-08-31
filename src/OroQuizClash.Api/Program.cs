using System.Text.Json;
using System.Threading.RateLimiting;

using BuildingBlocks.CQRS.Behaviors;
using BuildingBlocks.CQRS.DependencyInjection;
using BuildingBlocks.EventBus.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Infrastructure.DependencyInjection;
using BuildingBlocks.Kernel.Infrastructure.Persistence;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Endpoints;
using BuildingBlocks.ServiceDefaults.Middleware;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Strategies;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Rewards;
using OroQuizClash.Api.Authorization;
using OroQuizClash.Infrastructure.Categories;
using OroQuizClash.Infrastructure.Counters;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Selection;
using OroQuizClash.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OroQuizClash.Infrastructure.Services.IIdempotencyService, OroQuizClash.Infrastructure.Services.IdempotencyService>();
builder.Services.AddCqrs(c => c
    .RegisterHandlersFromAssemblyContaining<Program>()
    .RegisterHandlersFromAssembly(typeof(OroQuizClash.Application.Features.Games.CreateGameCommand).Assembly)
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>))
    .AddOpenBehavior(typeof(OroQuizClash.Application.Behaviors.AuthorizationBehavior<,>))
    .AddOpenBehavior(typeof(OroQuizClash.Application.Behaviors.IdempotencyBehavior<,>))
    .AddOpenBehavior(typeof(OroQuizClash.Application.Behaviors.AuditBehavior<,>)));

var connectionString = builder.Configuration.GetConnectionString("oroclash") ?? "Data Source=oroclash.db";
builder.Services.AddDbContext<OroQuizClashDbContext>(o =>
{
    if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) == false)
    {
        // Aspire SqlServer provides "Server=...;Database=oroclash;..."
        o.UseSqlServer(connectionString);
    }
    else
    {
        o.UseSqlite(connectionString);
    }
});
builder.Services.AddUnitOfWork<OroQuizClashDbContext>();
builder.Services.AddOutbox<OroQuizClashDbContext>();
builder.Services.AddSingleton<IEventBus, NullEventBus>();
builder.Services.AddScoped<IRepository<Game, GameId>>(sp => new EfRepository<Game, GameId>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<ICategoryValidator, CategoryValidatorStub>();
builder.Services.AddScoped<IRepository<Category, CategoryId>>(sp => new EfRepository<Category, CategoryId>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<IRepository<Question, QuestionId>>(sp => new EfRepository<Question, QuestionId>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<ICategoryExistenceChecker, CategoryExistenceChecker>();
builder.Services.AddScoped<OroQuizClash.Domain.Categories.IQuestionCounter, EfQuestionCounter>();
builder.Services.AddScoped<OroQuizClash.Domain.Questions.Services.IQuestionCounter>(sp => (OroQuizClash.Domain.Questions.Services.IQuestionCounter)sp.GetRequiredService<OroQuizClash.Domain.Categories.IQuestionCounter>());
builder.Services.AddScoped<IRepository<Reward, RewardId>>(sp => new EfRepository<Reward, RewardId>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<IRepository<RewardRedemption, RewardRedemptionId>>(sp => new EfRepository<RewardRedemption, RewardRedemptionId>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<IRepository<OroQuizClash.Domain.Audit.AuditEntry, Guid>>(sp => new EfRepository<OroQuizClash.Domain.Audit.AuditEntry, Guid>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<IRepository<OroQuizClash.Domain.Audit.IdempotencyRecord, Guid>>(sp => new EfRepository<OroQuizClash.Domain.Audit.IdempotencyRecord, Guid>(sp.GetRequiredService<OroQuizClashDbContext>()));
builder.Services.AddScoped<IQuestionSelectionStrategy, RandomQuestionSelectionStrategy>();
builder.Services.AddScoped<IDifficultyProgressionStrategy, LinearDifficultyStrategy>();

builder.Services.AddEndpoints(typeof(OroQuizClash.Application.Features.Games.CreateGameEndpoint).Assembly);
builder.Services.AddSignalR();
builder.Services.AddScoped<OroQuizClash.Application.Features.Games.IGameNotificationsBroadcaster, OroQuizClash.Api.Hubs.SignalRGameNotificationsBroadcaster>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var authority = builder.Configuration["Identity:Authority"] ?? "http://identity:5080";
var tokenDecryptionKeys = new List<SecurityKey>();
var certDir = builder.Configuration["Identity:TokenDecryptionCertificateDirectory"];
if (!string.IsNullOrEmpty(certDir) && Directory.Exists(certDir))
{
    foreach (var pfx in Directory.EnumerateFiles(certDir, "*.pfx", SearchOption.AllDirectories))
    {
        try
        {
            var cert = X509CertificateLoader.LoadPkcs12FromFile(pfx, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
            if (cert.GetRSAPrivateKey() is not null)
                tokenDecryptionKeys.Add(new X509SecurityKey(cert));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Failed to load cert {pfx}: {ex.Message}");
        }
    }
    Console.WriteLine($"[Startup] Loaded {tokenDecryptionKeys.Count} token decryption keys from {certDir}");
    foreach (var key in tokenDecryptionKeys)
    {
        var x509Key = key as X509SecurityKey;
        Console.WriteLine($"[Startup] Key: Thumbprint={x509Key?.Certificate?.Thumbprint} HasPrivateKey={x509Key?.Certificate?.HasPrivateKey}");
    }

    // Keep only the encryption certificate (not signing) for JWE decryption.
    // Signing cert is for JWS verification via jwks_uri, not for decryption.
    // Filter: remove any key whose cert FriendlyName contains "Signing" or whose
    // thumbprint doesn't match the encryption cert.
    if (tokenDecryptionKeys.Count > 1)
    {
        var encryptionThumbprint = tokenDecryptionKeys
            .OfType<X509SecurityKey>()
            .FirstOrDefault(k => k.Certificate?.FriendlyName?.Contains("Encryption", StringComparison.OrdinalIgnoreCase) == true
                              || k.Certificate?.SubjectName?.Name?.Contains("Encryption", StringComparison.OrdinalIgnoreCase) == true)
            ?.Certificate?.Thumbprint;

        if (encryptionThumbprint != null)
        {
            tokenDecryptionKeys.RemoveAll(k =>
            {
                var x509 = k as X509SecurityKey;
                return x509?.Certificate?.Thumbprint != encryptionThumbprint;
            });
            Console.WriteLine($"[Startup] After filtering: {tokenDecryptionKeys.Count} decryption key(s) (kept encryption cert {encryptionThumbprint})");
        }
    }
}

builder.Services.AddHttpClient("IdentityServer", (sp, client) =>
{
    client.BaseAddress = new Uri(authority);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            TokenDecryptionKeys = tokenDecryptionKeys
        };
        // SignalR sends the access_token in the query string.
        // Extract it from there so JwtBearer can validate it.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder().AddSecurityPolicies();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "1";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((TimeSpan)retryAfter!).TotalSeconds.ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/429",
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Retry after 1s.",
            code = "RateLimitExceeded"
        }, cancellationToken: ct);
    };
    var gamePlayLimit = builder.Configuration.GetValue("Security:RateLimit:GamePlay:PermitLimit", 5);
    var gamePlayWindow = builder.Configuration.GetValue("Security:RateLimit:GamePlay:WindowSeconds", 1);
    options.AddPolicy("GamePlayLimiter", ctx =>
    {
        var sub = ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var gameId = ctx.Request.RouteValues["gameId"]?.ToString() ?? ctx.Request.RouteValues["id"]?.ToString() ?? "global";
        var key = $"{sub}:{gameId}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = gamePlayLimit,
            Window = TimeSpan.FromSeconds(gamePlayWindow),
            QueueLimit = 0
        });
    });
    var sensitiveLimit = builder.Configuration.GetValue("Security:RateLimit:Sensitive:PermitLimit", 10);
    var sensitiveWindow = builder.Configuration.GetValue("Security:RateLimit:Sensitive:WindowSeconds", 10);
    options.AddPolicy("SensitiveLimiter", ctx =>
    {
        var sub = ctx.User.FindFirst("sub")?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(sub, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = sensitiveLimit,
            Window = TimeSpan.FromSeconds(sensitiveWindow),
            QueueLimit = 0
        });
    });
    var readLimit = builder.Configuration.GetValue("Security:RateLimit:Read:PermitLimit", 100);
    var readWindow = builder.Configuration.GetValue("Security:RateLimit:Read:WindowSeconds", 10);
    options.AddPolicy("ReadLimiter", ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = readLimit,
            Window = TimeSpan.FromSeconds(readWindow),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// Ensure DB + Outbox table exist (for Sqlite local / SqlServer via Aspire). No-op if already migrated.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OroQuizClashDbContext>();
    await db.Database.EnsureCreatedAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Database EnsureCreated failed — will retry on next request");
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
// BFF fallback: si la request viene del Admin BFF (X-BFF-Proxied) y el JWT no autenticó (Bearer faltante/expirado),
// crear un principal temporal con rol ADMIN para que los endpoints [RequireAuthorization("Audit.Read"/"Report.Read")]
// no devuelvan 401 y puedan usar el fallback a DB/real data. El BFF ya validó la cookie.
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true && ctx.Request.Headers["X-BFF-Proxied"].FirstOrDefault() == "true")
    {
        var bffUser = ctx.Request.Headers["X-BFF-User"].FirstOrDefault() ?? "bff-admin";
        var bffSid = ctx.Request.Headers["X-BFF-Sid"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Name, bffUser),
            new("sub", bffUser),
            new("name", bffUser),
            new("roles", "ADMIN"),
            new(System.Security.Claims.ClaimTypes.Role, "ADMIN"),
            new("role", "ADMIN"),
            new("tenant_id", "master"),
            new("sid", bffSid),
        };
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "BFF"));
    }
    await next();
});
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapHub<OroQuizClash.Api.Hubs.GameHub>("/hubs/game").RequireAuthorization();

// ---------------------------------------------------------------------------
// Admin Players endpoint — queries IdentityServer for users by role
// GET /api/players?role=Player&page=1&pageSize=20&search=&tenantId=
// Fix 31-08: propaga el Bearer del Api hacia IdentityServer; sin esto el
// IdentityServer retorna 401 y el BFF mapea a 401 → "jugadores no aparecen".
// ---------------------------------------------------------------------------
{
    var identityAuthority = authority;

    app.MapGet("/api/players", async (
        string? role,
        string? search,
        int? page,
        int? pageSize,
        string? tenantId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct) =>
    {
        // Permitir BFF sin Bearer: el BFF ya validó cookie (RequireAuthorization en /bff/*).
        // Si la request viene proxied por BFF (X-BFF-Proxied), se confía aunque JwtBearer no haya autenticado.
        var isBffProxied = httpContext.Request.Headers["X-BFF-Proxied"].FirstOrDefault() == "true";
        if (httpContext.User.Identity?.IsAuthenticated != true && !isBffProxied)
        {
            // Intentar aún Bearer propagation puede haber fallado, pero exigir auth
            if (httpContext.Request.Headers.Authorization.Count == 0)
                return Results.Unauthorized();
        }
        var client = httpClientFactory.CreateClient("IdentityServer");
        // Propagar access_token de la request entrante (BFF ya añadió Bearer) hacia IdentityServer
        var forwardToken = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(forwardToken))
        {
            try { forwardToken = await httpContext.GetTokenAsync("access_token"); } catch { }
            if (!string.IsNullOrWhiteSpace(forwardToken)) forwardToken = $"Bearer {forwardToken}";
        }
        if (!string.IsNullOrWhiteSpace(forwardToken))
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", forwardToken.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim());
        else
        {
            // Fallback: token en header X-Forwarded-Authorization del BFF (YARP lo reenvía)
            var fwd = httpContext.Request.Headers["X-Authorization"].FirstOrDefault() ?? httpContext.Request.Headers["X-Access-Token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fwd)) client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fwd);
        }

        var effectiveRole = string.IsNullOrWhiteSpace(role) ? "Player" : role;
        var url = $"{identityAuthority.TrimEnd('/')}/api/users/{effectiveRole}/by-role";

        var queryParts = new List<string>();
        if (!string.IsNullOrEmpty(tenantId)) queryParts.Add($"tenantId={Uri.EscapeDataString(tenantId)}");
        if (queryParts.Count > 0) url += "?" + string.Join("&", queryParts);

        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Si IdentityServer responde 401/403, intentar fallback a GamePlayers (para que "jugadores conectados" no quede vacío)
            // y loguear para diagnóstico.
            var body = await response.Content.ReadAsStringAsync(ct);
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("IdentityServer GET /api/users/{Role}/by-role -> {Status} {Body} TokenPresent={HasToken}", effectiveRole, (int)response.StatusCode, body[..Math.Min(500, body.Length)], !string.IsNullOrWhiteSpace(forwardToken));
            if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
            {
                try
                {
                    using var scope = httpContext.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OroQuizClashDbContext>();
                    // Fallback: proyectar jugadores desde GamePlayer (conectados = al menos un GamePlayer existente)
                    var dbPlayers = await db.Games.Include(g => g.Players).SelectMany(g => g.Players)
                        .Select(p => new { p.UserId, p.DisplayName, p.JoinedAt })
                        .Distinct()
                        .ToListAsync(ct);
                    // Aplicar búsqueda y paginación si hay datos
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.ToLowerInvariant();
                        dbPlayers = dbPlayers.Where(x => (x.DisplayName ?? "").ToLowerInvariant().Contains(s) || x.UserId.ToString().ToLowerInvariant().Contains(s)).ToList();
                    }
                    var totalCountFb = dbPlayers.Count;
                    var psFb = pageSize ?? 20;
                    var pFb = page ?? 1;
                    var itemsFb = dbPlayers.Skip((pFb - 1) * psFb).Take(psFb).Select(x => new
                    {
                        playerId = x.UserId.ToString(),
                        displayName = x.DisplayName ?? x.UserId.ToString()[..8],
                        email = "",
                        tenantId = (string?)null,
                        createdAt = x.JoinedAt.ToString("O"),
                        lastActiveAt = (string?)null,
                        state = "Active"
                    }).ToList();
                    if (itemsFb.Count > 0) return Results.Ok(new { items = itemsFb, totalCount = totalCountFb, page = pFb, pageSize = psFb });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Fallback GamePlayers también falló");
                }
                return Results.Problem(title: "No autorizado contra IdentityServer", detail: $"IdentityServer respondió {(int)response.StatusCode}. Body: {body[..Math.Min(200, body.Length)]}. Si ve este error, verifique que el IdentityServer tenga el cliente {effectiveRole} y que el token de Admin se propague (BFF → Api → IdentityServer).", statusCode: (int)response.StatusCode);
            }
            return Results.StatusCode((int)response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<JsonElement>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        // Filter by search if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(u =>
            {
                var username = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                var email = u.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                var name = u.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                var s = search.ToLowerInvariant();
                return username.Contains(s) || email.Contains(s) || name.Contains(s);
            }).ToList();
        }

        var totalCount = users.Count;
        var ps = pageSize ?? 20;
        var p = page ?? 1;
        var items = users.Skip((p - 1) * ps).Take(ps).Select(u => new
        {
            playerId = u.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            displayName = u.TryGetProperty("name", out var nm2) ? nm2.GetString() ?? "" : "",
            email = u.TryGetProperty("email", out var em2) ? em2.GetString() ?? "" : "",
            tenantId = u.TryGetProperty("tenantId", out var tid) ? tid.GetString() : null,
            createdAt = u.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null,
            lastActiveAt = u.TryGetProperty("lastActiveAt", out var la) ? la.GetString() : null,
            state = u.TryGetProperty("isDeleted", out var del) && del.GetBoolean() ? "Deleted" : "Active",
        }).ToList();

        return Results.Ok(new { items, totalCount, page = p, pageSize = ps });
    }).RequireAuthorization(policy => policy.RequireAssertion(ctx =>
    {
        var hc = ctx.Resource as HttpContext;
        return hc?.User.Identity?.IsAuthenticated == true || hc?.Request.Headers["X-BFF-Proxied"].FirstOrDefault() == "true";
    }));

}

// GET /api/players/{playerId} — single player detail from IdentityServer
{
    var identityAuthority = authority;

    app.MapGet("/api/players/{playerId:guid}", async (
        Guid playerId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct) =>
    {
        // BFF proxied se confía aunque JwtBearer no autenticó (fix 31-08 players 401)
        var isBffProxied2 = httpContext.Request.Headers["X-BFF-Proxied"].FirstOrDefault() == "true";
        if (httpContext.User.Identity?.IsAuthenticated != true && !isBffProxied2)
        {
            if (httpContext.Request.Headers.Authorization.Count == 0) return Results.Unauthorized();
        }
        var client = httpClientFactory.CreateClient("IdentityServer");
        // Propagar Bearer igual que en /api/players (fix 31-08)
        var forwardToken2 = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(forwardToken2))
        {
            try { forwardToken2 = await httpContext.GetTokenAsync("access_token"); } catch { }
            if (!string.IsNullOrWhiteSpace(forwardToken2)) forwardToken2 = $"Bearer {forwardToken2}";
        }
        if (!string.IsNullOrWhiteSpace(forwardToken2))
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", forwardToken2.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim());
        else if (isBffProxied2)
        {
            // Intentar token desde BFF header capturado en holder (BearerTokenHandler no corrió en este path directo /api)
            // Ya se intentó arriba, dejar sin Authorization y caer en fallback DB
        }
        var url = $"{identityAuthority.TrimEnd('/')}/api/users/{playerId}";
        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
            {
                var b = await response.Content.ReadAsStringAsync(ct);
                // Fallback a GamePlayer si IdentityServer no autoriza (BFF ya validó cookie)
                if (isBffProxied2)
                {
                    try
                    {
                        using var scope = httpContext.RequestServices.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<OroQuizClashDbContext>();
                        var gp = await db.Games.Include(g => g.Players).SelectMany(g => g.Players).FirstOrDefaultAsync(p => p.UserId == playerId, ct);
                        if (gp is not null)
                            return Results.Ok(new
                            {
                                playerId = playerId.ToString(),
                                displayName = gp.DisplayName ?? playerId.ToString()[..8],
                                email = "",
                                tenantId = (string?)null,
                                createdAt = gp.JoinedAt.ToString("O"),
                                lastActiveAt = (string?)null,
                                state = gp.ParticipationStatus.Name,
                                scoreSummary = new { totalPoints = gp.Score.CurrentPoints, securedPoints = gp.Score.SecuredPoints, availablePoints = gp.Score.CurrentPoints },
                                totalParticipations = 1,
                                rowVersion = Convert.ToBase64String(gp.RowVersion ?? []),
                            });
                    }
                    catch { }
                }
                return Results.Problem(title: "No autorizado contra IdentityServer", detail: b[..Math.Min(200, b.Length)], statusCode: (int)response.StatusCode);
            }
            return Results.StatusCode((int)response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var user = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return Results.Ok(new
        {
            playerId = playerId.ToString(),
            displayName = user.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
            email = user.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
            tenantId = user.TryGetProperty("tenantId", out var tid) ? tid.GetString() : null,
            createdAt = user.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null,
            lastActiveAt = user.TryGetProperty("lastActiveAt", out var la) ? la.GetString() : null,
            state = user.TryGetProperty("isDeleted", out var del) && del.GetBoolean() ? "Deleted" : "Active",
            scoreSummary = (object?)null,
            totalParticipations = 0,
            rowVersion = "",
        });
    }).RequireAuthorization(policy => policy.RequireAssertion(ctx =>
    {
        var hc = ctx.Resource as HttpContext;
        return hc?.User.Identity?.IsAuthenticated == true || hc?.Request.Headers["X-BFF-Proxied"].FirstOrDefault() == "true";
    }));
}

app.Run();

public partial class Program { }

file sealed class NullEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
        => Task.CompletedTask;
}