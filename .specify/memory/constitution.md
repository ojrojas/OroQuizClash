<!-- Sync Impact Report
Version change: 1.0.0 → 1.1.0 (MINOR — new governance-binded external identity platform)
Ratified: 2026-08-26 | Last Amended: 2026-08-26
Source amendment: draft/oroidentityserver-specification.md (podman image oroidentityserver) applied onto draft/constitution.md + draft/constitution-addendum.md + draft/game-concept.md
Modified principles:
  - V. Authoritative Domain Engine & Server Truth — clarified that authentication/authorization truth is delegated to OroIdentityServer; game truth remains in OroQuizClash domain
  - [NEW] VI. Externalized Identity via OroIdentityServer (NON-NEGOTIABLE) — containerized OAuth2/OIDC authority, user creation + auth only
Modified sections:
  - Additional Constraints → H. Security — rewritten to mandate OroIdentityServer as sole identity provider, JWT/OIDC integration, no local user store duplication, Podman deployment contract, env/DataProtection/DB isolation
  - Governance → Guidance Files — added draft/oroidentityserver-specification.md as normative reference
  - Header metadata — added Identity Provider line
Added sections: none beyond new principle VI (counts as MINOR)
Removed sections: none
Deferred TODOs: none
Bump rationale: MINOR — new principle/expanded guidance (external identity platform) without removing or redefining existing principles incompatibly. Follows SemVer: MAJOR=breaking redefinition, MINOR=new principle/section, PATCH=clarification.
-->

# OroQuizClash Constitution

**Project:** OroQuizClash *(canonical name; alias QuizArena in addendum/game-concept references the same system)*  
**Architecture:** Modular Monolith / Clean Architecture / DDD / CQRS / Vertical Slice  
**Backend:** .NET / C# (`net10.0`, aligned with BuildingBlocks multi-targeting)  
**Frontend:** Web (presentation-only; ASP.NET Core Web API + SignalR)  
**Primary Database:** Microsoft SQL Server (authoritative for OroQuizClash game domain)  
**Secondary Database Target:** Oracle (portable abstraction, no Domain/Application rewrite)  
**Identity Provider:** OroIdentityServer — Podman container image `oroidentityserver:latest` (OAuth2/OIDC via OpenIddict 8, PostgreSQL `identitydb`, Blazor FluentUI admin, RabbitMQ optional) — sole authority for user creation + authentication + authorization  
**Specification Method:** SDD / SpecKit — Constitution → Specification → Clarification → Plan → Tasks → Implementation → Tests → Validation

## Core Principles

### I. Domain First (NON-NEGOTIABLE)

Business rules MUST live in the Domain layer. Controllers, UI components, database repositories and infrastructure services MUST NOT contain core game rules.

The following MUST be modeled as domain concepts and protected by aggregate invariants: Game lifecycle, Round lifecycle, Question selection, Answer evaluation, Difficulty progression, Score calculation, Player withdrawal, Game completion, Reward eligibility and Consolation eligibility. State changes MUST occur through explicit domain behavior (e.g., `Game.Start()`, `Game.StartRound()`, `Game.SubmitAnswer()`, `Game.WithdrawPlayer()`, `Game.Finish()`, `Game.AdvanceLevel()`). Anemic domain models where behavior resides solely in application services are prohibited. Business logic MUST NOT depend on ASP.NET Core, Entity Framework Core, SQL Server, Oracle, Angular, SignalR or external APIs.

*Rationale:* The game is a domain engine, not a collection of HTTP endpoints. Centralizing rules guarantees testability, auditability and resistance to client tampering.

### II. Clean Architecture & Dependency Inversion (NON-NEGOTIABLE)

The system MUST follow dependency inversion. Dependency direction MUST be `Web → Application → Domain ← Infrastructure`; Infrastructure implements contracts defined by inner layers. Domain MUST NOT reference Infrastructure or Web; Application MUST NOT depend on concrete infrastructure implementations.

Domain-Driven Design is mandatory. Core aggregates/entities include `Game`, `GamePlayer`, `GameRound`, `Question`, `Category`, `AnswerOption`, `Score`, `PointTransaction`, `Reward`, `RewardRedemption`. Aggregates MUST protect invariants, MUST NOT expose unrestricted mutable state, and SHOULD use `BuildingBlocks.Kernel.Domain` abstractions (`Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`).

