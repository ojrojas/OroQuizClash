# OroIdentityServer

OroIdentityServer is an identity and authentication management system built on **.NET 10** and **ASP.NET Core**, implementing Domain-Driven Design (DDD) and Clean Architecture. It exposes an OAuth2 / OpenID Connect server via **OpenIddict**, a Blazor-based admin UI, a REST admin API, and an event-driven backbone over **RabbitMQ**.

## Key Features

### 1. Authentication and Authorization
- OAuth2 / OpenID Connect via OpenIddict 8 (authorization code, client credentials, password and refresh token flows)
- JWT issuance and validation, token revocation and authorization termination
- Cookie-based admin sign-in with shared DataProtection keyring
- Custom login/logout endpoints (`/auth/login`, `/auth/logout`, `/auth/change-password`)
- Login form rejects invalid credentials in place and shows an error, instead of silently redirecting
- Forced password change on first login for every user except the seeded admin account, enforced via a `must_change_password` claim and a redirect middleware that locks the UI to `/Account/ChangePassword` until cleared
- Relying-party-initiated logout (`~/connect/logout`) shows an IdentityServer-owned confirmation page (`/Account/Logout`) before ending the session, so signing out of a client app doesn't silently sign the admin out of IdentityServer itself

### 2. Blazor Admin UI
- FluentUI Blazor admin panel (list, detail/edit, create dialog, delete-with-confirmation) for Users, Roles, Applications, Scopes and Identification Types
- Sessions page to inspect and forcibly disconnect a user's active sessions
- Dashboard with live counts (users, connected users, roles, applications, scopes)
- Dark/light theme toggle (`FluentDesignTheme`) plus toast (`IToastService`) and dialog (`IDialogService`) providers for feedback and confirmations
- Runs under Blazor **Auto** render mode: components always call the `/api/*` endpoints over `HttpClient` (`IAdminXxxService`/`AdminXxxService` in `IdentityServer.Client`), whether rendered server-side on first paint or later from WebAssembly; the endpoints themselves delegate to CQRS-backed `ServerAdminXxxService` implementations (`IdentityServer/Services`) that talk to the dispatcher directly — no HTTP hop, no duplicated business logic between the two

