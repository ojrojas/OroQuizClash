# Contract: Security Policies — SPEC-013

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md)

Autorización centralizada. **Deny-by-default** (FR-004). Autenticación delegada a OroIdentityServer (Constitución VI).

## Roles y permisos (FR-002/FR-003)

| Permiso | ADMIN | GAME_MANAGER | PLAYER | REWARD_MANAGER |
|---------|:-----:|:------------:|:------:|:--------------:|
| `Category.Read` | ✓ | ✓ | ✓ (visibilidad) | — |
| `Category.Write` | ✓ | ✓ | — | — |
| `Category.Publish` | ✓ | ✓ | — | — |
| `Question.Read` | ✓ | ✓ | — (solo via `QuestionPresented`) | — |
| `Question.Write` | ✓ | ✓ | — | — |
| `Question.Publish` | ✓ | ✓ | — | — |
| `Game.Create` | ✓ | ✓ | — | — |
| `Game.Start` | ✓ | ✓ | — | — |
| `Game.Play` | ✓* | ✓* | ✓ (propia participación) | — |
| `Reward.Read` | ✓ | ✓ | ✓ | ✓ |
| `Reward.Redeem` | ✓ | — | ✓ (propios puntos) | — |
| `Reward.Manage` | ✓ | — | — | ✓ |
| `Report.Read` | ✓ | ✓ | — | ✓ |
| `Audit.Read` | ✓ | (limitado) | — | — |

`*` GAME_MANAGER/ADMIN como observador/autorizado en `Game.Play` ajeno cuando la operación lo permite (FR-007).

## Políticas ASP.NET Core (Api/Authorization/SecurityPolicies.cs)

14 políticas nombradas idénticas a permisos (`"Category.Read"`, ... `"Audit.Read"`). Registro en `Program.cs` via `AddAuthorizationBuilder()`:

```csharp
.AddPolicy("Category.Read", p => p.RequireAssertion(ctx =>
    ctx.User.HasClaim(c => c.Type=="roles" && c.Value=="ADMIN") || 
    ctx.User.HasClaim(c => c.Type=="role" && c.Value=="ADMIN") || ... ))
```

Mapeo real: `Category.Read` → `ADMIN`/`GAME_MANAGER`/`PLAYER` según tabla; `Game.Play` → `PLAYER` + alcance por recurso (ver abajo).

## Alcance por recurso (FR-005)

Poseer `Game.Play` no basta: `AuthorizationBehavior` y/o `IEndpoint` verifica `game.Players.Any(p => p.UserId == sub)` o `IsOrganizer` (`ADMIN`/`GAME_MANAGER`). Sin pertenencia → 403 sin revelar existencia.

## Pipeline centralizado (FR-019)

```text
ValidationBehavior → RateLimiting (partitioned) → IdempotencyBehavior → AuthorizationBehavior → AuditBehavior → Handler
```

`AuthorizationBehavior` lee atributo `[RequiresPermission("Game.Play")]` en el `ICommand`/`IQuery` y evalúa contra `ClaimsPrincipal` (inyectado via `IHttpContextAccessor`). Registra `Permission` en `AuditEntry`.

## Endpoints y políticas

| Endpoint | Política | Notas |
|----------|----------|-------|
| `POST /api/categories` | `Category.Write` |  |
| `PUT /api/categories/{id}` | `Category.Write` |  |
| `POST /api/categories/{id}/publish` | `Category.Publish` |  |
| `POST /api/questions` | `Question.Write` |  |
| `PUT /api/questions/{id}` | `Question.Write` |  |
| `POST /api/questions/{id}/publish` | `Question.Publish` |  |
| `POST /api/games` | `Game.Create` |  |
| `POST /api/games/{id}/ready` | `Game.Start` |  |
| `POST /api/games/{id}/open-lobby` | `Game.Start` |  |
| `POST /api/games/{id}/players` | `Game.Play` | `sub` es PlayerId |
| `POST /api/games/{id}/answers` | `Game.Play` | ignora Score/Time/PlayerId cliente |
| `POST /api/games/{id}/withdraw` | `Game.Play` |  |
| `GET /api/games/{id}/players/{playerId}/state` | `Game.Play` | solo propio u organizador |
| `GET /api/games/{id}/leaderboard` | `Game.Play` | participante u organizador |
| `POST /api/rewards/{id}/redeem` | `Reward.Redeem` | + Idempotency-Key |
| `POST /api/rewards` | `Reward.Manage` |  |
| `GET /api/audit` | `Audit.Read` | ver audit-api.md |
| `GET /health`, `/alive` | anonymous |  |

Sin autenticación → 401; con auth pero sin permiso → 403 (sin fuga de existencia); con permiso pero sin alcance de recurso → 403.

## Anti-tampering (FR-006–FR-010)

DTOs de `SubmitAnswer` NO exponen `score`, `correctness`, `elapsedTime`, `gameState`. Si llegan campos extra, se ignoran. Handler usa:
- `Correctness` → `question.AnswerOptions.First(o=>o.IsCorrect)` 
- `Points` → `ScoringSystem` + `PointTransaction` ledger
- `Time` → `DateTimeOffset.UtcNow` + `TimeLimitPerQuestion`
- `PlayerId` → `GameClaims.GetSub(User)`
- `GameState` → `Game.Status` máquina de estados

`questionId`/`answerOptionId` se valida pertenezca a `Game.CurrentRound`.

