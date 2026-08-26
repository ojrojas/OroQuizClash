Game of random question rounds with gamification and prizes based on collected points.


Architectural Structure of the Game

                    ┌─────────────────────────┐
                    │       QUIZ CLASH        │
                    │   Multiplayer Engine    │
                    └────────────┬────────────┘
                                 │
             ┌───────────────────┼───────────────────┐
             │                   │                   │
             ▼                   ▼                   ▼
       Administration        Game Engine        Rewards Engine
             │                   │                   │
             ▼                   ▼                   ▼
       Categories             Rounds             Points
       Questions              Questions           Prizes
       Answers                Time                Redemption
       Difficulty             Players             History

The key architectural principle would be that game rules are not embedded in controllers.

2. Main Domain Concepts

The initially suggested models are these entities/aggregates/value objects:
* Created with BuildingBlocks.Kernel.Domain and BuildingBlocks.Infrastructure for creating entities, aggregates and value objects, as well as specifications, and there is a reusable EfRepository for repositories to inherit from when they are created.

Catalog
Category
Question
AnswerOption
DifficultyLevel
AcademicLevel
AgeRange
Reward
Game
GamePlayer
GameRound
RoundQuestion
PlayerAnswer
Score
GameResult
Rewards
Reward
RewardRedemption
PointTransaction
Security / Auditing
User
GameAudit
GameEvent

An important separation would be:

Question
   │
   ├── Category
   ├── Difficulty
   ├── AcademicLevel
   ├── AgeRange
   └── AnswerOptions

A Question should not simply have:

Question
 ├── Text
 ├── CorrectAnswer
 └── Category

But something richer:

Question
 ├── Id
 ├── CategoryId
 ├── Text
 ├── Difficulty
 ├── AcademicLevel
 ├── MinimumAge
 ├── MaximumAge
 ├── Status
 ├── Version
 └── AnswerOptions
      ├── A
      ├── B
      ├── C
      └── D
3. Fundamental Rule: exactly 4 answers

A domain rule could be:

Question
 ├── exactly 4 options
 ├── 1 single correct answer
 ├── active question
 ├── active category
 └── defined difficulty

And also:

A category cannot be published if it has fewer than 5 valid questions.

This is much better than simply validating this in Angular.

The domain must protect the invariants.


The rule would be:

QuizArena implements the QuizArena domain; BuildingBlocks implements cross-cutting technical capabilities.

2. Do Not Duplicate BuildingBlocks

In the SPECs we should explicitly forbid creating parallel implementations of:

ICommand
IQuery
ICommandHandler
IQueryHandler
ISender
IPipelineBehavior
IDomainEvent
IDomainEventHandler
IRepository
IUnitOfWork
Specification<T>
AggregateRoot
Entity
ValueObject
StronglyTypedId
Result
Error
IEventBus
IntegrationEvent
IIntegrationEventHandler
Outbox
RabbitMQ EventBus
IEndpoint
GlobalExceptionHandler
OpenTelemetry
Health Checks

This must remain as an architectural constraint of the project.

3. Recommended Structure

With your existing BuildingBlocks, I would do something like this:

QuizArena/
│
├── BuildingBlocks/
│   ├── BuildingBlocks.Kernel.Domain/
│   ├── BuildingBlocks.Kernel.Infrastructure/
│   ├── BuildingBlocks.CQRS/
│   ├── BuildingBlocks.EventBus/
│   ├── BuildingBlocks.EventBus.RabbitMQ/
│   └── BuildingBlocks.ServiceDefaults/
│
├── src/
│   │
│   ├── QuizArena.Domain/
│   │
│   ├── QuizArena.Application/
│   │
│   ├── QuizArena.Infrastructure/
│   │
│   ├── QuizArena.Api/
│   │
│   └── QuizArena.Web/
│
├── tests/
│   ├── QuizArena.Domain.Tests/
│   ├── QuizArena.Application.Tests/
│   ├── QuizArena.Infrastructure.Tests/
│   ├── QuizArena.Api.Tests/
│   └── QuizArena.Architecture.Tests/
│
├── docs/
│   ├── architecture/
│   ├── adr/
│   └── database/
│
└── .specify/
    ├── constitution.md
    ├── specs/
    ├── plans/
    └── tasks/