*Reference:* `draft/constitution.md` §3-§4; `draft/game-concept.md` §2, §17-§18.

### III. BuildingBlocks as Platform — No Reinvention (NON-NEGOTIABLE)

OroQuizClash MUST reuse the existing BuildingBlocks platform as an architectural dependency. BuildingBlocks provide technical infrastructure; OroQuizClash provides business capabilities.

MUST reuse (and MUST NOT reimplement):

```
Entity, AggregateRoot, ValueObject, StronglyTypedId, Enumeration,
IDomainEvent, IDomainEventHandler, IBusinessRule, Result, Error,
IRepository<TAggregate,TId>, IUnitOfWork, Specification<T>,
ICommand, IQuery, ICommandHandler, IQueryHandler, ISender, IPipelineBehavior,
IntegrationEvent, IEventBus, IIntegrationEventHandler, IOutboxWriter, IEndpoint
```

Provided by `BuildingBlocks.Kernel.Domain`, `BuildingBlocks.CQRS`, `BuildingBlocks.EventBus`, `BuildingBlocks.EventBus.RabbitMQ`, `BuildingBlocks.Kernel.Infrastructure`, `BuildingBlocks.ServiceDefaults`.

MUST NOT introduce `MediatR`, `MassTransit` or `AutoMapper` for capabilities already covered. Multi-targeting MUST be `net10.0` (aligned with BuildingBlocks `net10.0`). Framework-specific code MUST be isolated and documented. Architecture tests MUST verify forbidden dependencies (Domain → ASP.NET/EF/RabbitMQ, Application → concrete infra, unauthorized MediatR/MassTransit/AutoMapper).

*Reference:* `draft/constitution-addendum.md` §1-§3, §19-§22; `draft/libraries/buildingblocks.md`.

### IV. Vertical Slice + Explicit CQRS (NON-NEGOTIABLE)

Application MUST use `BuildingBlocks.CQRS` (`ICommand<T>`, `IQuery<T>`, `ICommandHandler`, `IQueryHandler`, `ISender`, `IPipelineBehavior`) with Vertical Slice Architecture. No secondary dispatcher allowed. Validation and logging SHOULD use existing pipeline behaviors.

Each feature slice MUST be self-contained (`Command or Query` + `Validator` + `Handler` + `Response DTO` + `Endpoint`) under `Features/{Feature}/`, e.g., `Features/Games/SubmitAnswer.cs`. Centralized generic command folders are prohibited. Mapping MUST be explicit (no AutoMapper) and co-located with the feature. Endpoints MUST implement `IEndpoint` from `BuildingBlocks.ServiceDefaults`, remain thin, and delegate to `ISender.SendAsync()` then map `Result` to HTTP. Example commands: `CreateGame`, `JoinGame`, `StartGame`, `StartRound`, `SubmitAnswer`, `WithdrawPlayer`, `FinishGame`, `RedeemReward`; example queries: `GetGame`, `GetCurrentRound`, `GetCurrentQuestion`, `GetPlayerScore`, `GetLeaderboard`, `GetRewards`.

Domain events are in-process and dispatched inside `AppDbContextBase.SaveChanges`; integration events use `IntegrationEvent`/`IEventBus` via transactional Outbox (`IOutboxWriter` + `OutboxProcessor` → `BuildingBlocks.EventBus.RabbitMQ`). Domain → messaging infrastructure knowledge is forbidden. `AppDbContext` MUST derive from `AppDbContextBase`; `EfRepository` with `SpecificationEvaluator` and Outbox entity configuration are required.

*Reference:* `draft/constitution-addendum.md` §5-§10, §15-§18; `draft/constitution.md` §16-§17.

### V. Authoritative Domain Engine & Server Truth (NON-NEGOTIABLE)

The server is the sole authority. The client is untrusted and is a presentation layer only.

All authoritative **game** decisions (answer correctness, points awarded, advancement, reward eligibility, remaining time) MUST be evaluated server-side in OroQuizClash using server timestamps. Authentication and authorization truth is explicitly **delegated** to OroIdentityServer (see Principle VI) — OroQuizClash MUST NOT re-derive identity decisions locally. The frontend MAY provide countdown/score/round/leaderboard visualizations and answer selection but MUST NOT implement authoritative rules. Multiplayer player state MUST be isolated — a player MUST NOT mutate another player's answer/score/level/withdrawal/reward. SignalR MAY be used for server-driven notifications (`RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `LeaderboardUpdated`, `RoundCompleted`, `GameFinished`) but MUST NOT be the source of truth. All state-changing commands MUST be transactional and idempotent where applicable.

