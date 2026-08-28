# UI/UX Constitution Addendum

## 1. UI/UX Is a First-Class Architecture Concern

The user interface is part of the product architecture and MUST be specified using SDD/SpecKit.

UI implementation MUST NOT be treated as an implementation detail that can be freely designed after backend development.

All major user experiences MUST have an associated specification.

---

## 2. UI/UX Pro Max

QuizArena MUST use the `ui-ux-pro-max` skill as a design intelligence aid for UI/UX development.

The skill SHOULD be used for:

- Design system generation.
- Visual style selection.
- Color palette selection.
- Typography.
- UX patterns.
- Responsive behavior.
- Accessibility.
- Interaction patterns.
- Animation guidance.
- Component design.
- UI quality review.

The generated Design System MUST be persisted as project documentation and treated as the visual source of truth.

---

## 3. Design System First

The implementation of major UI surfaces MUST NOT begin before the Design System has been established.

The design process SHOULD follow:

```text
Product Requirements
       ↓
UX Analysis
       ↓
UI/UX Pro Max Design System
       ↓
Visual Direction
       ↓
Information Architecture
       ↓
Interaction Design
       ↓
Component Design
       ↓
Screen Design
       ↓
Implementation
       ↓
UX Review
```

---

## 4. Player Experience

QuizArena MUST provide an immersive game-show-inspired experience.

The player interface SHOULD communicate:

- Competition.
- Progression.
- Risk.
- Reward.
- Achievement.
- Tension.
- Clarity.

The interface MAY be inspired by the interaction principles of televised knowledge competitions, but MUST NOT copy the visual identity, branding, assets, sounds, layouts, or copyrighted design of an existing show.

---

## 5. Cinematic Game Experience

The active game screen MUST be treated as the primary product experience.

The game screen SHOULD provide:

- Clear question hierarchy.
- Four answer options.
- Visible progression.
- Current level.
- Current points.
- Secured points.
- Potential reward.
- Countdown.
- Player status.
- Optional leaderboard.
- Withdrawal action.
- Clear feedback after answer evaluation.

Visual effects SHOULD enhance the emotional experience without reducing usability.

---

## 6. Motion

Animations SHOULD communicate state changes rather than exist merely for decoration.

The application MUST respect:

```text
prefers-reduced-motion
```

Animations MUST NOT block the player's ability to answer within the configured time.

---

## 7. Responsive Design

Player experiences MUST support at minimum:

```text
375px
768px
1024px
1440px
```

Layouts MUST adapt rather than simply scale.

The game screen MUST preserve:

- Question readability.
- Answer accessibility.
- Timer visibility.
- Score visibility.
- Primary action visibility.

---

## 8. Accessibility

Interactive elements MUST provide:

- Keyboard accessibility where applicable.
- Visible focus state.
- Sufficient contrast.
- Accessible labels.
- Non-color-only feedback.
- Reduced motion support.
- Touch-friendly targets.

The UI/UX Pro Max pre-delivery checklist SHOULD be incorporated into the project's UI review process.

---

## 9. UI States

Every interactive screen MUST explicitly define its states.

At minimum:

```text
Loading
Ready
Empty
Error
Disabled
Active
Selected
Success
Failure
Processing
Completed
```

Game-specific screens MUST additionally define:

```text
QuestionActive
AnswerSelected
AnswerLocked
Evaluating
Correct
Incorrect
Timeout
RoundCompleted
WithdrawConfirmation
Withdrawn
Winner
Eliminated
Consolation
```

---

## 10. Separation of Experiences

QuizArena MUST distinguish between:

```text
Player Experience
Administration Experience
```

The player experience SHOULD be immersive and emotionally engaging.

The administration experience SHOULD prioritize:

- Productivity.
- Information density.
- Data clarity.
- Accessibility.
- Fast navigation.
- Operational efficiency.

Both MUST share the same underlying Design System.

---

## 11. Realtime UI