Although we can even keep the BuildingBlocks in their current workspace and have QuizArena simply reference them via ProjectReference.

4. Vertical Slice

Here there is another important decision.

Instead of:

Application/
    Commands/
    Queries/
    DTOs/
    Validators/
    Handlers/

I want QuizArena to follow exactly your BuildingBlocks approach:

QuizArena.Application/

Features/
│
├── Games/
│   ├── CreateGame.cs
│   ├── JoinGame.cs
│   ├── StartGame.cs
│   ├── StartRound.cs
│   ├── SubmitAnswer.cs
│   ├── WithdrawPlayer.cs
│   └── GetGame.cs
│
├── Categories/
│   ├── CreateCategory.cs
│   ├── UpdateCategory.cs
│   ├── PublishCategory.cs
│   └── GetCategories.cs
│
├── Questions/
│   ├── CreateQuestion.cs
│   ├── UpdateQuestion.cs
│   ├── PublishQuestion.cs
│   └── GetQuestions.cs
│
└── Rewards/
    ├── CreateReward.cs
    ├── RedeemReward.cs
    └── GetRewards.cs

And each feature locally contains:

Command
Validator
Handler
DTO
Endpoint

This fits perfectly with the CQRS and IEndpoint you already have.

5. Example of What a Slice Should Look Like

For example:

Features/Games/SubmitAnswer.cs

conceptually:

public sealed record SubmitAnswerCommand(
    Guid GameId,
    Guid RoundId,
    Guid QuestionId,
    Guid AnswerOptionId,
    Guid IdempotencyKey)
    : ICommand<Result<SubmitAnswerResponse>>;

Validator:

public sealed class SubmitAnswerValidator
    : Validator<SubmitAnswerCommand>
{
    public SubmitAnswerValidator()
    {
        RuleFor(
            x => x.GameId != Guid.Empty,
            nameof(SubmitAnswerCommand.GameId),
            "GameId is required.");

        RuleFor(
            x => x.RoundId != Guid.Empty,
            nameof(SubmitAnswerCommand.RoundId),
            "RoundId is required.");

        RuleFor(
            x => x.AnswerOptionId != Guid.Empty,
            nameof(SubmitAnswerCommand.AnswerOptionId),
            "AnswerOptionId is required.");
    }
}

Handler:

public sealed class SubmitAnswerHandler(
    IRepository<Game, GameId> games,
    IUnitOfWork unitOfWork)
    : ICommandHandler<
        SubmitAnswerCommand,
        Result<SubmitAnswerResponse>>
{
    public async Task<Result<SubmitAnswerResponse>> HandleAsync(
        SubmitAnswerCommand command,
        CancellationToken ct)
    {
        // load aggregate
        // execute domain behavior
        // save
        // return result

        return ...;
    }
}

And endpoint:

public sealed class SubmitAnswerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/games/{gameId}/answers",
            async (
                Guid gameId,
                SubmitAnswerCommand command,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.SendAsync(
                    command with { GameId = gameId },
                    ct);

                return result.ToOkResult();
            });
    }
}

The advantage is that the test project demonstrates that you know how to use and extend an existing architectural platform, which is a very important skill in enterprise environments.

6. BuildingBlocks → QuizArena Responsibility

I would document this matrix:

BuildingBlock	QuizArena uses it for
Kernel.Domain	Aggregates, entities, Value Objects, Result, rules, specifications
CQRS	Commands, Queries, handlers, behaviors
EventBus	Integration Events
EventBus.RabbitMQ	Asynchronous communication
Kernel.Infrastructure	EF Core, repositories, UoW, Outbox
ServiceDefaults	OTel, health, endpoints, errors, resilience

And QuizArena implements:

QuizArena	Responsibility
Game	Game rules
GameRound	Round rules
Question	Question bank
Category	Classification
Score	Scoring rules
Reward	Rewards catalog
RewardRedemption	Redemption
QuestionSelectionStrategy	Selection
DifficultyStrategy	Progression
LossPolicy	Risk
ConsolationPolicy	Consolation
7. Domain Events vs Integration Events

Here your BuildingBlocks are especially important.

We have:

DOMAIN EVENT

for internal things:

AnswerEvaluatedDomainEvent
PointsAwardedDomainEvent
PlayerWithdrawnDomainEvent
RoundCompletedDomainEvent
GameFinishedDomainEvent

And:

INTEGRATION EVENT

to go outside the module/application:

GameStartedIntegrationEvent
GameFinishedIntegrationEvent
RewardRedeemedIntegrationEvent
PlayerRewardGrantedIntegrationEvent

The architecture would be:

                 Game Aggregate
                       │
                       ▼
                Domain Event
                       │
                       ▼
             AppDbContextBase
                       │
                       ├── Domain Event Dispatcher
                       │
                       └── Outbox
                              │
                              ▼
                       RabbitMQ EventBus
                              │
                     Integration Event
                              │
                 ┌────────────┼────────────┐
                 ▼            ▼            ▼
              Rewards      Analytics     Notifications

And this is exactly where we must not invent another EventBus.

8. Outbox

For QuizArena, the combination:

EF Core
+
AppDbContextBase
+
IOutboxWriter
+
OutboxProcessor
+
RabbitMQ

gives us a fairly solid architecture.

For example:

StartGame
   │
   ▼
Game.Start()
   │
   ├── GameStartedDomainEvent
   │
   ▼
SaveChanges
   │
   ├── DB transaction
   │      ├── Game
   │      └── Outbox
   │
   ▼
OutboxProcessor
   │
   ▼
RabbitMQ

This way we avoid the classic problem:

DB commit → OK
RabbitMQ → ERROR

and we lose the event.

9. Multi-targeting net10

I would also include this as a project constraint.

QuizArena should follow the same target:

<TargetFrameworks>net10.0;</TargetFrameworks>

and not create different logic for each framework unless there really is an incompatibility.

The matrix would be:

                       net10.0       
                          │             
                          ▼             
                 ┌─────────────────────────┐
                 │      BuildingBlocks     │
                 └────────────┬────────────┘
                              │
                              ▼
                            QuizArena

This is also very good for a technical assessment because it demonstrates that you know how to work with shared multi-target libraries.



4. Difficulty Model

Here you can demonstrate quite a lot of design knowledge.

I would not use only:

Easy
Medium
Hard
Expert

I would use a multidimensional model.

For example:

DifficultyProfile
-----------------
Complexity: 1..10
AcademicLevel
AgeRange
KnowledgeDomain
CognitiveLevel

For example:

Level	Age	Academic	Complexity
Basic	8-12	Primary	1-2
Initial	13-15	Secondary	3-4
Intermediate	16-18	High School	5-6
Advanced	18+	University	7-8
Expert	18+	Specialization	9-10

But this must be configurable, not hardcoded.

5. Round Engine

The Game could have a configuration:

GameConfiguration

MinimumRounds = 5
MaximumRounds = 10
InitialDifficulty = 1
DifficultyIncrement = 1
SecondsPerQuestion = 30
PointsPerLevel = ...
AllowWithdrawal = true

Then:

Round 1 → difficulty 1
Round 2 → difficulty 2
Round 3 → difficulty 3
Round 4 → difficulty 4
Round 5 → difficulty 5

But it does not necessarily have to be linear.

You could have a strategy:

DifficultyProgressionStrategy

with implementations:

LinearDifficultyStrategy
ProgressiveDifficultyStrategy
AdaptiveDifficultyStrategy

This is excellent for demonstrating extensibility.

6. Complete Flow

The game could work like this:

CONFIGURE GAME
      │
      ▼
REGISTER PLAYERS
      │
      ▼
START GAME
      │
      ▼
