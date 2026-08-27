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
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Strategies;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Infrastructure.Categories;
using OroQuizClash.Infrastructure.Counters;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Selection;
using OroQuizClash.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddCqrs(c => c
    .RegisterHandlersFromAssemblyContaining<Program>()
    .RegisterHandlersFromAssembly(typeof(OroQuizClash.Application.Features.Games.CreateGameCommand).Assembly)
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

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
builder.Services.AddScoped<IQuestionSelectionStrategy, RandomQuestionSelectionStrategy>();
builder.Services.AddScoped<IDifficultyProgressionStrategy, LinearDifficultyStrategy>();

builder.Services.AddEndpoints(typeof(OroQuizClash.Application.Features.Games.CreateGameEndpoint).Assembly);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var authority = builder.Configuration["Identity:Authority"] ?? "http://identity:5080";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOrGameManager", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == "roles" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER")) ||
            ctx.User.HasClaim(c => c.Type == "role" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER")) ||
            ctx.User.IsInRole("ADMIN") || ctx.User.IsInRole("GAME_MANAGER")));

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
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapEndpoints();

app.Run();

public partial class Program { }

file sealed class NullEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
        => Task.CompletedTask;
}