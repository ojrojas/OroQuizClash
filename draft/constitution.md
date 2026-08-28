# OroQuizClash Constitution

**Version:** 1.0.0
**Status:** Active
**Project:** OroQuizClash
**Architecture:** Modular Monolith / Clean Architecture / DDD / CQRS
**Backend:** .NET / C#
**Frontend:** Web
**Primary Database:** Microsoft SQL Server
**Secondary Database Target:** Oracle
**Specification Method:** SDD / SpecKit

**Note 1** constitution-addendum.md Not optional, this a rule definition.
**Note 2** game-concept.md The game concept is what is expected of the project as a whole. It's what we're going to develop, and it's the highest priority. 
**Note 3** new constitution-addendum2.md Not optiomal, this expand to make html design UI/UX game.
**Note 4** in project angular (application player) use ngrx-signal-store library to manage state of application rule

---

## 1. Purpose

OroQuizClash is a configurable multiplayer question-and-answer game platform designed to demonstrate enterprise-grade software engineering capabilities using modern .NET and web technologies.

The platform allows administrators to configure categories, questions, difficulty levels, academic levels, age ranges, game rules and rewards.

Players participate in progressive rounds, answer questions, accumulate points and decide whether to continue playing or voluntarily withdraw and preserve their accumulated reward according to the configured game rules.

The project must demonstrate not only functional correctness but also architecture, domain modeling, maintainability, testability, security, concurrency control, observability and software engineering maturity.

---

# 2. Core Principles

## Principle I — Domain First

Business rules MUST live in the Domain layer.

Controllers, UI components, database repositories and infrastructure services MUST NOT contain core game rules.

The following rules MUST be represented by domain concepts:

* Game lifecycle.
* Round lifecycle.
* Question selection.
* Answer evaluation.
* Difficulty progression.
* Score calculation.
* Player withdrawal.
* Game completion.
* Reward eligibility.
* Consolation reward eligibility.

Business logic MUST NOT depend on:

* ASP.NET Core.
* Entity Framework Core.
* SQL Server.
* Oracle.
* Angular.
* SignalR.
* External APIs.

---

# 3. Clean Architecture

The system MUST follow dependency inversion.

The architectural dependency direction MUST be:

```text
Web
 ↓
Application
 ↓
Domain
 ↑
Infrastructure
```

The Domain layer MUST NOT reference Infrastructure or Web.

The Application layer MUST NOT depend directly on concrete infrastructure implementations.

Infrastructure MUST implement contracts defined by the inner layers.

---

# 4. Domain-Driven Design

The system MUST use Domain-Driven Design principles.

Core domain concepts include:

* Game.
* GamePlayer.
* GameRound.
* Question.
* Category.
* AnswerOption.
* Score.
* PointTransaction.
* Reward.
* RewardRedemption.

Aggregates MUST protect their own invariants.

An entity MUST NOT expose unrestricted mutable state.

State changes SHOULD occur through explicit domain behavior.

Examples:

```text
Game.Start()
Game.StartRound()
Game.SubmitAnswer()
Game.WithdrawPlayer()
Game.Finish()
Game.AdvanceLevel()
```

Avoid an anemic domain model where all behavior resides in application services.

---

# 5. Game as a State Machine

The game lifecycle MUST be explicitly modeled.

At minimum the system MUST support:

```text
DRAFT
READY
WAITING_FOR_PLAYERS
IN_PROGRESS
ROUND_IN_PROGRESS
ROUND_COMPLETED
FINISHED
CANCELLED
FORCED_FINISHED
```

Invalid state transitions MUST be rejected.

For example:

```text
FINISHED → StartGame
```

MUST NOT be allowed.

The domain MUST determine whether a requested transition is valid.

---

# 6. Question Invariants

Every active question MUST have exactly four answer options.

Every active question MUST have exactly one correct answer.

A question MUST belong to an active category before it can participate in a game.

A category MUST contain at least five valid questions before it can be published.

A valid question MUST define its difficulty characteristics.

At minimum these characteristics SHOULD support:

* Complexity.
* Academic level.
* Age range.
* Knowledge category.

Question selection MUST prevent unnecessary repetition within the same game.

---

# 7. Configurable Difficulty

Difficulty MUST be configurable.

The initial implementation SHOULD support at least:

```text
1 — Basic
2 — Elementary
3 — Intermediate
4 — Advanced
5 — Expert
```

However, the domain MUST NOT depend on these names being hardcoded.