┌───────────────────────┐
│      ROUND N          │
│                       │
│ Select question       │
│        ↓              │
│ Show question         │
│        ↓              │
│ Players answer        │
│        ↓              │
│ Validate answers      │
│        ↓              │
│ Calculate points      │
│        ↓              │
│ Update ranking        │
└───────────┬───────────┘
            │
            ▼
     Is it the last round?
       /          \
     NO            YES
      │             │
      ▼             ▼
Increase level   WINNER
      │             │
      └──────┐      ▼
             │   PRIZES
             │
             ▼
       Continue?
        /       \
      YES         NO
      │           │
      ▼           ▼
 Next          Withdrawal
 round             │
                   ▼
             Redeem points
7. The Most Interesting Rule: risk / reward

This is probably the most important part of your domain.

The player has:

CurrentScore
SecuredScore
PotentialScore

For example:

Round 1
100 points

Round 2
250 points

Round 3
500 points

Round 4
1,000 points

Round 5
2,500 points

But you can introduce the concept of secured points.

Example:

Player:

Current points:       1,000
Secured points:         500

If they decide to withdraw:

Prize = 500 points

If they continue:

Correct answer
→ 2,000 points

Incorrect answer
→ loses unsecured points
→ keeps 500

Or, if you want to literally respect your rule:

Incorrect answer
→ Score = 0
→ Prize = Consolation

I would make this configurable:

LossPolicy

LOSE_ALL
LOSE_CURRENT_ROUND
LOSE_UNSECURED_POINTS
FALLBACK_TO_CHECKPOINT

That turns a fixed business rule into a Game Rule Engine.

8. Game States

The Game aggregate should have an explicit state machine:

DRAFT
  ↓
READY
  ↓
WAITING_FOR_PLAYERS
  ↓
IN_PROGRESS
  ↓
ROUND_IN_PROGRESS
  ↓
ROUND_COMPLETED
  ↓
NEXT_ROUND
  ↓
FINISHED

Terminal states:

FINISHED
CANCELLED
FORCED_FINISHED
PLAYER_WITHDRAWN
PLAYER_ELIMINATED

This is very important.

You should not allow:

Game.Finish()

from any state.

9. Domain Events

Here you can greatly elevate the quality of the assessment.

Examples:

GameCreated
PlayerJoinedGame
GameStarted
RoundStarted
QuestionPresented
AnswerSubmitted
AnswerEvaluated
PlayerScored
PlayerLevelAdvanced
PlayerWithdrew
PlayerLostScore
RoundCompleted
GameFinished
RewardRedeemed

For example:

AnswerSubmitted
       │
       ▼
AnswerEvaluated
       │
       ├── Correct
       │      ↓
       │   PointsAwarded
       │
       └── Incorrect
              ↓
         ScoreLost

This also allows you to later implement:

Kafka
RabbitMQ
Azure Service Bus
AWS SNS/SQS

without polluting the domain.

10. CQRS BuildingBlocks.CQRS

For this technical assessment I would use CQRS.

Commands
CreateCategoryCommand
CreateQuestionCommand
PublishCategoryCommand

CreateGameCommand
JoinGameCommand
StartGameCommand

StartRoundCommand
SubmitAnswerCommand
WithdrawFromGameCommand

FinishGameCommand

CreateRewardCommand
RedeemRewardCommand
Queries
GetCategoriesQuery
GetCategoryQuery
GetQuestionsQuery

GetGameQuery
GetCurrentRoundQuery
GetCurrentQuestionQuery

GetLeaderboardQuery
GetPlayerScoreQuery

GetAvailableRewardsQuery
GetRewardRedemptionsQuery

And following your usual preference, without MediatR.

You can build a small CQRS dispatcher via interfaces:

public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> Handle(
        TCommand command,
        CancellationToken cancellationToken);
}
11. Concurrency: here you can earn many points in the assessment

Being multiplayer, it is not enough to do:

_db.SaveChanges();

Imagine:

Player A ──► answers
Player B ──► answers
Player C ──► answers

simultaneously.

You need to protect:

Game
Round
PlayerScore
Question

against race conditions.

You can use:

SQL Server
rowversion
Oracle
ROW SCN / version column