*Reference:* `draft/constitution.md` §9-§10, §20, §30, §39; `draft/oroidentityserver-specification.md` §1, §4.

### VI. Externalized Identity via OroIdentityServer — Podman Container (NON-NEGOTIABLE)

OroQuizClash MUST delegate all identity concerns to the pre-built **OroIdentityServer** Podman container image `oroidentityserver:latest` (defined by `src/IdentityServer/IdentityServer/Dockerfile`, spec in `draft/oroidentityserver-specification.md`). The image is the sole system of record for **user creation** and **authentication/authorization**; OroQuizClash MUST NOT implement a parallel user store, password hashing, JWT signing, token issuance or admin identity UI.

**Scope limited to identity:** Only creation/management of `User`, `Role`, `Permission`, `Tenant`, `IdentificationType`, `Application` (OIDC client), `Scope`, and `UserSession`, plus OAuth2/OIDC flows and admin API/UI. Game-specific entities (`Game`, `GamePlayer`, `Score`, `Reward`) remain in OroQuizClash. The OroIdentityServer database (`identitydb` on PostgreSQL) MUST remain physically and logically separate from OroQuizClash SQL Server/Oracle game database.

**Capabilities that MUST be consumed from the image:**

- OAuth2/OIDC via OpenIddict 8: `authorization_code` (recommended for web), `refresh_token`, `client_credentials`, `password`; JWT issuance/validation, revocation, end-session.
- Cookie-based admin sign-in with shared DataProtection keyring, custom `/auth/login`, `/auth/logout`, `/auth/change-password`, forced `must_change_password` claim handling, and IdentityServer-owned `/Account/Logout` confirmation for RP-initiated logout.
- Blazor FluentUI admin UI (`/`, Dashboard, Users/Roles/Applications/Scopes/Identification Types/Sessions) and REST admin API under `/api/*` (policies `ManagerOrAdmin`, `AdminOnly`, `MasterAdminOnly`).
- Session tracking (`UserSession`) and termination revoking OpenIddict tokens/authorizations.

**Integration contract (OroQuizClash as OIDC client):**

- Discover via `GET http://<identity-host>:5080/.well-known/openid-configuration` and consume `authorization_endpoint`, `token_endpoint`, `userinfo_endpoint`, `jwks_uri`, `introspect`, `revoke`.
- Register OroQuizClash as OpenIddict client via `POST /api/applications` (or Blazor admin UI) with `clientId`/`clientSecret`, `authorization_code` + `refresh_token`, `redirectUris`, `permissions` (`openid`, `profile`, `email`, `offline_access`).
- Validate JWTs via `jwks_uri`, consume `connect/userinfo` claims (`sub`, `email`, `name`, `roles`, `tenant_id`, `is_master_admin`, `must_change_password`), and enforce local authorization policies from those claims. No local password or role tables for identity.
- Use standard endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/introspect`, `/connect/revoke`, `/connect/logout`; auth endpoints `/auth/login`, `/auth/change-password`.

**Deployment contract (Podman — no reimplementation):**

- Build: `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Run standalone: `podman run --rm -p 5080:5080 -e ConnectionStrings__identitydb="Host=db;Port=5432;Database=identitydb;Username=postgres;Password=***" -e SymmetricSecurityKey="<base64>=32bytes" -e SEED_ADMIN_USERNAME="admin" -e SEED_ADMIN_PASSWORD="***" oroidentityserver:latest`
- Compose (`podman compose up -d --build`): services `db` (`postgres_db:5432` with `pg_isready` health check) + `identity-server` (`identity_server:5080,5086`, depends on `db`), volume `identity-dp-keys` at `/app/data-protection-keys`, auto-migrations + `admin` seed (`admin`/`Admin@123456` exempt from forced password change; all other users forced).
- Env overrides REQUIRED in production: `SymmetricSecurityKey` (≥32 bytes, shared across instances), `ConnectionStrings__identitydb`, `SEED_ADMIN_*` (`USERNAME`, `PASSWORD`, `EMAIL`, `NAME`, `LASTNAME`, `IDENTIFICATION`, `ROLE`, `FORCE_PASSWORD_CHANGE`), optional `EventBus__RabbitMQ__*`, `Kestrel__Certificates__Default__Path/Password/KeyPath` for HTTPS (`5086`), `ASPNETCORE_URLS`, `DatabaseSeeder__Skip`. Mounting `/app/Data/seedData.json` overrides baked seed.
- Image is `mcr.microsoft.com/dotnet/aspnet:10.0` slim, non-root, both HTTP `5080`/HTTPS `5086` bound (HTTPS needs cert mount). No SDK in runtime.

