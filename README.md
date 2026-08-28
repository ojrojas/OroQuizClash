opencode -s ses_fc127e1e1ffeAQrv8KOf7zBFGA



mac
opencode -s ses_fbf52eb20ffelMpsD1AikQkcYa
---

# OroQuizClash — QuizArena

Multiplayer trivia game platform: `QuizArena.Api` (Modular Monolith BuildingBlocks) + `QuizArena.Admin` (Blazor .NET 11) + `QuizArena.Player` (Angular 22).

## UI/UX Design System (SPEC-016, ADR-012)

- **Source of truth**: `design-system/MASTER.md` (shared) + `design-system/overrides/{admin,player}.md` + 11 page overrides in `design-system/pages/`
- **Tokens**: three-layer primitive→semantic→component in `design-system/tokens/design-tokens.{json,css}`; themes via `[data-theme="administration"|"player"]`; 0 hex literals outside tokens
- **Palette**: quiz blue `#2563EB` + gold `#F59E0B`; Admin light enterprise (`#1E40AF`/Fira), Player dark cinematic (`#0F172A`/Russo One + Chakra Petch)
- **Responsive**: 375/768/1024/1440 normative (adaptive, not scaled); WCAG 2.2 AA both themes; reduced-motion + forced-colors supported
- **Governance**: `design-system/GOVERNANCE.md` (token/component changes, semver, CI checks); quality gate `design-system/QUALITY-GATE.md`
- **Validation**: `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/` (0 literals) + `dotnet test tests/OroQuizClash.Architecture.Tests` (incl. `DesignSystemNoDirectDbTests`: Admin/Player never access DB directly)
- **Spec**: `specs/016-ui-ux-design-system/` | **ADR**: `docs/adr/ADR-012-design-system.md`
- **Roadmap**: SPEC-017 (Admin app) and SPEC-027 (Player app) consume this design system; no UI code before Design System established (Addendum 2 §3)

## QuizArena.Admin — Administration Application (SPEC-017, ADR-013)

net10.0 Blazor Web App (Auto interactivity, BFF via YARP). No direct DB access — all persistence flows through `QuizArena.Api` via the BFF forwarders.

- **Architecture**: Single-origin YARP catch-all `/bff/{**}→/api/{**}` + `/hubs/game` hub forward. Shared file `QuizArena.Admin.Client/Services` contracts, dual implementations: `Client*Service` → `/bff` (cookie + 401→login), `Server*Service` → `http://oroclash-api` (Aspire discovery + `BearerTokenHandler` with `GetTokenAsync("access_token")`). Tokens stay server-side (HttpOnly OIDC cookie + refresh); the WASM client receives only claims (`AddAuthenticationStateSerialization`).
- **OIDC**: Confidential `quizarena-admin` (`authorization_code` + `refresh_token` + PKCE) against `OroIdentityServer`. Scopes `openid profile offline_access roles admin`; `must_change_password` gating → provider `Account/ChangePassword`. Register once: `./scripts/register-admin-oidc-client.sh` (see `specs/017-admin-application/contracts/oidc-config.md`); then `aspire start` injects `Identity__Authority`/`Identity__ClientSecret`/`Identity__ApiScope=admin` via the AppHost parameter `quizarena-admin-oidc-secret` (user secrets).
- **Run (Aspire)**: `aspire start` (postgres + pgAdmin, sqlserver, redis, rabbitmq, identity-api `5080/5086`, `oroclash-api`, `quizarena-admin` `https://localhost:7172`). Health: `https://localhost:7172/health`, `…/alive`. Unauthenticated `GET /admin/dashboard` → `302` to `…/connect/authorize`.
- **AppHost**: `builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin").WithReference(api).WaitFor(api).WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http")).WithHttpHealthCheck("/health")`.
- **ADRs**: `docs/adr/ADR-013-admin-bff-communication.md`