Realtime events MUST drive UI state transitions without becoming the source of truth.

The architecture MUST follow:

```text
Backend State
     ↓
Realtime Event
     ↓
Client State
     ↓
UI
```

The client MUST NOT infer authoritative game state solely from animations or local timers.

---

## 12. Visual Quality Gate

A UI feature MUST NOT be considered complete merely because it compiles.

UI acceptance MUST include:

```text
Functional correctness
Visual consistency
Responsive behavior
Accessibility
Interaction feedback
Animation behavior
Loading states
Error states
Empty states
Reduced-motion behavior
```

---

## 13. Anti-Pattern

The following are prohibited unless explicitly justified:

```text
Generic Bootstrap-like UI
Default component-library appearance
Unstyled forms
Random gradients
Excessive glassmorphism
Excessive neon
Emoji used as primary icons
Unnecessary animations
Inconsistent spacing
Inconsistent typography
Hidden loading states
Missing error states
Mobile layouts that are desktop layouts compressed
```

---

## 14. Design Source of Truth

The project MUST maintain a persisted Design System.

Conceptually:

```text
design-system/
├── MASTER.md
├── components/
├── screens/
├── tokens/
└── overrides/
```

The Design System MUST be updated through the SDD process when major visual decisions change.

---

## 15. Definition of Done

A UI feature is complete only when:

- Its corresponding SPEC requirements are satisfied.
- Its Design System rules are respected.
- Its responsive behavior is implemented.
- Its accessibility requirements are satisfied.
- Its interaction states are implemented.
- Its loading/error/empty states are implemented.
- Its animations respect reduced-motion preferences.
- Its visual implementation has passed UI/UX review.


                         QUIZARENA
                            │
              ┌─────────────┴─────────────┐
              │                           │
              ▼                           ▼
       ADMINISTRATION APP           PLAYER GAME APP
              │                           │
          Blazor Web                 Angular 22
              │                           │
              ▼                           ▼
       Configuración                 Experiencia
       Operación                     de juego
       Preguntas                     Multiplayer
       Categorías                    Realtime
       Premios                       Score
       Reportes                      Rewards
       Auditoría

Angular 22 es actualmente la versión major activa de Angular, publicada el 3 de junio de 2026; por tanto, para este proyecto debemos especificar Angular 22.x, no simplemente “Angular latest”.

Además, la ui-ux-pro-max actual contempla Angular entre sus stacks y su flujo recomienda generar primero el Design System y persistirlo como design-system/MASTER.md, con overrides por página cuando sea necesario.

1. Nueva arquitectura de aplicaciones

Quedaría así:

QuizArena/
│
├── src/
│
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Kernel.Domain/
│   │   ├── BuildingBlocks.CQRS/
│   │   ├── BuildingBlocks.EventBus/
│   │   ├── BuildingBlocks.EventBus.RabbitMQ/
│   │   ├── BuildingBlocks.Kernel.Infrastructure/
│   │   └── BuildingBlocks.ServiceDefaults/
│   │
│   ├── Backend/
│   │   │
│   │   ├── QuizArena.Api/
│   │   ├── QuizArena.Application/
│   │   ├── QuizArena.Domain/
│   │   └── QuizArena.Infrastructure/
│   │
│   ├── Admin/
│   │   └── QuizArena.Admin/
│   │       └── Blazor Web App
│   │
│   └── Player/
│       └── QuizArena.Player/
│           └── Angular 22
│
├── tests/
│
├── specs/
│
└── design-system/
    ├── MASTER.md
    └── pages/

Hay una decisión arquitectónica importante aquí:

Backend

No haría:

Blazor → DB
Angular → DB

sino:

                  ┌───────────────┐
                  │ QuizArena API │
                  └───────┬───────┘
                          │
             ┌────────────┴────────────┐
             │                         │
             ▼                         ▼
        Blazor Admin             Angular Player

Las dos aplicaciones consumen el backend mediante contratos/API.