**Prohibitions:** OroQuizClash MUST NOT duplicate OroIdentityServer's `BuildingBlocks.Kernel`/`CQRS`/`EventBus` internals inside game services; it MUST consume them as a black-box OIDC provider. Any need to extend identity behavior MUST be done in the OroIdentityServer image, not forked into OroQuizClash. Aspire orchestration (`examples/AppHost`) MAY be used locally to wire Postgres+pgAdmin, Redis, RabbitMQ, `identity-api` and sample frontends, but production deployment is via Podman image as specified.

*Reference:* `draft/oroidentityserver-specification.md` full spec — Key Features §1-§9, Project Structure, Containerized Deployment, Integration §1-§5, Configuration & env table, API Endpoints.

## Additional Constraints

### A. Game Lifecycle as State Machine

Game lifecycle MUST be explicitly modeled with at minimum: `DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED`, `FINISHED`, `CANCELLED`, `FORCED_FINISHED`. Invalid transitions (e.g., `FINISHED → StartGame`) MUST be rejected by the Domain. Round and game state transitions MUST be concurrency-protected.

### B. Question & Category Invariants

Every active question MUST have exactly four answer options and exactly one correct answer, belong to an active category, and define difficulty characteristics (`Complexity`, `AcademicLevel`, `AgeRange`, `KnowledgeCategory`). A category MUST contain ≥5 valid questions before publication. Question selection MUST prevent unnecessary repetition within a game and SHOULD be behind `IQuestionSelectionStrategy` (e.g., `Random`, `DifficultyAware`, `Adaptive`). Selection MUST consider category, difficulty, academic/age range and already-used questions.

### C. Configurable Game Rules

Difficulty, game configuration, withdrawal/loss/consolation/reward policies MUST be configurable and represented via strategy/policy abstractions — NOT hardcoded.

- **Difficulty:** At least 5 levels (Basic/Elementary/Intermediate/Advanced/Expert naming is illustrative only). Progression via strategy (`Linear`, `Progressive`, `Adaptive`, `CategorySpecific`).
- **Game Configuration (immutable after start):** Category, min rounds (MUST be ≥5), max rounds, initial difficulty, progression strategy, `TimeLimitPerQuestion`, `PointsPerRound`, withdrawal/loss/consolation/reward policies.
- **Withdrawal policies:** `LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`. Withdrawal is an explicit domain action; forbidden after terminal state.
- **Loss policies (incorrect answer):** `LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT` — selected via configuration.
- **Consolation:** Independent from normal rewards; eligibility via explicit business rule; MUST NOT be treated as successful completion.
- **Rewards:** Independently modeled (`Reward`, `RewardRedemption`) with lifecycle `REQUESTED → APPROVED → REJECTED → DELIVERED → CANCELLED` (and `VALIDATING` where applicable). Redemption MUST be atomic, auditable and blocked on insufficient eligible points. Point deduction MUST be ledger-backed.

### D. Scoring via Ledger

Points MUST be represented as explicit `PointTransaction` ledger entries, not direct balance mutation. Types include `ANSWER_CORRECT`, `ANSWER_INCORRECT`, `ROUND_BONUS`, `LEVEL_BONUS`, `GAME_BONUS`, `PENALTY`, `WITHDRAWAL`, `REWARD_REDEMPTION`, `CONSOLATION`, `ADJUSTMENT`. Player balance MUST be reconstructable from transaction history.

### E. Persistence & Database Design