Difficulty progression MUST be represented through a strategy or equivalent abstraction.

The system SHOULD allow future implementations such as:

```text
Linear progression
Progressive progression
Adaptive progression
Category-specific progression
```

---

# 8. Game Configuration

A game MUST be configurable before it starts.

At minimum the configuration SHOULD support:

* Category.
* Minimum rounds.
* Maximum rounds.
* Initial difficulty.
* Difficulty progression.
* Time limit per question.
* Points per round.
* Withdrawal policy.
* Loss policy.
* Consolation policy.
* Reward policy.

The minimum number of rounds MUST be five.

Configuration MUST be immutable once the game has started unless a specific administrative rule explicitly allows modification.

---

# 9. Multiplayer

The game MUST support multiple players.

Player state MUST be isolated.

A player MUST NOT be able to modify another player's:

* Answer.
* Score.
* Level.
* Withdrawal state.
* Reward eligibility.

All authoritative game decisions MUST be made by the server.

The client MUST be considered untrusted.

---

# 10. Answer Submission

Answers MUST be evaluated server-side.

The client MUST NOT determine:

* Whether an answer is correct.
* How many points are awarded.
* Whether the player advances.
* Whether the player is eligible for a reward.

Each answer submission SHOULD contain an idempotency identifier.

Duplicate submissions MUST NOT result in duplicate point allocation.

The server MUST use server-side timestamps when validating response time.

---

# 11. Scoring

Points MUST be represented as explicit domain transactions.

Direct manipulation of a player's balance MUST be avoided.

The system SHOULD maintain a point ledger:

```text
PointTransaction
```

Examples:

```text
ANSWER_CORRECT
ANSWER_INCORRECT
ROUND_BONUS
LEVEL_BONUS
GAME_BONUS
PENALTY
WITHDRAWAL
REWARD_REDEMPTION
CONSOLATION
ADJUSTMENT
```

The player's balance MUST be reconstructable from the transaction history.

---

# 12. Risk and Withdrawal

The player MAY voluntarily withdraw when the current game rules allow it.

Withdrawal MUST be explicitly represented as a domain action.

The withdrawal behavior MUST be configurable.

Supported policies MAY include:

```text
LOSE_ALL
KEEP_CURRENT_SCORE
KEEP_SECURED_SCORE
KEEP_CHECKPOINT_SCORE
```

The player MUST NOT be allowed to withdraw after the game has reached a terminal state.

---

# 13. Incorrect Answers

An incorrect answer MUST be evaluated according to the configured loss policy.

Possible policies include:

```text
LOSE_ALL
LOSE_CURRENT_ROUND
LOSE_UNSECURED_POINTS
FALLBACK_TO_CHECKPOINT
```

The selected policy MUST be part of the game configuration.

The implementation MUST NOT hardcode a single loss strategy inside the game controller.

---

# 14. Consolation Rewards

A player who participates but does not obtain a normal reward MAY receive a consolation reward.

Eligibility MUST be determined by an explicit business rule.

Consolation rewards MUST be represented independently from normal rewards.

A consolation reward MUST NOT be treated as a successful normal game completion.

---

# 15. Rewards

Rewards MUST be modeled independently from the game engine.

The system MUST support:

```text
Reward
RewardRedemption
```

Reward redemption MUST have an explicit lifecycle.

At minimum:

```text
REQUESTED
APPROVED
REJECTED
DELIVERED
CANCELLED
```

Point deduction associated with a redemption MUST be atomic and auditable.

The system MUST prevent redemption when the player does not have sufficient eligible points.

---

# 16. CQRS

The application MUST use CQRS for relevant use cases.

Commands represent state-changing operations.

Examples:

```text
CreateGame
JoinGame
StartGame
StartRound
SubmitAnswer
WithdrawPlayer
FinishGame
RedeemReward
```

Queries represent read operations.

Examples:

```text
GetGame
GetCurrentRound
GetCurrentQuestion
GetPlayerScore
GetLeaderboard
GetRewards
```

The project MUST NOT introduce MediatR solely to implement CQRS.

Simple explicit command/query handlers SHOULD be preferred.

---

# 17. Domain Events

Important domain changes SHOULD generate domain events.

Examples:

```text
GameStarted
RoundStarted
QuestionPresented
AnswerSubmitted
AnswerEvaluated
PointsAwarded
PlayerLevelAdvanced
PlayerWithdrawn
PlayerEliminated
RoundCompleted
GameFinished
RewardRedeemed
```