Eso hace mucho más fuerte la prueba técnica.

2. Responsabilidad de cada aplicación
QuizArena Admin — Blazor

Esta aplicación es para:

ADMIN
GAME MANAGER
QUESTION MANAGER
REWARD MANAGER
REPORT USER

Debe permitir:

Dashboard
Games
Game Configuration
Categories
Question Bank
Rewards
Players
Live Games
Reports
Audit
Tecnología
.NET 11
Blazor Web App
Interactive Server

No utilizaría Blazor como aplicación de juego.

Blazor será la aplicación administrativa/operacional.

3. QuizArena Player — Angular 22

Esta es la aplicación que verá cada participante.

Y aquí hay que corregir algo fundamental de los SPEC anteriores.

No debemos diseñar:

Una pantalla compartida para todos los jugadores

sino:

Game Session
      │
      ├── Player A → Angular Session A
      │
      ├── Player B → Angular Session B
      │
      ├── Player C → Angular Session C
      │
      └── Player N → Angular Session N

Cada jugador tiene su propio estado de presentación.

4. Cada jugador tiene su propia pantalla

Esto debe convertirse en una regla explícita.

Por ejemplo:

GAME-001
Category: Science
Round: 4

Tenemos:

Player A
Score: 1,250
Answer: B
Timer: 08s

Player B
Score: 750
Answer: D
Timer: 08s

Player C
Score: 2,500
Answer: -
Timer: 08s

Todos están en:

Round 4
Question X

pero cada cliente Angular mantiene su propio estado de jugador.

El servidor mantiene el estado autoritativo.

                 SERVER
                    │
          ┌─────────┼─────────┐
          │         │         │
          ▼         ▼         ▼
       Player A  Player B  Player C
          │         │         │
       Angular    Angular   Angular
          │         │         │
       Screen A  Screen B  Screen C

Esto es esencial para multiplayer.

5. Actualización de SPEC existentes

No solamente debemos agregar SPEC-016+.

Hay que corregir algunos SPEC anteriores.

SPEC-004 — Game Lifecycle

Debe incluir explícitamente:

Game
 ├── Game Configuration
 ├── Game Session
 ├── Players
 ├── Rounds
 └── Game State

Y distinguir:

Game

de:

PlayerGameSession
6. Actualización SPEC-011 — Multiplayer

Este SPEC debe ampliarse.

Debe definir:

Game Session
GameSession
 ├── GameId
 ├── PlayerId
 ├── ConnectionId
 ├── Status
 ├── CurrentRound
 ├── CurrentQuestion
 ├── Score
 ├── SecuredPoints
 └── AnswerState
Regla fundamental

Cada jugador tiene una sesión lógica independiente dentro del mismo juego.

Por ejemplo:

Game #123
│
├── Session #A → Oscar
├── Session #B → Player B
├── Session #C → Player C
└── Session #D → Player D
7. Actualización SPEC-012 — Realtime

Aquí también hay que hacer una modificación importante.

No todos los eventos deben ser broadcast.

Debemos tener:

GLOBAL EVENTS

y:

PLAYER-SPECIFIC EVENTS
Globales
GameStarted
RoundStarted
RoundCompleted
GameFinished
Player-specific
PlayerQuestionPresented
PlayerAnswerAccepted
PlayerAnswerEvaluated
PlayerScoreUpdated
PlayerWithdrawalAccepted
PlayerEliminated
PlayerRewardAvailable

Por ejemplo:

RoundStarted
      │
      ├────► Player A
      ├────► Player B
      ├────► Player C
      └────► Player D

pero:

PlayerScoreUpdated(A)

solamente:

        └────► Player A

Esto además será una excelente demostración de conocimiento de SignalR + grupos + targeting de conexiones.

8. Nueva estructura de SPEC

Con las modificaciones, yo dejaría:

SPEC-001  Game Configuration
SPEC-002  Categories
SPEC-003  Question Bank
SPEC-004  Game Lifecycle
SPEC-005  Round Engine
SPEC-006  Answer Evaluation
SPEC-007  Scoring
SPEC-008  Player Withdrawal
SPEC-009  Rewards
SPEC-010  Consolation
SPEC-011  Multiplayer
SPEC-012  Realtime
SPEC-013  Security
SPEC-014  Audit
SPEC-015  Reporting

SPEC-016-ui-ux-design-system

# ADMINISTRACIÓN — BLAZOR
SPEC-017-admin-application
SPEC-018-admin-dashboard
SPEC-019-admin-game-configuration
SPEC-020-admin-categories
SPEC-021-admin-question-bank
SPEC-022-admin-game-operations
SPEC-023-admin-rewards
SPEC-024-admin-players
SPEC-025-admin-reporting
SPEC-026-admin-audit

# JUEGO — ANGULAR 22
SPEC-027-player-application
SPEC-028-player-lobby
SPEC-029-player-game
SPEC-030-player-rounds
SPEC-031-player-answering
SPEC-032-player-scoring
SPEC-033-player-multiplayer
SPEC-034-player-results
SPEC-035-player-withdrawal
SPEC-036-player-rewards

Yo agregaría SPEC-021 porque ahora tenemos una aplicación administrativa real y no solamente un “portal”.

9. SPEC-016 — UI/UX Design System

Este debe ser el primer SPEC que vas a desarrollar ahora.

Y no debe empezar diseñando pantallas.

Debe empezar por:

PRODUCT
   ↓
UX CHARACTERISTICS
   ↓
VISUAL DIRECTION
   ↓
DESIGN SYSTEM
   ↓
COMPONENTS
   ↓
APPLICATIONS

La skill ui-ux-pro-max actualmente tiene un generador de Design System que analiza producto/industria y devuelve patrón, estilo, colores, tipografía, efectos y anti-patterns; además permite persistir MASTER.md y overrides por página.

10. SPEC-016 debe contemplar dos Design Systems relacionados

No dos sistemas totalmente independientes.

              QUIZARENA DESIGN SYSTEM
                       │
             MASTER DESIGN SYSTEM
                       │
          ┌────────────┴────────────┐
          │                         │
          ▼                         ▼
    ADMIN OVERRIDES            PLAYER OVERRIDES
       Blazor                    Angular
Master
Colors
Typography
Spacing
Radius
Elevation
Motion
Icons
Accessibility
Buttons
Inputs
Cards
Dialogs
Tables
Notifications
Player override
Cinematic
Game-show
Immersive
High emotional feedback
Large typography
Countdown
Progression
Score
Reward
Celebration
Admin override
Professional
Dense
Data-oriented
Productivity
Tables
Forms
Filters
Dashboards
CRUD
11. Design System recomendado

La skill debe decidir la combinación definitiva mediante su proceso de generación, no debemos imponerle artificialmente una paleta antes de ejecutarla.

Pero sí debemos definir el objetivo:

Product:
Multiplayer Trivia Game Platform

Industry:
Gaming / Education / Rewards

Player style:
Premium cinematic game-show experience

Admin style:
Modern enterprise SaaS administration

Mood:
Exciting
Premium
Trustworthy
Competitive
Immersive
Accessible

Avoid:
Generic SaaS
Generic Bootstrap
AI purple gradients
Excessive glass
Excessive neon
Clutter

Y generar el Design System usando la skill.

12. Prompt de Design System para SPEC-016

Este sería el input inicial que utilizaría con UI/UX Pro Max:

QuizArena is a premium multiplayer trivia game platform.

It has two independent web applications sharing one design system:

1. Player Application:
Angular 22.
The player experience is a cinematic, immersive, premium game-show interface.
Each player has their own private game screen and private game state.
Players answer multiple-choice questions, see a countdown, progress through increasing difficulty levels, accumulate points, decide whether to continue or withdraw, compete with other players, and redeem rewards.