Primary implementation MUST be Microsoft SQL Server; architecture SHOULD allow Oracle support without modifying Domain/Application. Abstractions (`IRepository`, `IUnitOfWork`, `Specification<T>`) MUST NOT leak DB-specific details into Domain. `DbContext` SHOULD derive from `AppDbContextBase`; domain events and Outbox participate in the same `SaveChanges` transaction. Transactions MUST protect multi-aggregate game state changes. Database MUST enforce integrity constraints (exactly one correct answer, FK integrity, unique question/reward redemption identifiers, concurrency version via `rowversion`/`ROW SCN`). Indexes MUST follow actual query patterns. Specifications (`Where`, `And`/`Or`/`Not`, `Include`, ordering, pagination, `ApplyAsNoTracking`) SHOULD be used for reusable domain-oriented queries; no second specification framework allowed.

Identity data (`identitydb` PostgreSQL, Npgsql, EF Core migrations in `OroIdentityServer.Infraestructure`) MUST remain isolated from game persistence; OroQuizClash MUST NOT write to `identitydb` directly except via OroIdentityServer's `/api/*` and OIDC endpoints. Shared `BuildingBlocks.Kernel` primitives are reused, not duplicated, across both systems.

### F. Concurrency & Idempotency

System MUST assume concurrent requests. Optimistic concurrency SHOULD be preferred (`rowversion` on SQL Server, version column on Oracle). MUST protect: game/round state transitions, answer submissions, score updates, reward redemptions. Duplicate commands MUST be treated as idempotent via `IdempotencyKey`/`AnswerSubmissionId`; duplicate answer submissions MUST NOT duplicate point allocation. Integration event handlers MUST be idempotent under at-least-once delivery (use event/idempotency keys).

### G. Real-Time, Integration Events & Outbox

RabbitMQ MUST be used only where async integration adds value; critical game state MUST remain transactionally persisted — RabbitMQ is never the source of truth. Candidates for integration events: `GameFinished`, `RewardRedeemed`, `RewardGranted`, `GameStatisticsGenerated`, `NotificationRequested`. Flow MUST be `Command → Domain operation → Domain events → Transaction (aggregates + Outbox) → OutboxProcessor → RabbitMQ`. External publication before DB commit is forbidden. `BuildingBlocks.EventBus.RabbitMQ` (topic exchange, publisher confirms, manual ack, exponential retries) is the only allowed transport. OroIdentityServer's optional `EventBus__RabbitMQ__*` integration MUST NOT be assumed as game-state transport unless explicitly configured.

### H. Security — Delegated to OroIdentityServer

Authentication and authorization are mandatory and **MUST be delegated** to OroIdentityServer. OroQuizClash MUST NOT maintain its own user credential store.

- **OroQuizClash responsibilities:** Validate JWT bearer tokens issued by OroIdentityServer (`jwks_uri` from discovery), enforce policy-based authorization from OIDC claims (`roles`, `permissions`, `tenant_id`, `is_master_admin`), validate all user-controlled identifiers, and never trust client-supplied score, answer correctness, game state or remaining time. Rate limiting, correlation IDs, input validation, structured logging (without sensitive data) and audit requirements still apply locally.
- **OroIdentityServer responsibilities (consumed as-is):** User/Role/Permission/Tenant/Application/Scope lifecycle via `/api/users`, `/api/roles`, `/api/permissions`, `/api/tenants`, `/api/applications`, `/api/scopes`, `/api/identification-types`, `/api/user-sessions`; session tracking and forced disconnect; login/change-password/logout via `/auth/*` and `/Account/Login|ChangePassword|Logout|Consent`; OIDC discovery and token endpoints (`/connect/*`); localization (8 languages) and admin UI are out of scope for OroQuizClash to reimplement.
- **Policy mapping:** OroQuizClash SHOULD map its required roles/policies (`ADMIN`, `GAME_MANAGER`, `PLAYER`, `REWARD_MANAGER` and `Category.Read/Write`, `Question.Read/Write/Publish`, `Game.Create/Start/Play`, `Reward.Read/Redeem/Manage`) to OroIdentityServer roles/permissions and enforce them via JWT claims + local `[Authorize(Policy=...)]`. The underlying `ManagerOrAdmin`/`AdminOnly`/`MasterAdminOnly` policies of OroIdentityServer govern admin API access to identity itself, not game rules.
- **Prohibition:** No local `User` aggregate for authentication in OroQuizClash; game `GamePlayer` is a separate concept referencing the external `sub` claim, not a credential holder.

