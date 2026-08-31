# 05 — Guía de Instalación — OroQuizClash / QuizArena

> **Stacks:** `net10.0` `C#12` + Aspire 9 + Angular 22 + Blazor .NET 10 + Podman + SQL Server + PostgreSQL + Redis + RabbitMQ  
> **Orquestación:** `OroQuizClash.AppHost` (`Aspire 9`) — única fuente de verdad del grafo (`AppHost.cs:1-216`)  
> **Fecha:** 31-08-2026 — Constitución v1.1.0 — Specs 001–036

---

## 1. Requisitos

### 1.1 Software base (workstation desarrollo)

| Herramienta | Versión mínima / nota | Verificación |
|-------------|-----------------------|--------------|
| **.NET SDK** | `10.0.400` (`global.json:2` `rollForward latestFeature` `allowPrerelease`) | `dotnet --version` → `10.0.400`+ |
| **Aspire Workload** | 9.x (resiliente) | `dotnet workload install aspire` / `dotnet workload update` |
| **Aspire CLI** | `aspire` (para `aspire start/stop/ps/logs/dashboard`) | `aspire --version` |
| **Node.js** | `22` LTS (Angular 22) — `nvm` recomendado | `node --version` → `v22.x` |
| **pnpm** | `9`+ (Player usa `pnpm --frozen-lockfile`) — o `npm 10` con `--legacy-peer-deps` | `pnpm --version` / `corepack enable && corepack prepare pnpm@latest --activate` |
| **Podman** | `4.9+` (o Docker con compat `podman` — pero `oroidentityserver` se buildea con `podman build`) | `podman --version` / `podman machine start` si macOS/WSL |
| **Angular CLI** | `22` (`npm i -g @angular/cli@22` o `pnpm dlx`) | `ng version` |
| **Git / curl / openssl** | `openssl rand -base64 32` para secretos | `which openssl` |
| **Opcional** | `sqlcmd`/`mssql-cli`, `pgAdmin` (vía Aspire: `postgres` `WithPgAdmin`), `RabbitMQ Management` plugin (Aspire lo levanta) | |

> **Plataforma CI:** Linux/macOS/WSL2. En Windows nativo usar `Podman Desktop` o `Docker Desktop` + `podman machine`.

### 1.2 Hardware sugerido

- 8 GB RAM libre (sqlserver+postgres+redis+rabbitmq+identity+api+admin+player = ~3–4 GB).
- 10 GB disco para volúmenes Aspire + imágenes Podman.

### 1.3 Puertos (dev por Aspire)

