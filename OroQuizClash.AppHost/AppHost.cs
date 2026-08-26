using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Infrastructure: bases de datos y mensajería
// - sqlserver/oroclash  → DB primaria OroQuizClash (SQL Server, rowversion)
// - postgres/identitydb  → DB aislada OroIdentityServer (PostgreSQL)
// - redis                → cache / rate-limiting futuro
// - rabbitmq             → Outbox → EventBus (BuildingBlocks.EventBus.RabbitMQ)
// Todo con lifetime persistente para que los datos sobrevivan a `aspire stop`.
// ---------------------------------------------------------------------------

var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("oroclash-sqlserver-data");

var oroclashDb = sqlServer.AddDatabase("oroclash");

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("oroclash-postgres-data")
    .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent));

var identityDb = postgres.AddDatabase("identitydb");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("oroclash-redis-data");

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("oroclash-rabbitmq-data")
    .WithManagementPlugin();

// ---------------------------------------------------------------------------
// Parámetros / secretos (solo local). En producción se inyectan vía
// `aspire deploy` / variables de entorno del host.
// DataProtection keyring sobrevive entre reinicios vía volumen.
// ---------------------------------------------------------------------------

var symmetricKey = builder.AddParameter("symmetric-security-key", secret: true);
var seedAdminPassword = builder.AddParameter("seed-admin-password", secret: true);

// ---------------------------------------------------------------------------
// OroIdentityServer — Podman container `oroidentityserver:latest`
// Es la ÚNICA autoridad de identidad (Principio VI, constitución v1.1.0).
// Build: podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .
// El container trae su propia migración + seed `admin`/`Admin@123456` y expone
// OIDC discovery en http://identity-server:5080/.well-known/openid-configuration
// OroQuizClash.Api lo consume como Authority para validar JWT (jwks_uri).
// ---------------------------------------------------------------------------

IResourceBuilder<ContainerResource> identityServer = builder.AddContainer("identity-api", "localhost/oroidentityserver", "latest")
    // Aspire's https endpoint uses transport=http: the proxy terminates TLS and forwards
    // plaintext HTTP to the container, so the app only needs plain HTTP listeners on 5080
    // and 5086. This annotation makes the proxy use the development certificate.
    .WithHttpsCertificateConfiguration(ctx =>
    {
        ctx.Arguments.Add("--https-certificate-path");
        ctx.Arguments.Add(ctx.PfxPath);
        ctx.EnvironmentVariables.Add("ASPNETCORE_Kestrel__Certificates__Default__Path", ctx.PfxPath);
        ctx.EnvironmentVariables.Add("ASPNETCORE_Kestrel__Certificates__Default__Password", ctx.Password);
        return Task.CompletedTask;
    })
    .WithHttpEndpoint(port: 5080, targetPort: 5080, name: "http")
    .WithHttpsEndpoint(port: 5086, targetPort: 5086, name: "https")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    // DB aislada: el container debe apuntar a la postgres de Aspire
    .WithReference(identityDb).WaitFor(identityDb)
    // SymmetricSecurityKey ≥32 bytes — compartida por todas las instancias (requerido en prod)
    .WithEnvironment("SymmetricSecurityKey", symmetricKey)
    .WithEnvironment("SEED_TENANT_NAME", "OroMasterTenant")
    .WithEnvironment("SEED_ADMIN_USERNAME", "admin")
    .WithEnvironment("SEED_ADMIN_PASSWORD", seedAdminPassword)
    .WithEnvironment("SEED_ADMIN_EMAIL", "admin@oroclash.local")
    // RabbitMQ opcional (para IntegrationEvents del identity, no para game-state)
    .WithEnvironment("EventBus__RabbitMQ__HostName", rabbitMq.Resource.Name)
    .WithVolume("identity-dp-keys", "/app/data-protection-keys");

// ---------------------------------------------------------------------------
// OroQuizClash.Api — host principal (modular monolith)
// - EF Core: oroclash (SQL Server si Aspire lo provee, fallback Sqlite en Program.cs)
// - Outbox → RabbitMQ
// - OIDC JWT: valida contra identity-server discovery (jwks_uri)
// - Endpoints: IEndpoint slices (CreateGame, StartGame, etc.)
// - OTel + health (/health, /alive) via BuildingBlocks.ServiceDefaults
// ---------------------------------------------------------------------------

var api = builder.AddProject<Projects.OroQuizClash_Api>("oroclash-api")
    .WithReference(oroclashDb).WaitFor(oroclashDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(redis).WaitFor(redis)
    .WaitFor(identityServer)
    .WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http"))
    .WithHttpHealthCheck("/health");

// ---------------------------------------------------------------------------
// Notas para `aspire start` / `aspire deploy`:
// - Local:  `aspire start` levanta sqlserver + postgres (pgAdmin) + redis + rabbitmq (management)
//           + identity-server (5080/5086) + oroclash-api. Dashboard en https://localhost:17113
// - Para que identity-server arranque sin parámetros secretos, define en tu environment:
//     export symmetric_security_key="$(openssl rand -base64 32)"
//     export seed_admin_password="Admin@123456"
//   o pasa `--parameter` al CLI de Aspire.
// - Integraciones futuras (OroQuizClash.Web, workers) se añaden aquí con
//   `builder.AddProject<Projects.OroQuizClash_Web>("oroclash-web")...`
//   y `WithReference(api)` + `WithBrowserLogs()` si aplica.
// - Deployment: `aspire publish` genera artefactos para Docker Compose / AKS /
//   Azure Container Apps; el AppHost es la única fuente de verdad del grafo.
// ---------------------------------------------------------------------------

// Soporte para testing: Aspire.Hosting.Testing (WebApplicationFactory) puede
// crear un DistributedApplicationTestingBuilder a partir de este AppHost.

if (builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
{
    // En dev, asegurar que la DB se cree aunque no haya migraciones iniciales.
    // Las migraciones reales se añaden con `dotnet ef migrations add Initial --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api`
}

builder.Build().Run();