### I. Validation, Errors, Observability & Audit

- **Validation:** Three levels — API (transport contract), Application (use-case requirements), Domain (invariants). Domain invariants MUST NOT rely solely on API validation. OroIdentityServer's FluentValidation pipeline is separate; OroQuizClash uses its own `ValidationBehavior`.
- **Errors:** Consistent `RFC 7807 ProblemDetails` via `GlobalExceptionHandler` and `Result → HTTP` mapping. Business failures use explicit `Error` codes (e.g., `GameNotFound`, `GameAlreadyStarted`, `InvalidGameState`, `PlayerNotInGame`, `QuestionAlreadyAnswered`, `InvalidAnswer`, `InsufficientPoints`, `RewardUnavailable`, `CategoryNotReady`). Identity errors (invalid credentials, token revocation, `must_change_password` redirect, session termination) are owned by OroIdentityServer and MUST be surfaced as OIDC/HTTP semantics, not re-wrapped as game domain errors. Internal details MUST NOT leak.
- **Observability:** Hosts MUST use `BuildingBlocks.ServiceDefaults` (OpenTelemetry logs/traces/metrics + OTLP, `/health`/`/alive`, HTTP resilience). Structured logging MUST include `CorrelationId`, `TraceId`, `GameId`, `PlayerId`, `RoundId`, `QuestionId`, `Command`, `Duration`, `Result`; sensitive data MUST NOT be logged. OroIdentityServer's Serilog/Seq/OpenTelemetry/Quartz setup is independent and MUST NOT be merged into game hosts.
- **Audit:** Append-only audit records (game creation/configuration/start, player join, question selection, answer submission, score changes, withdrawal, game completion, reward redemption, administrative adjustments). Audit MUST NOT mutate historical decisions. Identity audit (user creation, role assignment, login sessions, tenant changes) is owned by OroIdentityServer and MUST NOT be duplicated in game audit tables.

### J. API & Frontend

API SHOULD follow REST with meaningful resources, DTOs at the boundary (no direct exposure of domain entities), and pagination for large collections. Example:

```
POST /api/games | POST /api/games/{gameId}/players | POST /api/games/{gameId}/start
GET  /api/games/{gameId} | GET /api/games/{gameId}/rounds/current | GET /api/games/{gameId}/questions/current
POST /api/games/{gameId}/answers | POST /api/games/{gameId}/withdraw | GET /api/games/{gameId}/leaderboard
GET  /api/rewards | POST /api/rewards/{rewardId}/redeem
POST /api/categories | PUT /api/categories/{id} | POST /api/categories/{id}/publish
POST /api/questions | PUT /api/questions/{id}
```

All game APIs MUST require JWT bearer authentication issued by OroIdentityServer; anonymous access is forbidden except where explicitly specified (e.g., health checks). Frontend MUST be treated as presentation layer; backend remains authoritative. Frontend authentication MUST use OIDC `authorization_code` + `refresh_token` against OroIdentityServer discovery, not a custom login form. The login/change-password/logout UI is provided by OroIdentityServer (`/Account/*`, `/auth/*`); OroQuizClash frontend MUST redirect there and handle `must_change_password` claim gating.

## Development Workflow

### SDD Flow

All significant functionality MUST begin with a specification. Required flow:

```
Constitution → Specification → Clarification → Architecture Plan → Tasks → Implementation → Tests → Validation
```

A feature is NOT complete merely because it compiles. It MUST satisfy `Specification + Acceptance Criteria + Automated Tests`. Identity-related specs MUST reference OroIdentityServer as an external dependency via its discovery document, not by re-specifying user tables.

### Testing Strategy (MANDATORY)

Automated tests are mandatory. Minimum suites: `Domain Unit Tests`, `Application Tests`, `Integration Tests`, `API Tests`, `Architecture Tests`. Critical game rules (`Game.Start/StartRound/SubmitAnswer/Withdraw/Finish/AdvanceLevel`, scoring, withdrawal/loss policies) MUST have unit tests using `Arrange/Act/Assert` with descriptive names. Concurrency-sensitive operations and idempotency SHOULD have integration tests (including duplicate simultaneous submissions). Architecture tests MUST enforce dependency rules. Domain/Application tests MUST run without Web server, DB connection, external API, browser or SignalR — except for integration tests that spin up the OroIdentityServer container via Podman/Testcontainers for OIDC validation. Core business rules MUST be executable in isolation. Identity flows SHOULD be tested via `tests/Server.Tests`-style `WebApplicationFactory` + OIDC mock or via the real `oroidentityserver:latest` container with seeded `admin` credentials, not by faking password hashes locally.