| Recurso | Endpoint Aspire | Puerto host | Protocolo |
|---------|-----------------|-------------|-----------|
| `sqlserver` | `sqlserver` | efímero (Aspire proxy) | TDS |
| `postgres` | `postgres` → `PgAdmin` | efímero | postgres/pgAdmin web |
| `redis` | `redis` | efímero | RESP |
| `rabbitmq` | `rabbitmq` + management | efímero (+15672) | AMQP/HTTP |
| `identity-api` | `http` / `https` | `5080` / `5086` | HTTP/HTTPS |
| `oroclash-api` | `http` | `5000` (Aspire proxy) + Aspire asignado | HTTP |
| `quizarena-admin` | `http` / `https` | `5008` / `7172` | HTTP/HTTPS |
| `quizarena-player` | `http` | `4200` | HTTP (`ng serve --host 0.0.0.0`) |
| Aspire Dashboard |  | `17113` | HTTP (https://localhost:17113) |

> Los puertos efímeros se ven con `aspire ps`. Los fijos (`5080/5086/4200/7172`) están en `AppHost.cs:67-175`.

---

## 2. Instalación rápida (happy path — 15 min)

### 2.1 Clonar y restaurar

```bash
git clone <repo-url> OroQuizClash && cd OroQuizClash
# Verificar SDK
dotnet --version   # 10.0.400
dotnet workload install aspire
# Restaurar .NET
dotnet restore OroQuizClash.slnx
dotnet build OroQuizClash.slnx  # 0 errors, 15 warnings esperadas
# Restaurar Player
cd src/Player/QuizArena.Player
corepack enable
pnpm install --frozen-lockfile
cp src/environments/environment.example.ts src/environments/environment.ts
# environment.ts ya viene con apiUrl=http://localhost:5000/api (dev) y identityAuthority=http://localhost:5080
cd ../../..
```

### 2.2 Construir imagen `oroidentityserver` (obligatorio, una vez por actualización)

`OroIdentityServer` es un contenedor Podman que **no** se compila vía `dotnet run`; se debe buildear desde su Dockerfile antes de `aspire start`.

```bash
podman build -f src/IdentityServer/IdentityServer/Dockerfile -t localhost/oroidentityserver:latest .
podman images | grep oroidentityserver  # verificar
```

> Sin este `build`, `identity-api` fallará con `image not found`.

### 2.3 Generar secretos de desarrollo

`AppHost.cs:43-44` declara 3 parámetros secretos (Aspire los pide si no existen, pero es más cómodo exportarlos):

```bash
export symmetric_security_key="$(openssl rand -base64 32)"   # ≥32 bytes, compartida entre instancias (SymmetricSecurityKey)
export seed_admin_password="Admin@123456"                   # seed del superadmin OroIdentityServer (SEED_ADMIN_PASSWORD)
# El secreto del cliente quizarena-admin se genera al registrar el cliente (paso 2.5)
```

Alternativa Aspire: `dotnet user-secrets` o `aspire start --parameter symmetric-security-key=...`.

> En `aspire start` sin exportar, Aspire pedirá interactivamente los parámetros `symmetric-security-key` y `seed-admin-password`.

### 2.4 Levantar toda la solución (`aspire start`)

```bash
aspire start
# Equivalente: dotnet run --project OroQuizClash.AppHost
# Esperar hasta que el Dashboard (https://localhost:17113) muestre todos los recursos en verde:
#  sqlserver, postgres (pgAdmin), redis, rabbitmq (management), identity-api, oroclash-api, oroclash-seeder (completed), quizarena-player, quizarena-admin
```

**Verificaciones tras `aspire start`:**

```bash
aspire ps
# NAME               STATE    ENDPOINTS
# sqlserver          Running  sqlserver://...
# postgres           Running  postgres://...
# redis              Running  redis://...
# rabbitmq           Running  amqp://... , http://...:15672 (management)
# identity-api       Running  http://localhost:5080 https://localhost:5086
# oroclash-api       Running  http://localhost:5000
# quizarena-player   Running  http://localhost:4200
# quizarena-admin    Running  http://localhost:5008 https://localhost:7172
# oroclash-seeder    Completed (one-shot)

curl -sk http://localhost:5000/health | jq         # oroclash-api health
curl -sk http://localhost:5080/.well-known/openid-configuration | jq .jwks_uri  # → https://localhost:5086/.well-known/jwks
curl -sk https://localhost:7172/health | jq       # admin health (ignorar cert dev)
curl -sk http://localhost:4200 | head             # player (ng serve proxy)
aspire logs oroclash-seeder  # debe mostrar "Seeder completo: 10 categorías, 200 preguntas, 10 juegos (10 WAITING_FOR_PLAYERS)"
```

**Health URLs:**

- `http://localhost:5000/health` y `/alive` (API)
- `https://localhost:7172/health` (Admin) — usar `curl -k`
- `http://localhost:5080/health` o `/.well-known/openid-configuration` (Identity)

### 2.5 Registrar cliente `quizarena-admin` (OIDC confidential — una sola vez)

El seed del IdentityServer crea `admin/Admin@123456` pero **no** crea el cliente `quizarena-admin` (lo hace el script contra la API runtime).

```bash
# Generar secreto aleatorio para el cliente
export ADMIN_CLIENT_SECRET="$(openssl rand -hex 32)"
echo "ADMIN_CLIENT_SECRET=$ADMIN_CLIENT_SECRET"  # guardarlo para el siguiente paso

# Opción A: script (requiere que identity-api esté Running y admin password exportado)
export IDP_URL="https://localhost:5086"
export IDP_ADMIN_PASSWORD="${seed_admin_password:-Admin@123456}"
export ADMIN_REDIRECT_URI="https://localhost:7172/signin-oidc"
export ADMIN_POST_LOGOUT_URI="https://localhost:7172/signout-callback-oidc"
./scripts/register-admin-oidc-client.sh
# El script imprime: "Store this secret as quizarena-admin-oidc-secret"

# Opción B: re-ejecutar aspire start con el parámetro
aspire stop
export symmetric_security_key="$symmetric_security_key"
export seed_admin_password="$seed_admin_password"
aspire start --parameter quizarena-admin-oidc-secret="$ADMIN_CLIENT_SECRET"
# O: dotnet user-secrets set "Parameters:quizarena-admin-oidc-secret" "$ADMIN_CLIENT_SECRET" --project OroQuizClash.AppHost
```

> El `AppHost.cs:116-130` pasa `Identity__ClientSecret = quizarena-admin-oidc-secret` y `Identity__ApiScope=admin` a `quizarena-admin`. Sin este registro, el login de Admin redirigirá a error `invalid_client`.

### 2.6 Registrar cliente `quizarena-player` (SPA público — una sola vez)

El Player es `Public` + PKCE; se registra **vía UI de OroIdentityServer** (no hay script).

1. Abrir `https://localhost:5086` (o `http://localhost:5080`) → Login `admin / Admin@123456`.
2. `Applications` → `Create`:
   - `clientId`: `quizarena-player`
   - `displayName`: `QuizArena Player (PKCE)`
   - `clientType`: `public`
   - `applicationType`: `web`
   - `consentType`: `implicit`
   - `permissions`: `ept:authorization`, `ept:token`, `gt:authorization_code`, `rst:code`, `scp:openid`, `scp:profile`, `scp:email`, `scp:offline_access` (añadir `scp:roles` si usará roles player)
   - `requirements`: `ft:pkce`
   - `redirectUris`: `http://localhost:4200/auth/callback` (añadir `https://localhost:4200/auth/callback` si se usará https, y en prod `https://<dominio>/auth/callback`)
   - `postLogoutRedirectUris`: `http://localhost:4200/auth/logout-callback` (imprescindible para que el botón "Cerrar sesión" del fix 31-08 funcione; sin esto el logout server-side falla y el fallback local es el que cierra sesión — ver nota en `auth.service.ts`)
3. Guardar.

> **Nota logout (fix 31-08):** `AuthService.logout()` (`Player/.../core/auth/auth.service.ts`) intenta `logoffAndRevokeTokens()` y si el IdP no tiene `postLogoutRedirectUri` registrado hace fallback a `logoffLocal()` + `window.location.href=/auth/logout-callback`. Registrar ambas `postLogoutRedirectUris` evita el salto visible al fallback pero la app ya funciona sin él.

### 2.7 Crear usuarios de prueba

Vía `https://localhost:5086` → `Users` → `Create`:

- **ADMIN de ejemplo:** `username=alice` `email=alice@oroclash.local` `password=Player@123` `roles=[ADMIN]` o `GAME_MANAGER`+`REWARD_MANAGER`.
- **PLAYER de ejemplo:** `username=bob` `email=bob@player.local` `password=Player@123` `role=PLAYER`. Repetir para multijugador (`carol`, `dave`...).

> `must_change_password` gating: al primer login el usuario con flag `must_change_password` es redirigido a `Account/ChangePassword` hasta cambiarla.

### 2.8 Verificar flujos end-to-end

**Admin:**

- Abrir `https://localhost:7172` → `Login` → OIDC `admin/Admin@123456` → Dashboard.
- `Categories` → verificar 10 `Active` (Biología Celular...). `Question Bank` → 200 `Published`.
- `Games` → 10 `WAITING_FOR_PLAYERS` (Torneo ...). Crear uno nuevo (`Open Lobby → Start`) o usar los sembrados.
- `Live /admin/live/{gameId}` → `Start Round` → ver `RoundStarted` en dashboard.

**Player:**

- Abrir `http://localhost:4200` → `Iniciar sesión` → PKCE login como `bob` → `Lobby` → `Available Games` 8 cols + `Join`.
- `Game` → `Ronda 3/10` + Timer + Pregunta (4 radios) → responder → `Score Panel Potential` + Leaderboard.
- `Retiro` → diálogo 3 métricas → confirmar → `WITHDRAWN`.
- `Rewards` → `AvailablePoints` + catálogo 4 métricas → `Detalle` → `Canje 2 pasos` → `Historial`.

---

## 3. Instalación alternativa — sin Aspire (manual)

Si `aspire start` no está disponible, cada servicio se puede levantar manualmente:

```bash
# Pre-requisito: build oroidentityserver
podman build -f src/IdentityServer/IdentityServer/Dockerfile -t localhost/oroidentityserver:latest .

# 1) Infra: sqlserver, postgres, redis, rabbitmq (via podman compose)
podman run -d --name oroclash-sqlserver -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong!Passw0rd' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
podman run -d --name oroclash-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16
podman run -d --name oroclash-redis -p 6379:6379 redis:7
podman run -d --name oroclash-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# 2) OroIdentityServer
podman run -d --name identity-api -p 5080:5080 -p 5086:5086 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=identitydb;Username=postgres;Password=postgres" \
  -e SymmetricSecurityKey="$(openssl rand -base64 32)" \
  -e SEED_ADMIN_PASSWORD='Admin@123456' \
  localhost/oroidentityserver:latest

# 3) OroQuizClash.Api (sqlserver fallback:connection string)
export ConnectionStrings__oroclash="Server=localhost,1433;Database=oroclash;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
export Identity__Authority="http://localhost:5080"
dotnet run --project src/OroQuizClash.Api --urls "http://localhost:5000"

# 4) Seeder
dotnet run --project src/Seeder/OroQuizClash.Seeder

# 5) Admin
export Oidc__Authority="https://localhost:5086"  # o http://localhost:5080 si no hay cert
export Oidc__ClientId="quizarena-admin"
export Identity__ClientSecret="$ADMIN_CLIENT_SECRET"
dotnet run --project src/Admin/QuizArena.Admin --urls "https://localhost:7172;http://localhost:5008"

# 6) Player
cd src/Player/QuizArena.Player
cp src/environments/environment.example.ts src/environments/environment.ts
# editar environment.ts: apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080
pnpm exec ng serve --host 0.0.0.0 --port 4200
```

---

## 4. Configuración del Sistema (orden recomendado)

| Paso | Acción | Ubicación / Comando |
|------|--------|---------------------|
| 1 | Registrar `quizarena-player` OIDC (una vez) | `https://localhost:5086` → Applications → Create (`client_id=quizarena-player`, `Public`, PKCE, `redirect=http://localhost:4200/auth/callback`, `postLogout=http://localhost:4200/auth/logout-callback`, scopes `openid profile email offline_access`) |
| 2 | Registrar `quizarena-admin` OIDC (una vez) | `./scripts/register-admin-oidc-client.sh` con `IDP_ADMIN_PASSWORD` |
| 3 | Crear usuarios jugadores | Admin UI → Users → Create → rol `PLAYER` (repetir) |
| 4 | Verificar categorías sembradas | Admin UI → Categories → 10 `Active` (si no, revisitar Seeder logs) |
| 5 | Crear/activar categorías nuevas (si aplica) | `Categories → Create → Publish` (requiere ≥5 preguntas `Published`) |
| 6 | Crear preguntas | `Question Bank → Create` (4 opciones, 1 correcta) → `Publish` → repetir ≥5 por categoría para activar juegos |
| 7 | Crear juegos | `Games → Create` (16 atributos `GameConfiguration`) → `Open Lobby` → `Start` |

> **Juegos sembrados:** 10 juegos `WAITING_FOR_PLAYERS` ya disponibles tras `aspire start` sin pasos 4-7.

---

## 5. Tests & calidad

```bash
# .NET — 864+ passed
dotnet build OroQuizClash.slnx                    # 0 errors net10.0
dotnet test                                       # Domain 272 + Application 131 + Arch 79 + Api 113 + Admin 269 + Infra 27
dotnet test --filter Rewards                      # foco recompensas
dotnet test --filter "Lobby|Player|Rewards"       # foco player

# Player SPA
cd src/Player/QuizArena.Player
npm test -- --watch=false                         # Vitest 3 + Testing Library jsdom (8 specs rewards/* + withdrawal/rounds)
npx ng lint                                       # eslint 9 + @ngrx/eslint-plugin

# Design system
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/  # 0 literals
dotnet test --filter Architecture                 # DesignSystemNoDirectDbTests
```

---

## 6. Troubleshooting

| Síntoma | Causa probable | Solución |
|---------|----------------|----------|
| `aspire dashboard` no abre | `dotnet workload` desactualizado | `dotnet workload update` + `aspire update` |
| `identity-api` crash `image not found` | No se buildeó `oroidentityserver` | `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t localhost/oroidentityserver:latest .` |
| `identity-api` crash `SymmetricSecurityKey` | Falta `symmetric_security_key` | `export symmetric_security_key="$(openssl rand -base64 32)"` y reiniciar `aspire start` |
| `oroclash-api` `SqlException` `Login failed for sa` | Volumen `sqlserver` corrupto o password cambiada | `podman volume rm oroclash-sqlserver-data` + `aspire start` |
| `quizarena-admin` 401 `invalid_client` | `quizarena-admin` no registrado / secret mismatch | Re-ejecutar `register-admin-oidc-client.sh` con `ADMIN_CLIENT_SECRET` + `aspire start --parameter ...` |
| `quizarena-admin` redirección infinita | Cert `https` no confiable en navegador | `curl -k https://localhost:5086/.well-known/openid-configuration` debe responder; confiar cert dev de Aspire (`dotnet dev-certs https --trust`) |
| `quizarena-player` "Cerrar sesión" no hace nada | `postLogoutRedirectUris` no registrado en IdP | Registrar `http://localhost:4200/auth/logout-callback` en `quizarena-player` (Admin UI Applications). El fix 31-08 hace fallback local así que tras el fix el botón ya cierra sesión aunque falte el registro, pero el registrado es lo normativo |
| `quizarena-player` `ng serve` `ERR_PNPM_ABORTED` | `CI` no exportado | `export CI=true` (o `CI=true pnpm install --frozen-lockfile`); alternativa `builder.AddContainer node:22-alpine` en `AppHost.cs:183` |
| `Reward creation` `500` `NullReferenceException` `description.Trim` | Bug previo al fix 31-08 cuando `Description` venía `null` | Fix aplicado en `Domain/Rewards/Reward.cs:43` (`(desc??"").Trim()`) + `CreateRewardRequest` compat V2. Reconstruir: `dotnet build` |
| `Reward creation` `422 PointsRequired must be >0` | Cliente enviaba `Cost` pero Api esperaba `PointsRequired` | Fix aplicado en `Features/Rewards/CreateReward.cs:66` (fusiona `Cost→PointsRequired`). Reconstruir Api |
| `Reward creation` `400 Type invalid` | Validación `IsDefined(Type)` en `RewardForm` fallaba con `null` | Client ya envía `ToApi(Type)`; server ignora `Type` (no es parte del agregado legado). No requiere acción |
| `Player` "No autenticado" tras login | CORS/mixed-content `http` vs `https` Authority | `environment.identityAuthority` debe coincidir con `Identity__Authority` del Api y con discovery `https://localhost:5086`. En dev con Aspire usar `http://localhost:5080` para ambos si el cert https no está instalado |
| `Seeder` "Se falló EnsureCreated reintento 5/5" | `sqlserver` aún iniciando | Esperar 10s y `podman logs oroclash-sqlserver`; `aspire start` reintenta automáticamente. `postman` alternativa: lanzar seeder manual `dotnet run --project src/Seeder/OroQuizClash.Seeder` |

---

## 7. Deployment (`aspire publish`)

```bash
# Docker Compose (local prod-like)
aspire publish --output-path publish/compose
# Editar publish/compose/docker-compose.yaml para ajustar secrets/connection strings
docker compose -f publish/compose/docker-compose.yaml up --build -d

# Kubernetes / Azure Container Apps
aspire publish --publisher manifest  # genera manifest.json
# Azure Container Apps
az login
aspire deploy --publisher azure  # o seguir guía `aspire deployment` skill
```

El `AppHost` en `IsPublishMode` usa `AddDockerfile("quizarena-player", ".", "src/Player/QuizArena.Player/Dockerfile")` (multi-stage `node build → nginx`) y `WithHttpsCertificateConfiguration` del Identity pasa a referencia `ASPNETCORE_Kestrel__Certificates`.

---

## 8. Referencias

- **Manual de Usuario:** `draft/manual-de-usuario.md` — roles, OIDC completo, categorías→preguntas→juegos, flujo jugador, troubleshooting.
- **Specs:** `specs/017-admin-application/contracts/oidc-config.md` (registro OIDC admin), `specs/027-player-application/` (Player OIDC PKCE)
- **AppHost:** `OroQuizClash.AppHost/AppHost.cs:1-216`
- **Seeder:** `src/Seeder/OroQuizClash.Seeder/{Worker.cs,SeedData.cs}`
- **Scripts:** `scripts/register-admin-oidc-client.sh`
- **Design system tokens:** `design-system/tokens/design-tokens.css` (validar con `validate-tokens.cjs`)
- **ADRs:** `docs/adr/ADR-013-admin-bff-communication.md` (BFF YARP)

*Guía vigente a 31-08-2026 (36 specs, fixes 31-08 Rewards+Logout incluidos). Para actualizar, editar `entregables/05-Guia-de-Instalacion.md` y versionar.*