And from EF Core:

[Timestamp]
public byte[] RowVersion { get; private set; }

You can also use an idempotency rule:

AnswerSubmissionId

to prevent the same answer from being processed twice.

12. Random Question

I would not do:

.OrderBy(x => Guid.NewGuid())

for a serious system.

You can create:

IQuestionSelectionStrategy

with:

RandomQuestionSelectionStrategy
DifficultyAwareQuestionSelectionStrategy
AdaptiveQuestionSelectionStrategy

And the selection must consider:

Category
Difficulty
AcademicLevel
AgeRange
AlreadyUsedQuestions
Game
Round

For example:

Category = Mathematics

Difficulty = 5

Available questions:
Q1
Q2
Q3
Q4
Q5
Q6
Q7

Used:
Q2
Q5

Candidate:
Q1
Q3
Q4
Q6
Q7

This way you avoid repeating questions within the same game.

13. Response Time

I would add:

TimeLimitSeconds

For example:

30 seconds

And an answer would have:

PlayerAnswer
-------------------
Id
GameId
RoundId
QuestionId
PlayerId
SelectedOptionId
SubmittedAt
ElapsedMilliseconds
IsCorrect
PointsAwarded

The answer must be determined using the server as the time authority, never the browser.

14. Points System

I would not do:

player.Points += 100;

directly.

I would create a ledger:

PointTransaction

Id
PlayerId
GameId
Type
Points
Reference
CreatedAt

Types:

ANSWER_CORRECT
ANSWER_INCORRECT
ROUND_BONUS
LEVEL_BONUS
GAME_BONUS
WITHDRAWAL
PENALTY
REWARD_REDEMPTION
CONSOLATION
ADJUSTMENT

Then you can reconstruct the balance:

Initial
 + Correct answers
 + Bonuses
 - Penalties
 - Redemptions
 = Current balance

This is much more defensible architecturally.

15. Rewards

The reward should not be coupled to the game.

Reward
----------------
Id
Name
Description
PointsRequired
Stock
Status
ExpirationDate

And:

RewardRedemption
----------------
Id
PlayerId
RewardId
Points
Status
RequestedAt
ApprovedAt
DeliveredAt

States:

REQUESTED
VALIDATING
APPROVED
REJECTED
DELIVERED
CANCELLED

This way you can later support:

Physical prizes
Coupons
Gift cards
Money
Benefits

without modifying the game engine.

For the technical assessment, I would keep money as a reward/prize abstraction and avoid implementing real payments. If it becomes a commercial product, then regulation, promotion terms, taxes and gambling rules by jurisdiction would need to be studied.

16. Consolation

I would also turn this into a rule:

ConsolationPolicy

Example:

If player participates
and answers at least one question
and finishes without a prize:

→ deliver ConsolationReward

It could be:

10 points

or:

Badge
Coupon
Free entry
17. Architecture I Would Use

For the assessment:

OroQuizClash.sln

src/
 ├── OroQuizClash.Domain
 ├── OroQuizClash.Application
 ├── OroQuizClash.Infrastructure
 ├── OroQuizClash.Web
 └── OroQuizClash.Api

tests/
 ├── OroQuizClash.Domain.Tests
 ├── OroQuizClash.Application.Tests
 ├── OroQuizClash.Infrastructure.Tests
 └── OroQuizClash.Architecture.Tests

If you want to demonstrate even more:

OroQuizClash.Realtime
OroQuizClash.Workers

but I would not add microservices for the sake of it.

For a technical assessment, a well-designed modular monolith can demonstrate more maturity than 10 empty microservices.

18. Clean + Hexagonal + DDD

The dependency:

                 ┌───────────────────┐
                 │ OroQuizClash.Web  │
                 └─────────┬─────────┘
                           │
                 ┌─────────▼─────────┐
                 │ Application       │
                 │ CQRS / Use Cases  │
                 └─────────┬─────────┘
                           │
                 ┌─────────▼─────────┐
                 │ Domain            │
                 │ Rules / Entities  │
                 └───────────────────┘
                           ▲
                           │
                 ┌─────────┴─────────┐
                 │ Infrastructure    │
                 │ EF / SQL / Oracle │
                 └───────────────────┘