### Code Quality & Dependencies

Code MUST favor explicitness, readability, cohesion, small abstractions, strong typing, immutability where appropriate, dependency inversion and composition over inheritance. Unnecessary abstractions are forbidden — every abstraction MUST have a clear business/architectural purpose; do not introduce patterns merely to demonstrate them. External libraries MUST be justified; native .NET capabilities are preferred when sufficient; infrastructure complexity MUST be minimized. Adding a second identity library (e.g., IdentityServer4, ASP.NET Identity) to OroQuizClash is prohibited — OroIdentityServer is the single identity dependency.

### ADRs

Significant decisions MUST be documented as ADRs (e.g., `ADR-001 Modular Monolith`, `ADR-002 Clean Architecture`, `ADR-003 CQRS without MediatR`, `ADR-004 SQL Server primary`, `ADR-005 SignalR`, `ADR-006 Optimistic concurrency`, `ADR-007 Point ledger`, `ADR-008 Question selection strategy`, `ADR-009 Reward abstraction`, `ADR-010 OroIdentityServer as external OIDC provider via Podman`).

### Definition of Done

A feature is complete only when: domain behavior is implemented, application use case is implemented, API contract exists (when applicable), persistence is implemented (when applicable), validation exists, error scenarios are handled, automated tests exist, concurrency concerns evaluated, security implications evaluated (including OroIdentityServer claim/policy enforcement), logging/auditing evaluated, and documentation/specification updated.

### Non-Functional & Evaluation Objectives

System SHOULD target availability, scalability, security, observability, maintainability, testability, consistency and low latency. Initial architecture SHOULD be modular monolith — premature distributed complexity MUST be avoided. Simplicity principle: `Simple + Well Designed + Well Tested + Extensible` over `Complex + Distributed + Over-engineered + Poorly Tested`. Implementation SHOULD demonstrate: Clean Architecture, DDD, CQRS, SOLID, modern .NET/ASP.NET Core, REST, validation, auth via external OIDC, SignalR, concurrency/transactions, relational modeling, SQL Server/EF Core/query optimization, unit/integration/architecture testing, logging/observability/auditing, configurable rules, extensible engine, reward lifecycle, multiplayer, anti-cheating and idempotency.

## Governance

This Constitution supersedes all other practices. All PRs and reviews MUST verify compliance; violations MUST be justified via an ADR or rejected. Complexity MUST be justified against the Simplicity principle.

**Amendment Procedure:** Changes require: (1) documented proposal with rationale, (2) approval by project maintainers, (3) migration plan for affected specs/plans/tasks, (4) version bump per semantic versioning. Amendments are recorded in the Sync Impact Report comment at the top of this file. Changing the identity provider from OroIdentityServer requires a MAJOR version bump.

**Versioning Policy (SemVer):** `MAJOR` — backward-incompatible governance or principle removals/redefinitions (including replacing OroIdentityServer); `MINOR` — new principle or materially expanded guidance (including adding OroIdentityServer); `PATCH` — clarifications, wording, typo fixes.

**Compliance Review:** Every spec, plan and task MUST reference applicable constitution principles. Architecture tests and code reviews are the enforcement mechanisms. `BuildingBlocks.ServiceDefaults` conventions (OTel, health checks, resilience, `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`) are the default and require an ADR to replace. Identity compliance MUST be verified by asserting JWT validation against OroIdentityServer's `jwks_uri` and that no local credential handling exists.

**Guidance Files:** Use `draft/constitution.md` (39 sections) as the authoritative domain reference, `draft/constitution-addendum.md` as the mandatory BuildingBlocks addendum, `draft/game-concept.md` / `draft/libraries/buildingblocks.md` as supporting context, and `draft/oroidentityserver-specification.md` as the normative reference for the OroIdentityServer Podman image (build, run, env, OIDC discovery, admin API/UI). Runtime development guidance lives in `.specify/memory/constitution.md` (this file).

**Version**: 1.1.0 | **Ratified**: 2026-08-26 | **Last Amended**: 2026-08-26