Domain events MUST NOT require the Domain layer to know the messaging infrastructure.

---

# 18. Persistence

The primary database implementation MUST use Microsoft SQL Server.

The persistence architecture SHOULD allow Oracle support without modifying Domain or Application.

The database abstraction MUST NOT leak database-specific implementation details into the Domain.

Transactions MUST protect operations that modify multiple related pieces of game state.

---

# 19. Concurrency

The system MUST assume concurrent requests.

Concurrency control MUST be implemented for critical aggregates and operations.

At minimum the implementation MUST protect:

* Game state transitions.
* Round state transitions.
* Answer submissions.
* Score updates.
* Reward redemptions.

Optimistic concurrency SHOULD be preferred where appropriate.

SQL Server SHOULD use a version column such as `rowversion`.

Duplicate commands MUST be safely rejected or treated as idempotent.

---

# 20. Real-Time Communication

The multiplayer experience SHOULD use server-driven real-time communication.

ASP.NET Core SignalR SHOULD be used for:

* Round start notifications.
* Question availability.
* Player answer notifications where appropriate.
* Score updates.
* Leaderboard updates.
* Round completion.
* Game completion.

The authoritative state MUST remain on the server.

SignalR MUST NOT become the source of truth.

---

# 21. Security

The system MUST implement authentication and authorization.

Authorization MUST use policies rather than relying exclusively on UI visibility.

At minimum the system SHOULD distinguish:

```text
ADMIN
GAME_MANAGER
PLAYER
REWARD_MANAGER
```

The API MUST validate all user-controlled identifiers and operations.

The server MUST never trust:

* Player score supplied by the client.
* Answer correctness supplied by the client.
* Game state supplied by the client.
* Remaining time supplied by the client.

---

# 22. Validation

Validation MUST exist at multiple levels.

### API validation

Validates transport and request contracts.

### Application validation

Validates use-case requirements.

### Domain validation

Protects business invariants.

Domain invariants MUST NOT depend exclusively on API validation.

---

# 23. Error Handling

The API MUST expose consistent errors using RFC 7807 Problem Details or an equivalent standardized mechanism.

Expected business failures SHOULD use explicit domain/application errors.

Examples:

```text
GameNotFound
GameAlreadyStarted
InvalidGameState
PlayerNotInGame
QuestionAlreadyAnswered
InvalidAnswer
InsufficientPoints
RewardUnavailable
RewardAlreadyRedeemed
CategoryNotReady
```

Internal implementation details MUST NOT leak through API error responses.

---

# 24. Observability

The application MUST implement structured logging.

Logs SHOULD include:

```text
CorrelationId
TraceId
GameId
PlayerId
RoundId
QuestionId
Command
Duration
Result
```

Sensitive information MUST NOT be logged.

Critical game operations SHOULD be auditable.

---

# 25. Auditability

The following operations SHOULD be auditable:

* Game creation.
* Game configuration.
* Game start.
* Player joining.
* Question selection.
* Answer submission.
* Score changes.
* Player withdrawal.
* Game completion.
* Reward redemption.
* Administrative adjustments.

Audit records MUST be append-oriented and MUST NOT alter historical game decisions.

---

# 26. Testing

The project MUST implement automated tests.

At minimum:

```text
Domain Unit Tests
Application Tests
Integration Tests
API Tests
Architecture Tests
```

Critical game rules MUST have unit tests.

Concurrency-sensitive operations SHOULD have integration tests.

The project SHOULD follow the principle:

```text
Arrange
Act
Assert
```

and use descriptive test names.

---

# 27. Testable Architecture

The architecture MUST allow Domain and Application tests without requiring:

* Web server startup.
* Database connection.
* External API.
* Browser.
* SignalR connection.

Core business rules MUST be executable in isolation.

---

# 28. API Design

The API SHOULD follow REST principles where appropriate.

Endpoints MUST use meaningful resources.

The API MUST NOT expose internal domain entities directly as response contracts.

DTOs SHOULD be used at the application/API boundary.

Pagination MUST be used for potentially large collections.

---

# 29. Database Design

The database MUST enforce appropriate integrity constraints.

Important constraints SHOULD be represented at the database level when appropriate.

Examples:

```text
Exactly one correct answer
Foreign key integrity
Unique question identifiers
Unique reward redemption references
Concurrency version
```

Indexes MUST be created according to actual query patterns.

---

# 30. Frontend Architecture

