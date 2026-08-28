# Contracts: Auth OIDC for Player (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28

## Identity Provider

- **OroIdentityServer** `oroidentityserver:latest` (Principio VI) — discovery `{{identity-authority}}/.well-known/openid-configuration` (dev `http://identity-api:5080`, prod `https://identity.example.com`).
- **Endpoints consumidos**: `authorization_endpoint` (`/connect/authorize`), `token_endpoint` (`/connect/token`), `userinfo_endpoint` (`/connect/userinfo`), `jwks_uri` (`/.well-known/jwks`), `end_session_endpoint` (`/connect/logout`), `revocation_endpoint` (`/connect/revoke`), `introspection` opcional. Auth UI: `/auth/login`, `/auth/change-password`, `/Account/Logout`.

## Angular PKCE (v1 base)

### Client registration (OroIdentityServer)

Via `POST /api/applications` o Blazor Admin UI:

```json
{
  "clientId": "quizarena-player",
  "displayName": "QuizArena Player",
  "clientType": "public",
  "redirectUris": ["http://localhost:4200/auth/callback", "https://player.oroclash.local/auth/callback"],
  "postLogoutRedirectUris": ["http://localhost:4200/auth/logout-callback", "https://player.oroclash.local/"],
  "permissions": ["authorization_code", "refresh_token"],
  "scopes": ["openid", "profile", "email", "offline_access", "api"],
  "requirePkce": true,
  "requireConsent": false
}
```
- `clientSecret` vacío (public). `offline_access` para `refresh_token`.

### Angular config (`angular-auth-oidc-client`)

```ts
// app.config.ts
import { provideAuth } from 'angular-auth-oidc-client';

export const authConfig = {
  config: {
    authority: environment.identityAuthority, // http://identity-api:5080
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/logout-callback',
    clientId: 'quizarena-player',
    scope: 'openid profile email offline_access api',
    responseType: 'code',
    silentRenew: true,
    useRefreshToken: true,
    renewTimeBeforeTokenExpiresInSeconds: 30,
    secureRoutes: [environment.apiUrl], // solo oroclash-api lleva Bearer
    maxIdTokenIatOffsetAllowedInSeconds: 600,
    customParamsAuthRequest: { prompt: 'login' },
  }
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, correlationIdInterceptor])),
    provideAuth(authConfig),
  ]
};
```

Alternative `oauth2-oidc`:

```ts
authConfig: AuthConfig = {
  issuer: environment.identityAuthority,
  redirectUri: window.location.origin + '/auth/callback',
  postLogoutRedirectUri: window.location.origin,
  clientId: 'quizarena-player',
  responseType: 'code',
  scope: 'openid profile email offline_access api',
  showDebugInformation: false,
  useSilentRefresh: true,
  silentRefreshTimeout: 5000,
  timeoutFactor: 0.75,
  requireHttps: false, // dev http; true en prod
  strictDiscoveryDocumentValidation: false,
}
```

### Claims consumed

From `id_token` / `userinfo`:

| Claim | Uso |
|-------|-----|
| `sub` | `Player.playerId` (PlayerId = sub, FR-002/FR-015) |
| `name` | `Player.displayName` |
| `email` | `Player.email` |
| `roles` / `role` | Guard `AnyPlayerRole` (PLAYER por defecto) |
| `tenant_id` | `Player.tenantId` |
| `must_change_password` | `MustChangePasswordGuard` → redirect `/auth/change-password` (hosted by identity-server, no local UI) |
| `is_master_admin` | ignorado (admin only) |

### Guards

```ts
export const authGuard: CanActivateFn = () => inject(OidcSecurityService).isAuthenticated$;
export const mustChangePasswordGuard: CanActivateFn = () => {
  const claims = inject(OidcSecurityService).getPayloadFromIdToken();
  return claims?.must_change_password ? redirectTo('/auth/change-password-external') : true;
};
```

### Interceptors

- `authInterceptor`: si `req.url.startsWith(apiUrl)` y `isAuthenticated` → `req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })`.
- `correlationIdInterceptor`: `X-Correlation-Id: crypto.randomUUID()`.
- `refresh` se maneja por librería (`silentRenew` / `useRefreshToken`); 401 no recuperable → `login()` redirect a `/connect/authorize`.

## BFF Alternative (si se elige host)

Si auditoría exige ocultar tokens:

- `QuizArena.Player.Host` (net10.0) replica `QuizArena.Admin/Program.cs`:
  - `AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme).AddOpenIdConnect(... Authority = Identity:Authority, ClientId = quizarena-player-bff, ClientSecret = secret, ResponseType = Code, SaveTokens = true, GetClaimsFromUserInfoEndpoint = true)` + `AddCookie`.
  - `AddAuthorization` con policy `PlayerOnly` (roles claim `PLAYER`).
  - `MapForwarder("/bff/{**catch-all}", "http://oroclash-api", transform: req => req.Headers.Authorization = $"Bearer {accessTokenFromCookie}" )`.
  - `MapFallbackToFile("index.html")` para SPA routing.
- Angular entonces llama `fetch('/bff/games/...')` sin `Authorization` header; BFF adjunta Bearer server-side. Tokens nunca en `sessionStorage`.

## Logout

- `POST /connect/revoke` (access + refresh) + `GET /connect/logout?id_token_hint=...&post_logout_redirect_uri=...` (RP-initiated). Lib `logoff()` lo hace.
- `Account/Logout` confirmation hosted by identity-server.

## Security notes

- Nunca `localStorage` para tokens (XSS). Memoria + `sessionStorage` efímero para `silentRenew` nonce.
- `access_token` lifetime corto (5-15 min) + `refresh_token` rotación.
- `withCredentials` no usado en PKCE (Bearer header); en BFF sí (`Cookie` httpOnly + `SameSite=Lax`).
