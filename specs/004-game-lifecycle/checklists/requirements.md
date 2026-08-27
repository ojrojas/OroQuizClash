# Specification Quality Checklist: Game Lifecycle

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — Spec describe WHÁT (9 estados, transiciones, reglas "no puede iniciar sin...", eventos de dominio) sin prescribir C#/.NET, EF, endpoints concretos más allá de nombres de comandos; BuildingBlocks mencionado solo como abstracción constitucional ya existente.
- [x] Focused on user value and business needs — Escenarios desde perspectiva organizador/jugador/sistema autoritativo, valor "precondiciones sólidas", "motor mínimo jugable", "defensa de invariantes", "cierre auditable".
- [x] Written for non-technical stakeholders — Reglas y escenarios en lenguaje de negocio con Given/When/Then, sin código; estados y eventos explicados como flujo de partida.
- [x] All mandatory sections completed — User Scenarios (4 stories), Requirements (FR-001..014), Key Entities, Success Criteria (SC-001..008), Assumptions, Dependencies, Out of Scope, References presentes.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — 0 marcadores; decisiones potencialmente ambiguas (IN_PROGRESS vs ROUND_IN_PROGRESS modelado, late join, selección sin preguntas) resueltas en Assumptions con variante documentada sin bloquear planning.
- [x] Requirements are testable and unambiguous — FR-001..014 cada uno verificable: FR-003 gate categoría ≥5, FR-004 NotEnoughPlayers, FR-005 RoundAlreadyInProgress, FR-006 NoActiveRound, FR-007 ConfigurationImmutable, FR-008 InvalidGameState matriz, FR-013 idempotencia Join/SubmitAnswer.
- [x] Success criteria are measurable — SC-001..008 cuantifican 100% rechazo/éxito, <1s/<2s/<500ms p95, 409 100% en concurrencia, 90% usabilidad; verificables por curl + rowversion.
- [x] Success criteria are technology-agnostic (no implementation details) — Métricas describen resultados observables (201, 400, 409, eventos emitidos, bloquea mutación) no SQL/cache/ORM.
- [x] All acceptance scenarios are defined — 4 historias con 6+6+5+4 escenarios Given/When/Then cubriendo creación, ciclo rondas, defensa invariantes, finalización/cancelación.
- [x] Edge cases are identified — 11 casos: categoría archivada entre Create y MarkReady, Join después de Start, MinPlayers incoherente, NoAvailableQuestion en StartRound, duplicado SubmitAnswer idempotente, concurrencia StartRound/CompleteRound, rowversion stale, ForceFinish sin motivo, Cancel desde FINISHED, Timeout frontera, abandono sin jugadores.
- [x] Scope is clearly bounded — Out of Scope excluye selección concreta (SPEC-003), evaluación/puntaje ledger, UI, late join, importación masiva; enfoque solo ciclo de estados y gates.
- [x] Dependencies and assumptions identified — Dependencias listan SPEC-001/002/003/011 + BuildingBlocks + OroIdentityServer; Assumptions documentan estados intermedios, late join no permitido, numeración rondas, rowversion, DomainEvent/Outbox, roles ADMIN/GAME_MANAGER/PLAYER, Reason 3–500, NoAvailableQuestion manejo, defaults Min/MaxPlayers.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria — Cada FR mapea a US: FR-003/004→US1 Sc 2-5, FR-005→US2 Sc 3-4, FR-006→US3 Sc 2-3, FR-007→US3 Sc 1, FR-008→US3 Sc 4 + US4 Sc 1-4, FR-013→US2 Sc 6 + Edge Cases duplicado.
- [x] User scenarios cover primary flows — P1 stories (crear-preparar + ciclo rondas + defensa) entregan MVP jugable; P2 (finalización) cierra ciclo auditable; flujos IS1→IS2→IS3→IS4 validables incrementalmente.
- [x] Feature meets measurable outcomes defined in Success Criteria — SC-001..007 verifican gates FR-003..FR-008; SC-008 mide usabilidad flujo completo Create→Finish; SC-004 verifica selección PUBLISHED SPEC-003 en StartRound <500ms.
- [x] No implementation details leak into specification — Nombres de agregados/métodos (`Game.Create`, `MarkReady`, `StartRound`) son los de constitución (Domain First), no exponen EF, SQL, API payloads concretos; persistencia solo como `Specification`/`rowversion` abstracto constitucional.

## Notes

- Validation iteration 1: All items pass. No rework needed.
- Trazabilidad: Estados DRAFT→READY→WAITING_FOR_PLAYERS→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED + CANCELLED/FORCED_FINISHED cubren constitución A (9 estados); reglas 1-6 trazables: "sin config válida"→FR-003/SC-001, "sin jugadores suficientes"→FR-004/SC-003, "anterior no terminó"→FR-005/SC-004, "sin ronda activa"→FR-006/SC-005, "no modificarse después de comenzar"→FR-007/SC-006, "solo finalizar desde válidos"→FR-008/SC-007.
- Eventos GameCreated/GameReady/PlayerJoined/GameStarted/GameFinished/GameCancelled/GameForcedFinished trazables a FR-010 y US1-US4; RoundStarted/RoundCompleted incluidos como eventos de ronda.
- 0 [NEEDS CLARIFICATION]; variantes (IN_PROGRESS fino vs grueso, NoAvailableQuestion) manejadas en Assumptions sin bloquear.
- Ready for `/speckit.clarify` or `/speckit.plan`.

