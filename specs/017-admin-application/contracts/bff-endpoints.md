# Contract: BFF Endpoints (QuizArena.Admin server)

**Patrón**: sample oficial `BlazorWebAppOidcBffAutoYarpAspire` — el servidor Blazor es el único origen que habla el navegador; el servidor reenvía al API con el token del operador.

## 1. Forwarder REST catch-all

```
Ruta expuesta : /bff/{**catch-all}
Destino       : http://oroclash-api  (Aspire service discovery)
Rewrite       : /bff/{rest} → /api/{rest}
Auth          : .RequireAuthorization() (sesión cookie válida)
Transform     : Authorization: Bearer {access_token}  (de la cookie OIDC, vía GetTokenAsync)
Métodos       : todos (GET/POST/PUT/DELETE) — transparentes
```

**Ejemplos de mapeo**:

| Cliente llama | Forwarder envía | Endpoint del API (SPEC) |
|---------------|-----------------|--------------------------|
| `GET /bff/games?status=Active&page=1` | `GET /api/games?...` | Games list (004) |
| `POST /bff/games` | `POST /api/games` | CreateGame (001) |
| `POST /bff/games/{id}/start` | `POST /api/games/{id}/start` | StartGame (004) |
| `GET /bff/categories` | `GET /api/categories` | Categories (002) |
| `POST /bff/categories/{id}/publish` | `POST /api/categories/{id}/publish` | Publish gate (002) |
| `GET /bff/questions` | `GET /api/questions` | Questions (003) |
| `GET /bff/redemptions` | `GET /api/redemptions` | Redemptions (009) |
| `GET /bff/reports/rewards` | `GET /api/reports/rewards` | Reporting (015) |
| `GET /bff/audit?actor=...` | `GET /api/audit?...` | Audit (014) |

**Comportamientos contractuales**:
- **401 del API** (token expirado pese a refresh) → el servidor responde 401; el cliente Blazor dispara re-autenticación (challenge OIDC).
- **403 del API** (rol insuficiente) → passthrough 403; la UI muestra denegación clara (FR-008 spec).
- **ProblemDetails (RFC 7807)** del API → passthrough del cuerpo; el cliente mapea a `ApiErrorView` (data-model §4).
- El BFF **no interpreta ni transforma** cuerpos: proxy transparente salvo path rewrite + header de autorización.
- El BFF **no expone** rutas que no existan en el API (404 del API = 404 al cliente).

## 2. Forwarder hub SignalR

```
Ruta expuesta : /hubs/game
Destino       : http://oroclash-api/hubs/game
Auth          : .RequireAuthorization()
Transform     : Authorization: Bearer {access_token}
Transporte    : WebSockets proxyados por HttpForwarder (negotiate + WS)
```

Detalle de eventos y política de reconexión: [realtime.md](realtime.md).

## 3. Endpoints de autenticación (propios del servidor)

```
GET/POST /authentication/login     → challenge OIDC (redirección a OroIdentityServer /connect/authorize)
GET/POST /authentication/logout    → sign-out local + OIDC (redirección a /Account/Logout del proveedor)
GET  /signin-oidc                  → callback OIDC (handler, no código propio)
GET  /signout-callback-oidc        → callback post-logout
GET  /signout-oidc                 → front-channel logout del proveedor
```

Implementación: `MapGroup("/authentication").MapLoginAndLogout()` (del sample). **Ningún formulario de credenciales propio** (Constitución VI).

## 4. Endpoints de infraestructura

- `/health`, `/alive` — BuildingBlocks.ServiceDefaults (sin auth).
- Estáticos: `design-tokens.css`, fuentes, assets (sin auth).

## 5. Prohibiciones

- El BFF MUST NOT aceptar llamadas sin sesión (401 inmediato).
- El BFF MUST NOT cachear respuestas del API (datos operativos en vivo).
- El navegador MUST NOT conocer la URL del API (`http://oroclash-api` es interna del grafo Aspire).