### 3. Localization
- 8 supported languages: English, Spanish (LatAm), French, Italian, German, Portuguese (Brazil), Japanese, Chinese (Simplified) — `SharedResources.*.resx` under `IdentityServer.Client/Resources`
- Default culture resolves from the browser's `Accept-Language` header (which reflects the OS locale on Windows, Linux and macOS); an explicit choice from the language picker overrides it via a persisted `.AspNetCore.Culture` cookie
- Culture stays in sync between the server-rendered first paint and the WebAssembly runtime that takes over afterward (`/culture/set` endpoint + a small JS interop bootstrap in the client's `Program.cs`)

### 4. User, Role, Permission and Tenant Management
- Full CRUD for users, roles, permissions, tenants, identification types, applications and scopes
- Role/permission-based authorization for the admin API
- Multi-tenant support: tenant activation/suspension and user-to-tenant assignment
- User session and login-session tracking, with session termination revoking OpenIddict tokens/authorizations

### 5. Domain-Driven Design
- Aggregate roots, entities and value objects in `BuildingBlocks.Kernel`
- `Result`/`Error` pattern instead of exceptions for expected failures
- Business rules and domain events, dispatched via `DomainEventDispatcher`
- Repository + specification pattern for querying aggregates

### 6. CQRS — no MediatR
- Hand-rolled `ICommand`/`IQuery` abstractions and dispatchers in `BuildingBlocks.CQRS`
- Pipeline behaviors for logging and FluentValidation-based request validation

### 7. Event-Driven Integration
- `BuildingBlocks.EventBus` defines integration events and an in-memory subscription registry
- `BuildingBlocks.EventBus.RabbitMQ` provides a RabbitMQ-backed `IEventBus` implementation with Polly-based retry

### 8. Modern .NET Stack
- .NET 10 / ASP.NET Core minimal APIs
- Entity Framework Core with PostgreSQL (Npgsql)
- Serilog structured logging (console, Seq)
- OpenTelemetry instrumentation and Quartz-backed OpenIddict cleanup jobs
- FluentUI Blazor components for the built-in admin UI (Interactive Server + WebAssembly render modes)

### 9. Local Orchestration with .NET Aspire
- `examples/AppHost` wires up Postgres (+ pgAdmin), Redis, RabbitMQ and the identity server for local development
- `examples/Frontends/oroidentity-admin` is a sample Angular admin frontend, run through Aspire's Node/pnpm integration

## Project Structure

```
OroIdentityServer/
├── src/
│   ├── Core/                                   # Domain models, aggregates, interfaces (OroIdentityServer.Core)
│   ├── Application/                             # CQRS commands/queries and handlers, module extensions
│   ├── Infraestructure/                          # EF Core DbContext, migrations, repositories, specifications
│   ├── IdentityServer/
│   │   ├── IdentityServer/                       # Host: minimal API endpoints, OpenIddict, Blazor Server admin UI
│   │   │   ├── Endpoints/                        # /api/* minimal API groups, delegate to Services/ServerAdminXxxService
│   │   │   ├── Services/                         # ServerAdminXxxService: CQRS-backed IAdminXxxService implementations
│   │   │   └── Components/Accounts/Pages/        # Login, Logout (confirmation), ChangePassword (static SSR pages)
│   │   └── IdentityServer.Client/                # Blazor WebAssembly client (Auto render mode)
│   │       ├── Interfaces/, Services/            # IAdminXxxService + HTTP-based AdminXxxService (used by components)
│   │       ├── Models/                           # Client-facing request/response DTOs, per admin domain
│   │       ├── Pages/, Components/               # Admin CRUD pages, create dialogs, Dashboard, Sessions
│   │       └── Resources/                        # SharedResources.*.resx — localization (8 languages)
│   ├── Shared/
│   │   └── OroIdentityServer.Shared/             # Shared contracts across host/client
│   └── BuildingBlocks/
│       ├── BuildingBlocks.Kernel/                # DDD primitives: Entity, AggregateRoot, ValueObject, Result/Error
│       ├── BuildingBlocks.CQRS/                  # Custom command/query dispatchers and pipeline behaviors
│       ├── BuildingBlocks.EventBus/               # Integration event contracts and subscription registry
│       ├── BuildingBlocks.EventBus.RabbitMQ/       # RabbitMQ transport for the event bus
│       ├── BuildingBlocks.Logger/                 # Serilog configuration and logging extensions
│       └── BuildingBlocks.ServicesDefaults/        # Shared service registration helpers
├── examples/
│   ├── AppHost/                                  # .NET Aspire orchestration (Postgres, Redis, RabbitMQ, admin UI)
│   ├── BlazorServerSessionExample/               # Blazor Server session example
│   ├── Frontends/oroidentity-admin/               # Sample Angular admin frontend
│   └── NodeJsApiExample/                         # Minimal Node.js API example
├── tests/
│   ├── Server.Tests/                             # Endpoint + authorization integration tests (WebApplicationFactory)
│   └── BuildingBlocks.*.UnitTests/                # Unit/integration tests per building block
├── data-protection-keys/                         # Shared DataProtection keyring for local development
├── docker-compose.yaml                           # PostgreSQL + Identity Server containers
├── README.md
├── LICENCE
└── OroIdentityServer.slnx
```

## Technologies Used

- **.NET 10.0 / ASP.NET Core** — minimal APIs and Blazor (Interactive Server + WebAssembly)
- **OpenIddict 8** — OAuth2 / OpenID Connect server, validation and Quartz-based token cleanup
- **Entity Framework Core + Npgsql** — PostgreSQL persistence
- **FluentValidation** — request validation pipeline behavior
- **RabbitMQ.Client + Polly** — integration event bus with retry policies
- **Redis** — provisioned via Aspire for local development
- **.NET Aspire** — distributed application orchestration (`examples/AppHost`)
- **Serilog** — structured logging (console, Seq sink)
- **OpenTelemetry** — tracing/metrics instrumentation
- **Microsoft.FluentUI.AspNetCore.Components** — admin UI components (dark theme, toasts, dialogs, data grids)
- **Microsoft.Extensions.Localization** — RESX-based localization, 8 languages, satellite assemblies for WASM
- **Scalar.AspNetCore / Microsoft.OpenApi** — OpenAPI documentation
- **xUnit, NSubstitute, Testcontainers, EF Core InMemory** — test stack

## Domain-Driven Design Implementation

### Aggregate Roots
- `User`, `Role`, `Permission`, `Tenant`, `UserSession`, `IdentificationType` and related aggregates, one module per bounded context under `src/Core/Modules`

### Value Objects & Kernel Primitives
- `Entity`, `AggregateRoot`, `ValueObject`, `BusinessRule` in `BuildingBlocks.Kernel`
- `Result`/`Error` types for explicit, exception-free failure handling
- `IAuditableEntity` for created/modified auditing

### Repositories & Specifications
- Generic `IRepository<T>` and `ISpecification<T>` in the kernel
- Concrete repositories per aggregate under `src/Infraestructure/Repositories`

### CQRS (custom, no MediatR)
- `ICommand` / `IQuery` abstractions with dedicated `CommandDispatcher` / `QueryDispatcher`
- `LoggingBehavior` and `ValidationBehavior` pipeline behaviors

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Docker or Podman (for PostgreSQL, RabbitMQ, Redis)
- Node.js + pnpm (only if running the sample Angular admin frontend via Aspire)

### Local Development Setup (recommended: Aspire)

1. **Clone the repository:**
   ```bash
   git clone https://github.com/ojrojas/OroIdentityServer.git
   cd OroIdentityServer
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Run the AppHost:**
   ```bash
   dotnet run --project examples/AppHost/AppHost.csproj
   ```
   This provisions:
   - PostgreSQL (with pgAdmin) and a persistent data volume
   - RabbitMQ (persistent container lifetime)
   - Redis
   - The identity server (`identity-api`)
   - The sample Angular admin frontend (`identity-admin`, via pnpm)

4. **Access the applications** (ports are assigned by Aspire; check the dashboard for the current run):
   - **Aspire Dashboard:** printed in the console output when the AppHost starts (`https://localhost:17113` by default)
   - **Identity Server:** proxied through Aspire, or directly at the host's launch profile port
   - **Angular Admin Frontend:** proxied through Aspire on port `30645`

### Manual Setup (without Aspire)

1. **Start PostgreSQL:**
   ```bash
   docker compose up -d
   ```
   (uses `docker-compose.yaml`, database `identitydb` on port `5432`)

2. **Run database migrations:**
   ```bash
   dotnet ef database update --project src/Infraestructure/OroIdentityServer.Infraestructure.csproj --startup-project src/IdentityServer/IdentityServer/IdentityServer.csproj
   ```

3. **Run the identity server:**
   ```bash
   dotnet run --project src/IdentityServer/IdentityServer/IdentityServer.csproj
   ```
   Available at `http://localhost:5080` / `https://localhost:7114` (see `Properties/launchSettings.json`).

   > Note: without Aspire, RabbitMQ connection settings must be supplied manually if the event bus is enabled; otherwise disable/skip the RabbitMQ registration for local runs.

## Containerized Deployment

The Docker image is defined by `src/IdentityServer/IdentityServer/Dockerfile`. It exposes all
runtime settings as environment variables, so the same image can be used by anyone — on a laptop,
on a server, or in a cluster. The image is based on the slim `mcr.microsoft.com/dotnet/aspnet:10.0`
runtime (no SDK) and runs as a non-root user.

### Building the image

```bash
podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .
```

### Running standalone (with an external PostgreSQL)

```bash
podman run --rm -p 5080:5080 \
  -e ConnectionStrings__identitydb="Host=db;Port=5432;Database=identitydb;Username=postgres;Password=yourpassword" \
  -e SymmetricSecurityKey="$(openssl rand -base64 32)" \
  -e SEED_ADMIN_USERNAME="admin" \
  -e SEED_ADMIN_PASSWORD="Admin@123456" \
  oroidentityserver:latest
```

### Running with docker-compose

The provided `docker-compose.yaml` builds the image and wires it to a PostgreSQL container.
Migrations and the universal `admin` seed run automatically on first start:

```bash
podman compose up -d --build
# Open http://localhost:5080 and sign in as `admin` / `Admin@123456`
```

The compose file creates two services:

| Service | Container | Ports | Description |
|---------|-----------|-------|-------------|
| `db` | `postgres_db` | `5432` | PostgreSQL with health check (`pg_isready`) |
| `identity-server` | `identity_server` | `5080`, `5086` | The identity server (depends on `db`) |

A named volume `identity-dp-keys` persists ASP.NET Data Protection keys at `/app/data-protection-keys`
so tokens and cookies survive container restarts.

### Enabling HTTPS

The image always starts with both HTTP (port `5080`) and HTTPS (port `5086`) bound, but HTTPS
requires a certificate. Without one, only HTTP works. To enable HTTPS, mount a certificate and
override the Kestrel settings at run time (no application changes needed):

```bash
podman run --rm -p 5080:5080 -p 5086:5086 \
  -v "$PWD/certs/https.pfx:/app/certs/https.pfx:ro" \
  -e ASPNETCORE_URLS="http://+:5080;https://+:5086" \
  -e Kestrel__Certificates__Default__Path="/app/certs/https.pfx" \
  -e Kestrel__Certificates__Default__Password="changeit" \
  -e ConnectionStrings__identitydb="Host=db;Port=5432;Database=identitydb;Username=postgres;Password=yourpassword" \
  -e SEED_ADMIN_USERNAME="admin" \
  -e SEED_ADMIN_PASSWORD="Admin@123456" \
  oroidentityserver:latest
```

For a PEM certificate instead of a PFX, set `Kestrel__Certificates__Default__KeyPath` to the
private key file.

## Integration — Using the Image in Other Projects

Any application that needs OAuth2 / OpenID Connect authentication can point at a running
OroIdentityServer instance. The image is self-contained: build it, run it, and configure your
client to use its well-known discovery endpoints.

### 1. Discover the OIDC metadata

Once the server is running, the OpenID Connect discovery document is available at:

```
GET http://<host>:5080/.well-known/openid-configuration
```

This returns the full metadata including `authorization_endpoint`, `token_endpoint`,
`userinfo_endpoint`, `jwks_uri`, supported scopes, grant types, and signing algorithms.
Most OIDC libraries can consume this URL automatically.

### 2. Register your client application

Before your app can authenticate, register it as an OpenIddict client. This can be done
via the admin API or the Blazor admin UI:

**Via the API:**
```bash
curl -X POST http://<host>:5080/api/applications \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "my-app",
    "clientSecret": "my-secret",
    "displayName": "My Application",
    "consentType": "Implicit",
    "grantTypes": ["authorization_code", "refresh_token"],
    "redirectUris": ["http://localhost:3000/callback"],
    "permissions": ["openid", "profile", "email", "offline_access"]
  }'
```

**Via the admin UI:** navigate to `/` (the Blazor admin panel), go to Applications, and
create a new entry with the same parameters.

### 3. Configure your application

#### .NET / ASP.NET Core (using OpenIddict client)

```csharp
builder.Services.AddAuthentication(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
    .AddOpenIddict(options =>
    {
        options.AddClient()
            .UseAuthorizationCodeFlow()
            .AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey()
            .UseAspNetCore()
            .EnableRedirectionEndpointPassthrough()
            .EnableTokenEndpointPassthrough();

        options.SetIssuer("http://localhost:5080");
    });
```

#### Angular / Node.js / Python / any OIDC library

Use the discovery endpoint directly:

```javascript
// Example: using oidc-client-ts (JavaScript/TypeScript)
import { UserManagerSettings } from 'oidc-client-ts';

const settings: UserManagerSettings = {
  authority: 'http://localhost:5080',
  client_id: 'my-app',
  redirect_uri: 'http://localhost:3000/callback',
  response_type: 'code',
  scope: 'openid profile email',
  post_logout_redirect_uri: 'http://localhost:3000',
};

const userManager = new UserManager(settings);
```

```python
# Example: using Authlib (Python)
from authlib.integrations.flask_client import OAuth

oauth = OAuth()
oauth.register(
    'identity',
    server_metadata_url='http://localhost:5080/.well-known/openid-configuration',
    client_id='my-app',
    client_secret='my-secret',
    request_token_params={'scope': 'openid profile email'},
)
```

### 4. Supported flows

| Flow | Grant Type | Use Case |
|------|-----------|----------|
| Authorization Code | `authorization_code` | Web apps (recommended). Redirects user to `/connect/authorize`, exchanges code at `/connect/token`. |
| Client Credentials | `client_credentials` | Machine-to-machine. No user interaction. Token from `/connect/token` directly. |
| Password | `password` | Legacy / first-party apps. Username + password exchanged at `/connect/token`. |
| Refresh Token | `refresh_token` | Obtain a new access token without re-authentication. |

### 5. Key endpoints for clients

| Endpoint | URL | Purpose |
|----------|-----|---------|
| Discovery | `GET /.well-known/openid-configuration` | OIDC metadata (auto-configures most libraries) |
| Authorize | `GET/POST /connect/authorize` | Authorization code / consent flow |
| Token | `POST /connect/token` | Exchange code or credentials for tokens |
| UserInfo | `GET /connect/userinfo` | Retrieve user claims (requires bearer token) |
| Introspect | `POST /connect/introspect` | Validate a token's validity and metadata |
| Revoke | `POST /connect/revoke` | Revoke an access or refresh token |
| End Session | `GET/POST /connect/logout` | Initiate single logout |

## Configuration

Key configuration files:
- `src/IdentityServer/IdentityServer/appsettings.json` / `appsettings.Development.json` — logging, OpenIddict, DB connection
- `Directory.Build.props` — shared build properties
- `Directory.Packages.props` — centralized (central package management) NuGet versions
- `Data/seedData.json` (under the host project) — seed data for users, roles, applications and scopes on first run; controlled by the `DatabaseSeeder:Skip` setting. The universal bootstrap admin is the `admin` account (role **Administrator**, username `admin`, default password `Admin@123456`), which is exempt from the forced first-login password change; every other user (seeded or created later) must change their password on first sign-in. The admin identity can be customized through the `SEED_ADMIN_*` environment variables listed below.

### Environment variables

The Dockerfile declares sensible defaults for every variable; each one can be overridden with
`-e` (Podman/Docker) or in the compose file:

| Variable | Default | Purpose |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:5080;https://+:5086` | URLs Kestrel binds to. Override to `http://+:5080` (HTTP-only) or keep both with a certificate for HTTPS |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET environment |
| `Kestrel__Certificates__Default__Path` | *(unset)* | PFX (or PEM cert) path used for the HTTPS endpoint |
| `Kestrel__Certificates__Default__Password` | *(unset)* | Password of the PFX certificate |
| `Kestrel__Certificates__Default__KeyPath` | *(unset)* | Private key path for a PEM certificate |
| `ConnectionStrings__identitydb` | `Host=db;...` | PostgreSQL connection string |
| `SymmetricSecurityKey` | dev key | Base64 OpenIddict signing/encryption key (>= 32 bytes) — **must be overridden in production** and shared by all instances |
| `IDENTITY_ADMIN_HTTP` | `http://localhost:4200` | Base URL of the external admin SPA, used when seeding the OpenIddict `Admin` client |
| `DatabaseSeeder__Skip` | `false` | Set `true` to skip the seeder on startup |
| `SEED_TENANT_NAME` | `OroMasterRealm` | Tenant name created during seeding |
| `SEED_ADMIN_USERNAME` | `admin` | Username of the bootstrap admin |
| `SEED_ADMIN_PASSWORD` | `Admin@123456` | Password of the bootstrap admin |
| `SEED_ADMIN_EMAIL` | `admin@example.com` | Email of the bootstrap admin |
| `SEED_ADMIN_NAME` | `Admin` | First name of the bootstrap admin |
| `SEED_ADMIN_LASTNAME` | `Administrator` | Last name of the bootstrap admin |
| `SEED_ADMIN_IDENTIFICATION` | `000000001` | Identification number of the bootstrap admin |
| `SEED_ADMIN_ROLE` | `Administrator` | Role granted to the bootstrap admin |
| `SEED_ADMIN_FORCE_PASSWORD_CHANGE` | `false` | `true` forces the admin to change its password on first login |
| `EventBus__RabbitMQ__HostName` | `localhost` | RabbitMQ host (optional) |
| `EventBus__RabbitMQ__Port` | `5672` | RabbitMQ port (optional) |
| `EventBus__RabbitMQ__UserName` | `guest` | RabbitMQ user (optional) |
| `EventBus__RabbitMQ__Password` | `guest` | RabbitMQ password (optional) |
| `EventBus__RabbitMQ__VirtualHost` | `/` | RabbitMQ virtual host (optional) |

> A seed file mounted at `/app/Data/seedData.json` overrides the one baked into the image.

## API Endpoints

### Authorization Policies

Admin API endpoints are protected by role-based authorization policies:

| Policy | Where Applied | Meaning |
|--------|--------------|---------|
| `ManagerOrAdmin` | `/api` root group | Authenticated user with Manager or Admin role |
| `AdminOnly` | `/api/roles`, `/api/permissions` | Admin role required |
| `MasterAdminOnly` | `/api/applications`, `/api/scopes`, `/api/tenants` (full CRUD) | Requires `is_master_admin` claim |
| `[Authorize]` (default) | `/api/dashboard/stats` | Any authenticated user |

### OpenIddict Connect Endpoints

These are the OAuth2 / OpenID Connect protocol endpoints:

| Method | Route | Description |
|--------|-------|-------------|
| `GET/POST` | `/connect/authorize` | Authorization endpoint. Triggers consent flow for external clients, issues authorization codes. |
| `POST` | `/connect/token` | Token exchange. Handles `authorization_code`, `refresh_token`, `client_credentials`, and `password` grants. |
| `GET/POST` | `/connect/logout` | End-session. Redirects to `/Account/Logout` for confirmation before signing out. |
| `GET` | `/connect/userinfo` | Returns user claims (subject, email, name, roles, tenant_id). Requires a valid bearer token. |
| `POST` | `/connect/introspect` | Token introspection. Validates a token's validity and metadata. |
| `POST` | `/connect/revoke` | Token revocation. Invalidates an access or refresh token. |

### Auth — `/auth`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `/auth/login` | Admin sign-in via form data (`LoginIdentifier`, `Password`). Creates session cookie and `UserSession` record. Redirects to `ReturnUrl` or `/`. | No |
| `GET/POST` | `/auth/logout` | Admin sign-out. Deactivates the `UserSession`, revokes all OpenIddict tokens/authorizations. | No |
| `POST` | `/auth/change-password` | Changes the authenticated user's password. Body: `NewPassword`, `ConfirmPassword`. | Yes |

### Users — `/api/users` (ManagerOrAdmin)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/users` | List all users |
| `GET` | `/api/users/{id}` | Get user by ID |
| `POST` | `/api/users` | Create a new user |
| `PUT` | `/api/users/{id}` | Update a user |
| `DELETE` | `/api/users/{id}` | Delete a user |
| `PUT` | `/api/users/{id}/roles` | Assign roles to a user |
| `POST` | `/api/users/{id}/lock` | Lock a user account |
| `POST` | `/api/users/{id}/unlock` | Unlock a user account |
| `GET` | `/api/users/{role}/by-role` | Get users by role name. Query param: `tenantId` (optional) |

### Roles — `/api/roles` (AdminOnly)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/roles` | List all roles |
| `GET` | `/api/roles/{id}` | Get role by ID |
| `POST` | `/api/roles` | Create a role |
| `PUT` | `/api/roles/{id}` | Update a role |
| `DELETE` | `/api/roles/{id}` | Delete a role |

### Permissions — `/api/permissions` (AdminOnly)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/permissions` | List all permissions |
| `GET` | `/api/permissions/{id}` | Get permission by ID |
| `POST` | `/api/permissions` | Create a permission |
| `PUT` | `/api/permissions/{id}` | Update a permission |
| `DELETE` | `/api/permissions/{id}` | Delete a permission |

### Tenants — `/api/tenants`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `/api/tenants/mine` | Tenants accessible to the current user | ManagerOrAdmin |
| `GET` | `/api/tenants/by-user/{userId}` | Tenants for a specific user | ManagerOrAdmin |
| `GET` | `/api/tenants` | List all tenants | MasterAdminOnly |
| `GET` | `/api/tenants/{id}` | Get tenant by ID | MasterAdminOnly |
| `POST` | `/api/tenants` | Create a tenant | MasterAdminOnly |
| `PUT` | `/api/tenants/{id}` | Update a tenant | MasterAdminOnly |
| `POST` | `/api/tenants/{id}/activate` | Activate a tenant | MasterAdminOnly |
| `POST` | `/api/tenants/{id}/suspend` | Suspend a tenant | MasterAdminOnly |
| `POST` | `/api/tenants/{id}/users` | Add a user to a tenant | MasterAdminOnly |

### Applications (OpenIddict clients) — `/api/applications` (MasterAdminOnly)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/applications` | List all OIDC client applications |
| `GET` | `/api/applications/{clientId}` | Get application by client ID |
| `POST` | `/api/applications` | Create an OIDC application |
| `PUT` | `/api/applications/{clientId}` | Update an OIDC application |
| `DELETE` | `/api/applications/{clientId}` | Delete an OIDC application |

### Scopes — `/api/scopes` (MasterAdminOnly)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/scopes` | List all OIDC scopes |
| `POST` | `/api/scopes` | Create a scope |
| `PUT` | `/api/scopes/{name}` | Update a scope by name |
| `DELETE` | `/api/scopes/{name}` | Delete a scope by name |

### Identification Types — `/api/identification-types` (ManagerOrAdmin)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/identification-types` | List all identification types |
| `GET` | `/api/identification-types/{id}` | Get identification type by ID |
| `POST` | `/api/identification-types` | Create an identification type |
| `PUT` | `/api/identification-types/{id}` | Update an identification type |
| `DELETE` | `/api/identification-types/{id}` | Delete an identification type |

### User Sessions — `/api/user-sessions` (ManagerOrAdmin)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/user-sessions/by-user/{userId}` | Get sessions for a specific user |
| `GET` | `/api/user-sessions/active` | Get all active sessions |
| `GET` | `/api/user-sessions/active-count` | Get count of active sessions |
| `POST` | `/api/user-sessions` | Create a user session |
| `POST` | `/api/user-sessions/{id}/deactivate` | Deactivate a specific session |
| `POST` | `/api/user-sessions/terminate-all/{userId}` | Terminate all sessions for a user |

### Sessions — `/api/sessions` (ManagerOrAdmin)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/sessions/by-user/{userId}` | Get sessions for a user (admin session model) |

### Validation Logs — `/api/validation-logs` (ManagerOrAdmin)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/validation-logs/daily-summary` | Daily validation log summary. Query: `days` (default: 7) |
| `GET` | `/api/validation-logs/recent` | Recent validation log entries. Query: `take` (default: 6) |

### Dashboard — `/api/dashboard` (any authenticated user)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/dashboard/stats` | Dashboard statistics (user counts, session counts, etc.) |

### Utility

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/culture/set` | Sets the `.AspNetCore.Culture` cookie and redirects. Query: `culture` (required), `redirectUri` (optional) |

### Blazor UI Pages (static SSR)

| Route | Purpose |
|-------|---------|
| `/Account/Login` | Login page (renders form that POSTs to `/auth/login`) |
| `/Account/ChangePassword` | Must-change-password page (renders form that POSTs to `/auth/change-password`) |
| `/Account/Consent` | OIDC consent page (renders form that POSTs to `/connect/authorize`) |
| `/Account/Logout` | Logout confirmation page |
| `/Account/AccessDenied` | Access denied page |

## Testing

- `tests/Server.Tests` — integration tests against the host via `WebApplicationFactory`, covering auth endpoints and admin API authorization
- `tests/BuildingBlocks.Kernel.UnitTests` — kernel primitives (entities, results, business rules)
- `tests/BuildingBlocks.CQRS.UnitTests` — dispatcher and pipeline behavior tests
- `tests/BuildingBlocks.EventBus.UnitTests` — subscription registry tests
- `tests/BuildingBlocks.EventBus.RabbitMQ.IntegrationTests` — RabbitMQ event bus tests (Testcontainers)
- `tests/BuildingBlocks.Logger.UnitTests`, `tests/BuildingBlocks.ServicesDefaults.UnitTests`

Run all tests:
```bash
dotnet test
```

## License

Licensed under the GNU AGPL v3.0. See [LICENCE](./LICENCE) for details.
