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
            var cert = X509CertificateLoader.LoadPkcs12FromFile(pfx, string.Empty, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
            if (cert.GetRSAPrivateKey() is not null)
                tokenDecryptionKeys.Add(new X509SecurityKey(cert));
        }
        catch
        {
            // Ignore certs that cannot be loaded (e.g. unrelated or locked files).
        }
    }
}

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
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapHub<OroQuizClash.Api.Hubs.GameHub>("/hubs/game").RequireAuthorization();

app.Run();

public partial class Program { }

file sealed class NullEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
        => Task.CompletedTask;
}