The web client MUST be treated as a presentation layer.

The frontend MUST NOT implement authoritative game rules.

The frontend MAY provide:

* Countdown visualization.
* Score visualization.
* Current round visualization.
* Leaderboard.
* Answer selection.
* Game state presentation.

The backend remains the authoritative source of truth.

---

# 31. Code Quality

Code MUST favor:

* Explicitness.
* Readability.
* Cohesion.
* Small abstractions.
* Strong typing.
* Immutability where appropriate.
* Dependency inversion.
* Composition over inheritance.

Avoid unnecessary abstractions.

Do not introduce patterns merely to demonstrate patterns.

Every abstraction MUST have a clear architectural or business purpose.

---

# 32. Dependency Management

External libraries MUST be justified.

The project SHOULD prefer native .NET capabilities when they are sufficient.

Libraries MUST NOT be introduced solely because they are popular.

The solution SHOULD minimize unnecessary infrastructure complexity.

---

# 33. Architecture Decision Records

Significant architectural decisions MUST be documented using ADRs.

Examples:

```text
ADR-001 Modular Monolith
ADR-002 Clean Architecture
ADR-003 CQRS without MediatR
ADR-004 SQL Server as primary provider
ADR-005 SignalR for real-time communication
ADR-006 Optimistic concurrency
ADR-007 Point ledger
ADR-008 Question selection strategy
ADR-009 Reward abstraction
```

---

# 34. Specification-Driven Development

All significant functionality MUST begin with a specification.

The development flow MUST follow:

```text
Constitution
      ↓
Specification
      ↓
Clarification
      ↓
Architecture Plan
      ↓
Tasks
      ↓
Implementation
      ↓
Tests
      ↓
Validation
```

A feature MUST NOT be considered complete merely because its code compiles.

It MUST satisfy:

```text
Specification
+
Acceptance Criteria
+
Automated Tests
```

---

# 35. Definition of Done

A feature is complete only when:

* Domain behavior is implemented.
* Application use case is implemented.
* API contract exists when applicable.
* Persistence is implemented when applicable.
* Validation exists.
* Error scenarios are handled.
* Automated tests exist.
* Concurrency concerns have been evaluated.
* Security implications have been evaluated.
* Logging/auditing requirements have been evaluated.
* Documentation/specification is updated.

---

# 36. Non-Functional Requirements

The system SHOULD target:

```text
Availability
Scalability
Security
Observability
Maintainability
Testability
Consistency
Low latency
```

The implementation SHOULD avoid premature distributed-system complexity.

The initial architecture SHOULD be a modular monolith unless a clear requirement justifies service decomposition.

---

# 37. Technical Evaluation Objective

Because OroQuizClash is also a technical assessment project, the implementation SHOULD demonstrate the following competencies:

### Architecture

* Clean Architecture.
* DDD.
* CQRS.
* SOLID.
* Dependency Inversion.
* Modular design.

### Backend

* Modern .NET.
* ASP.NET Core.
* REST API.
* Validation.
* Authentication/Authorization.
* SignalR.
* Concurrency.
* Transactions.

### Data

* Relational modeling.
* SQL Server.
* EF Core.
* Query optimization.
* Transactions.
* Optimistic concurrency.
* Database abstraction.

### Engineering

* Unit testing.
* Integration testing.
* Architecture testing.
* Logging.
* Observability.
* Auditing.
* Error handling.

### Product Thinking

* Configurable rules.
* Extensible game engine.
* Reward lifecycle.
* Multiplayer experience.
* Anti-cheating considerations.
* Idempotency.

---

# 38. Simplicity Principle

The solution MUST NOT become unnecessarily complex merely to demonstrate technical knowledge.

The preferred solution is:

```text
Simple
+
Well Designed
+
Well Tested
+
Extensible
```

rather than:

```text
Complex
+
Distributed
+
Over-engineered
+
Poorly Tested
```

---

# 39. Final Architectural Principle

The most important principle of OroQuizClash is:

> **The game is a domain engine, not a collection of HTTP endpoints.**

Controllers expose capabilities.

Application handlers orchestrate use cases.

Infrastructure persists and integrates.

The Domain owns the rules.

The database preserves state.

The frontend presents the experience.

The real-time infrastructure distributes state changes.

Tests prove that the rules remain correct.

---

**Constitution Status:** Accepted as the architectural baseline for OroQuizClash.

**Next step:** Specifications MUST be created from this constitution before implementation.