The domain must not know that EF Core, SQL Server, Oracle, ASP.NET or Angular exist.

19. Database

The assessment says:

MS SQL Server or Oracle.

I would implement SQL Server as the main provider, but design Infrastructure so that Domain and Application are agnostic.

Domain
   ↓
Application
   ↓
Infrastructure.Abstractions
   ↓
Infrastructure.SqlServer

And potentially:

Infrastructure.Oracle

The assessment then demonstrates that you understand:

Database Independence

without falling into the trap of trying to fully support two databases from day one.

20. Web

For modern .NET:

ASP.NET Core Web API
+
Angular
+
SignalR

SignalR would be especially interesting.

Flow:

             Game Server
                 │
       ┌─────────┼─────────┐
       │         │         │
       ▼         ▼         ▼
    Player A  Player B  Player C

Events:

RoundStarted
QuestionAvailable
PlayerAnswered
PlayerScoreUpdated
LeaderboardUpdated
RoundFinished
GameFinished

That really turns the system into multiplayer.

21. API

Example:

POST /api/games
POST /api/games/{gameId}/players
POST /api/games/{gameId}/start

GET /api/games/{gameId}

GET /api/games/{gameId}/rounds/current
GET /api/games/{gameId}/questions/current

POST /api/games/{gameId}/answers

POST /api/games/{gameId}/withdraw

GET /api/games/{gameId}/leaderboard

GET /api/rewards
POST /api/rewards/{rewardId}/redeem

Administration:

POST /api/categories
PUT /api/categories/{id}
POST /api/categories/{id}/publish

POST /api/questions
PUT /api/questions/{id}
DELETE /api/questions/{id}
22. Security

For a senior-level assessment I would include:

Authentication
Authorization
Roles
Policies
Rate limiting
Input validation
Idempotency
Audit
Correlation ID
Structured logging
Global exception handling
ProblemDetails

Roles:

ADMIN
GAME_MANAGER
PLAYER
REWARD_MANAGER

And policies:

Category.Read
Category.Write

Question.Read
Question.Write
Question.Publish

Game.Create
Game.Start
Game.Play

Reward.Read
Reward.Redeem
Reward.Manage
23. Testing

Here you can make the assessment stand out a lot.

Unit tests

Especially:

Game.Start()
Game.StartRound()
Game.SubmitAnswer()
Game.Withdraw()
Game.Finish()
Game.AdvanceLevel()

Example:

Given:
    player has 1,000 points

When:
    player answers incorrectly

Then:
    score must become 0
    player must not redeem reward
    consolation reward must be available
Integration tests
API
+
EF Core
+
SQL Server
Architecture tests

Validate:

Domain → no Infrastructure dependency
Domain → no ASP.NET dependency
Application → no Web dependency
Concurrency tests

Especially:

Two submissions
same player
same round
same question

There must be only one valid evaluation.

24. SDD / SpecKit

I would structure the project as:

.specify/
 ├── constitution.md
 ├── specs/
 │    ├── SPEC-001-game-configuration.md
 │    ├── SPEC-002-categories.md
 │    ├── SPEC-003-question-bank.md
 │    ├── SPEC-004-game-lifecycle.md
 │    ├── SPEC-005-round-engine.md
 │    ├── SPEC-006-answer-evaluation.md
 │    ├── SPEC-007-scoring.md
 │    ├── SPEC-008-player-withdrawal.md
 │    ├── SPEC-009-rewards.md
 │    ├── SPEC-010-consolation.md
 │    ├── SPEC-011-multiplayer.md
 │    ├── SPEC-012-realtime.md
 │    ├── SPEC-013-security.md
 │    ├── SPEC-014-audit.md
 │    └── SPEC-015-reporting.md
 │
 ├── plans/
 └── tasks/

And also:

docs/
 ├── architecture/
 ├── adr/
 ├── api/
 └── database/
