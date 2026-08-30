using Microsoft.Extensions.Hosting;
using System.IO;

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

// Persist the OpenIddict dev encryption/signing certificate store so the
// generated cert is STABLE across restarts. The API reads the same cert from
// this host directory to DECRYPT the (JWE) access tokens the identity server
// issues (the API's JwtBearer handler cannot validate encrypted tokens).
var oidcCertDir = Path.Combine(builder.AppHostDirectory, ".oidc-certs");
Directory.CreateDirectory(oidcCertDir);
identityServer.WithBindMount(oidcCertDir, "/home/app/.dotnet/corefx/cryptography/x509stores");

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
    .WithEnvironment("Identity__TokenDecryptionCertificateDirectory", oidcCertDir)
    .WithHttpHealthCheck("/health");

// ---------------------------------------------------------------------------
// QuizArena.Admin — Blazor Web App Auto (BFF, SPEC-017)
// - Único origen del navegador: forwarder YARP /bff/{**} → oroclash-api /api/{**}
//   adjuntando el access_token de la cookie OIDC (tokens nunca en el navegador).
// - OIDC authorization_code + refresh_token contra identity-api (Principio VI).
// - Sin acceso a base de datos (FR-030): todo dato llega vía BFF.
// ---------------------------------------------------------------------------

var adminOidcSecret = builder.AddParameter("quizarena-admin-oidc-secret", secret: true);

var admin = builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin")
    .WithReference(api).WaitFor(api)
    .WithReference(redis).WaitFor(redis)
    .WithHttpEndpoint(port: 5008, targetPort: 5008, name: "http", isProxied: false)
    .WithHttpsEndpoint(port: 7172, targetPort: 7172, name: "https", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("Oidc__Authority", identityServer.GetEndpoint("https"))
    .WithEnvironment("Oidc__ClientId", "quizarena-admin")
    .WithEnvironment("Api__BaseUrl", api.GetEndpoint("http"))
    .WithEnvironment("Identity__Authority", identityServer.GetEndpoint("https"))
    .WithEnvironment("Identity__ClientSecret", adminOidcSecret)
    .WithEnvironment("Identity__ApiScope", "admin")
    .WithHttpHealthCheck("/health");

// ---------------------------------------------------------------------------
// OroQuizClash.Seeder — Datos de prueba secundaria (10 categorías ×20 preguntas + 10 juegos WAITING_FOR_PLAYERS)
// Idempotente: valida si ya existen datos antes de sembrar; se ejecuta al inicio y termina (one-shot).
// Requiere borrar volumen mssql para resiembra limpia: podman volume rm oroclash-sqlserver-data
// ---------------------------------------------------------------------------

builder.AddProject<Projects.OroQuizClash_Seeder>("oroclash-seeder")
    .WithReference(oroclashDb).WaitFor(oroclashDb)
    .WaitFor(api)
    .WaitFor(identityServer);

// ---------------------------------------------------------------------------
// QuizArena.Player — Angular 22 SPA (SPEC-027)
// - PKCE public SPA (angular-auth-oidc-client) contra identity-api
// - Proxy /api → oroclash-api, /hubs → oroclash-api SignalR
// - En dev: Podman container node:22-alpine con bind-mount + ng serve (hot reload)
//   idéntico a identity-api (localhost/oroidentityserver) pero en modo dev.
// - En publish: Dockerfile multi-stage (node build → nginx) en src/Player/QuizArena.Player/Dockerfile
//   con context en raíz del repo (podman build -f src/Player/QuizArena.Player/Dockerfile -t localhost/quizarena-player:latest .)
// ---------------------------------------------------------------------------

if (builder.ExecutionContext.IsPublishMode)
{
    // Producción / `aspire publish`: build via Dockerfile (nginx sirve dist/quizarena-player/browser)
    // Equivalente Podman a identity-server: podman build -f src/Player/QuizArena.Player/Dockerfile -t localhost/quizarena-player:latest .
    builder.AddDockerfile("quizarena-player", ".", "src/Player/QuizArena.Player/Dockerfile")
        .WithHttpEndpoint(targetPort: 80, name: "http")
        .WithExternalHttpEndpoints()
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WithEnvironment("IDENTITY_AUTHORITY", identityServer.GetEndpoint("https"))
        .WithEnvironment("PORT", "80");
}
else
{
    // Dev / `aspire run`: host directo con pnpm + ng serve (más rápido, HMR).
    // Fix del bug original: path debe ser "../src/Player/QuizArena.Player" (relativo a AppHost), no "src/...".
    // Si tu entorno no tiene node/pnpm local, usa la alternativa Podman de abajo.
    builder.AddJavaScriptApp("quizarena-player", "../src/Player/QuizArena.Player", "start")
        .WithPnpm(installArgs: ["--frozen-lockfile"])
        .WithHttpEndpoint(port: 4200, targetPort: 4200, name: "http", env: "PORT", isProxied: false)
        .WithExternalHttpEndpoints()
        .WithEnvironment("CI", "true")
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WithEnvironment("IDENTITY_AUTHORITY", identityServer.GetEndpoint("https"));

    // Alternativa Podman dev (como identity-server, si no tienes pnpm local):
    // Descomenta para levantar Angular dentro de contenedor node:22-alpine con hot-reload.
    // Requiere montar design-system y CI=true para evitar ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY.
    // builder.AddContainer("quizarena-player", "node", "22-alpine")
    //     .WithBindMount("../src/Player/QuizArena.Player", "/app")
    //     .WithBindMount("../design-system", "/design-system")
    //     .WithHttpEndpoint(targetPort: 4200, name: "http")
    //     .WithExternalHttpEndpoints()
    //     .WithEnvironment("CI", "true")
    //     .WithEnvironment("API_URL", api.GetEndpoint("http"))
    //     .WithEnvironment("IDENTITY_AUTHORITY", identityServer.GetEndpoint("http"))
    //     .WithEnvironment("PORT", "4200")
    //     .WithArgs("sh", "-c", "cd /app && corepack enable && pnpm install --frozen-lockfile && pnpm exec ng serve --host 0.0.0.0 --port 4200");
}

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