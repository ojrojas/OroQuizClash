using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Seeder;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var cs = builder.Configuration.GetConnectionString("oroclash");
if (string.IsNullOrWhiteSpace(cs)) cs = "Data Source=oroclash.db";

builder.Services.AddSingleton<IDomainEventDispatcher, NullDispatcher>();

builder.Services.AddDbContext<OroQuizClashDbContext>(o =>
{
    if (cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) && !cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        o.UseSqlServer(cs);
    else
        o.UseSqlite(cs);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