2. Administration Application:
Blazor Web App on .NET 11.
The administration experience is a professional enterprise dashboard used to configure games, categories, questions, difficulty levels, scoring, rewards, players, live games, reports and audit information.

Visual inspiration:
premium televised knowledge competitions, modern interactive game shows, cinematic competition interfaces.

Do NOT copy the visual identity, branding, graphics, sounds, layouts or assets of any existing television show.

Player experience:
cinematic, immersive, exciting, premium, competitive, dramatic but usable.

Administration experience:
professional, efficient, information-dense, modern SaaS, accessible.

The design must prioritize:
- accessibility
- responsive design
- keyboard navigation where applicable
- touch-friendly interactions
- high contrast
- clear hierarchy
- meaningful animation
- reduced-motion support
- clear loading, error and empty states
- consistent design tokens
- reusable components
- responsive layouts

Avoid:
- generic Bootstrap appearance
- generic SaaS templates
- excessive glassmorphism
- excessive neon
- random gradients
- AI purple/pink gradients
- excessive animation
- decorative elements that compete with the question
- emoji as primary icons

Generate a complete design system with:
- visual style
- color palette
- typography
- spacing
- radius
- elevation
- motion
- iconography
- buttons
- cards
- forms
- dialogs
- tables
- navigation
- notifications
- game controls
- answer options
- timers
- progress indicators
- score indicators
- reward components
- leaderboard
- responsive rules
- accessibility rules
- player-specific game states
- admin-specific states

Technology:
Angular 22 for Player.
Blazor Web App for Administration.

The design system must be persisted as:
design-system/MASTER.md

Create page/application-specific overrides for:
- player-home
- game-lobby
- game-screen
- game-results
- rewards
- admin-dashboard
- game-configuration
- categories
- question-bank
- live-games
- reports
13. SPEC-017 — Player Application

Ahora debe decir explícitamente:

Esta especificación define una aplicación Angular 22 independiente responsable de la experiencia del jugador.

Screens
/player
/player/login
/player/home
/player/games
/player/lobby/:gameId
/player/game/:gameId
/player/results/:gameId
/player/rewards
/player/rewards/:rewardId
/player/redemptions
/player/profile
14. SPEC-018 — Game Experience

Debe definir específicamente:

ONE PLAYER
     ↓
ONE ANGULAR APPLICATION
     ↓
ONE GAME SESSION
     ↓
ONE PRIVATE GAME SCREEN

Mientras varios jugadores están simultáneamente jugando:

                    GAME SERVER
                        │
          ┌─────────────┼─────────────┐
          │             │             │
          ▼             ▼             ▼
      Session A      Session B      Session C
          │             │             │
          ▼             ▼             ▼
      Angular A      Angular B      Angular C
          │             │             │
        Screen A     Screen B     Screen C

La UI debe poder mostrar información pública como:

Leaderboard
Current round
Players remaining

pero información privada:

My answer
My score
My secured points
My timer
My withdrawal
My reward

debe pertenecer exclusivamente al jugador correspondiente.

15. SPEC-019 — Administration Application

Debe cambiar de:

Admin Portal

a:

Administration Application

y establecer:

Technology:
Blazor Web App
.NET 11
Módulos
Dashboard
Game Management
Game Configuration
Game Templates
Categories
Question Bank
Question Validation
Difficulty Management
Players
Live Games
Rewards
Redemptions
Reports
Audit
System Configuration
16. SPEC-020 — Rewards Experience

Debe existir en Angular:

Points Wallet
Rewards Catalog
Reward Detail
Redeem Reward
Confirmation
Redemption History
Consolation Reward

Pero el administrador tendrá:

Reward Management
Stock
Redemptions
Approval
Delivery
Reports

Por lo tanto:

Angular
   ↓
Reward consumption

Blazor
   ↓
Reward administration
17. SPEC-021 — Admin Game Operations

Esta nueva SPEC la recomiendo especialmente para tu prueba técnica.

