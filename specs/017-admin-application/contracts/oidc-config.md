# Contract: OIDC Configuration (OroIdentityServer ↔ QuizArena.Admin)

**Autoridad**: OroIdentityServer (OpenIddict 8) — única fuente de identidad (Constitución VI). Discovery: `{Identity:Authority}/.well-known/openid-configuration`.

## 1. Registro del cliente en OroIdentityServer

| Campo | Valor |
|-------|-------|
| client_id | `quizarena-admin` |
| Tipo | Confidencial (client_secret) |
| Grants | `authorization_code` + `refresh_token` |
| Redirect URIs | `https://{admin-host}/signin-oidc` |
| Post-logout redirect | `https://{admin-host}/signout-callback-oidc` |
| Front-channel logout | `https://{admin-host}/signout-oidc` |
| Scopes | `openid`, `profile`, `offline_access`, `roles`, y scope(s) de recurso del API QuizArena |
| PKCE | Sí (S256) |

> El registro se crea/automatiza vía `/api/applications` de OroIdentityServer en la fase de tareas; el client_secret se almacena como secreto de Aspire/configuración (nunca en el repo).

## 2. Configuración del handler en el servidor Blazor

```
AddAuthentication().AddOpenIdConnect(oidc => { ... }).AddCookie(CookieDefaults)

oidc.SignInScheme            = CookieAuthenticationDefaults.AuthenticationScheme
oidc.Authority               = Configuration["Identity:Authority"]   // endpoint http de identity-api (Aspire)
oidc.ClientId                = "quizarena-admin"
oidc.ClientSecret            = Configuration["Identity:ClientSecret"]
oidc.ResponseType            = OpenIdConnectResponseType.Code
oidc.SaveTokens              = true                                  // access/refresh en la cookie
oidc.Scope                  += { "offline_access", "<api-scope>" }   // además de openid/profile
oidc.MapInboundClaims        = false
oidc.TokenValidationParameters.NameClaimType = "name"
oidc.TokenValidationParameters.RoleClaimType = "roles"
oidc.CallbackPath            = "/signin-oidc"          (default)
oidc.SignedOutCallbackPath   = "/signout-callback-oidc" (default)
oidc.RemoteSignOutPath       = "/signout-oidc"          (default)
```

## 3. Refresh no interactivo (`CookieOidcRefresher`)

- `ConfigureCookieOidc(cookieScheme, oidcScheme)` adjunta `OnValidatePrincipal`:
  - Si el `access_token` expiró → usa el `refresh_token` contra el token endpoint del discovery para obtener uno nuevo.
  - Reemite la cookie con el nuevo `access_token` guardado.
  - Si el refresh falla → sign-out del usuario (debe re-autenticarse).
- Requisito del proveedor: OpenIddict debe permitir el grant `refresh_token` para `quizarena-admin` (sí por defecto con offline_access).

## 4. Claims y serialización al cliente

- Claims nativos del JWT (sin mapeo SOAP): `sub`, `name`, `roles[]`, `must_change_password`, `tenant_id`.
- Servidor: `AddCascadingAuthenticationState()` + `AddAuthenticationStateSerialization(o => o.SerializeAllClaims = true)` + `PersistingAuthenticationStateProvider`.
- Cliente WASM: `AddAuthorizationCore()` + `AddCascadingAuthenticationState()` + `AddAuthenticationStateDeserialization()` + `PersistentAuthenticationStateProvider`.
- El cliente **solo recibe claims** (estado de autenticación); **nunca recibe tokens** (los tokens permanecen en la cookie del servidor — BFF).

## 5. Manejo de `must_change_password`

- Si el claim `must_change_password == true` → la app bloquea la navegación administrativa y redirige a `{Identity:Authority}/Account/ChangePassword` (con return URL).
- Tras el cambio, el flujo OIDC re-emite claims sin el flag.

## 6. Autorización en el servidor Blazor

- `AddAuthorization()` + políticas locales espejo de `SecurityPolicies` del API por rol:
  - `AdminOnly` → `roles ∋ ADMIN`
  - `AdminOrGameManager` → `roles ∋ {ADMIN, GAME_MANAGER}`
  - `RewardManagerOrAdmin` → `roles ∋ {ADMIN, REWARD_MANAGER}`
- Rutas: `[Authorize]` base; secciones con política específica (p. ej., Audit `AdminOnly`). La autoridad final sigue siendo el API (403).

## 7. Prohibiciones

- MUST NOT existir login/logout con credenciales propias.
- MUST NOT almacenarse tokens en el cliente WASM (localStorage/sessionStorage/memoria).
- MUST NOT duplicarse user store ni auditoría de identidad (Constitución H/I).