Define cómo el operador administra un juego en vivo.

Por ejemplo:

Live Games

y abrir:

GAME #1024

Ver:

STATUS: LIVE

ROUND: 5 / 10

PLAYERS
────────────────────────
Oscar          2,500
Player 2       1,750
Player 3       1,250
Player 4         750

QUESTION
────────────────────────
Current question

ANSWER STATUS
────────────────────────
Oscar       Answered
Player 2    Answered
Player 3    Waiting
Player 4    Answered

El administrador podría tener acciones controladas como:

Pause Game
Resume Game
Force Finish
Cancel Game
Advance Round
View Player

Pero no debe poder manipular arbitrariamente puntos o respuestas desde la UI normal.

Cualquier acción privilegiada debe pasar por reglas de seguridad y auditoría.

18. Arquitectura final

La visión completa quedaría:

                              QUIZARENA
                                  │
              ┌───────────────────┴──────────────────┐
              │                                      │
              ▼                                      ▼
     ADMINISTRATION APP                         PLAYER APP
          Blazor                                  Angular 22
       .NET 11                                    Browser
              │                                      │
              └───────────────────┬──────────────────┘
                                  │
                                  ▼
                         QUIZARENA BACKEND
                                  │
                ┌─────────────────┼─────────────────┐
                │                 │                 │
                ▼                 ▼                 ▼
             Domain           Application      Infrastructure
                │                 │                 │
                └─────────────────┼─────────────────┘
                                  │
                 ┌────────────────┼────────────────┐
                 │                │                │
                 ▼                ▼                ▼
              SQL Server       RabbitMQ         SignalR

Y ahí aparecen claramente tus BuildingBlocks:

BuildingBlocks.Kernel.Domain
        ↓
Domain

BuildingBlocks.CQRS
        ↓
Application / Vertical Slices

BuildingBlocks.Kernel.Infrastructure
        ↓
EF Core / Repository / Outbox

BuildingBlocks.EventBus
        ↓
Integration Events

BuildingBlocks.EventBus.RabbitMQ
        ↓
RabbitMQ

BuildingBlocks.ServiceDefaults
        ↓
OpenTelemetry
Health
Resilience
Endpoints
Exception Handling

Sin MediatR, sin AutoMapper y sin MassTransit.

19. Orden exacto para comenzar

Ahora que todavía no has creado SPEC-016, yo no empezaría por SPEC-017.

Hazlo en este orden:

STEP 1
Actualizar Constitution
        ↓
STEP 2
Actualizar SPEC-004
        ↓
STEP 3
Actualizar SPEC-011
        ↓
STEP 4
Actualizar SPEC-012
        ↓
STEP 5
Crear SPEC-016
        ↓
STEP 6
Ejecutar UI/UX Pro Max
        ↓
design-system/MASTER.md
        ↓
STEP 7
Crear page overrides
        ↓
STEP 8
SPEC-017 Player Application
        ↓
STEP 9
SPEC-018 Game Experience
        ↓
STEP 10
SPEC-019 Administration Application
        ↓
STEP 11
SPEC-020 Rewards Experience
        ↓
STEP 12
SPEC-021 Admin Game Operations

Y solo después:

SPEC
  ↓
/plan
  ↓
/tasks
  ↓
implementation
Una decisión adicional que considero muy buena para la prueba

No intentaría hacer que Blazor y Angular tengan exactamente el mismo aspecto.

Compartirán:

Design Tokens
Typography
Color semantics
Spacing
Radius
Iconography
Accessibility
Motion principles

pero tendrán dos expresiones diferentes del mismo producto:

                    QUIZARENA
                       │
              DESIGN LANGUAGE
                       │
          ┌────────────┴────────────┐
          │                         │
       PLAYER                    ADMIN
          │                         │
      Angular 22                  Blazor
          │                         │
   "Game Show"                "Command Center"
          │                         │
   Emotional UX               Operational UX