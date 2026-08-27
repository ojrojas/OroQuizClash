# Quickstart: Answer Evaluation

**Feature**: `006-answer-evaluation` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Prerequisites

- .NET 10.0 SDK installed
- SQL Server running (local or container)
- OroIdentityServer running (or JWT mock for local testing)
- Existing database with Game, GameRound, GamePlayer, Question, AnswerOption data (from SPEC-001/003/004/005)

## Setup

```bash
# Restore and build
dotnet restore src/OroQuizClash.Api
dotnet build src/OroQuizClash.Api

# Run database migrations (if EF Core migrations exist)
dotnet ef database update --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api

# Start the application
dotnet run --project src/OroQuizClash.Api
```

## Validation Scenarios

### V-001: Correct Answer Submission (P1)

**Scenario**: Player submits correct answer within time limit → server evaluates correct, calculates points, creates PointTransaction.

**Setup**: Game IN_PROGRESS, Round ROUND_IN_PROGRESS (TimeLimit=30s, StartedAt=T0), Player IN_PROGRESS, Question with 4 AnswerOptions (one correct).

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{correct-option-id}"}'
```

**Expected**: `200 OK`
```json
{
  "answerId": "...",
  "correct": true,
  "points": 10,
  "elapsedTime": 10,
  "status": "EVALUATED",
  "roundNumber": 1,
  "gameStatus": "ROUND_IN_PROGRESS"
}
```

**Verify**:
- `correct` is `true` (server-determined)
- `points` = `PointsPerRound × DifficultyMultiplier` (server-calculated)
- `elapsedTime` = `ServerTimestamp - StartedAt` (server-calculated, ≤ TimeLimit)
- `status` = `EVALUATED`
- `PointTransaction` created with `Type=ANSWER_CORRECT`, `Points=calculated`

---

### V-002: Incorrect Answer Submission (P1)

**Scenario**: Player submits incorrect answer → server evaluates incorrect, points=0.

**Setup**: Same as V-001 but `answerOptionId` points to incorrect option.

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{incorrect-option-id}"}'
```

**Expected**: `200 OK`
```json
{
  "correct": false,
  "points": 0,
  "status": "EVALUATED"
}
```

**Verify**:
- `correct` = `false`
- `points` = `0`
- `PointTransaction` created with `Type=ANSWER_INCORRECT`, `Points=0`

---

### V-003: Timeout (Answer After TimeLimit) (P1)

**Scenario**: Player submits answer after TimeLimit → server rejects with AnswerTimeout.

**Setup**: Round with `TimeLimit=30s`, `StartedAt=T0`, submit at `T0+31s`.

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{any-option-id}"}'
```

**Expected**: `408 Request Timeout`
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "AnswerTimeout",
  "status": 408,
  "detail": "Answer submitted after time limit."
}
```

**Verify**:
- No `Answer` created with `Status=EVALUATED`
- No `PointTransaction` created

---

### V-004: Idempotent Submission (Duplicate) (P1)

**Scenario**: Player submits same answer twice for same round → second returns same result without duplication.

**Setup**: Player already answered in round (Answer exists with Status=EVALUATED).

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{same-option-id}"}'
```

**Expected**: `200 OK` with same result as first submission.

**Verify**:
- Same `answerId` returned (no new Answer created)
- Same `correct`, `points`, `elapsedTime` values
- No duplicate `PointTransaction` created
- `UNIQUE (GameId, PlayerId, RoundId)` constraint holds

---

### V-005: Invalid AnswerOptionId (P1)

**Scenario**: Player submits AnswerOptionId that doesn't belong to the active Question → server rejects with InvalidAnswer.

**Setup**: Question has 4 AnswerOptions (IDs: A, B, C, D). Player submits AnswerOptionId=E (doesn't exist).

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{nonexistent-option-id}"}'
```

**Expected**: `400 Bad Request`
```json
{
  "status": 400,
  "detail": "Answer option does not belong to the active question."
}
```

---

### V-006: Player Not In Game (P1)

**Scenario**: Player with `Status=WITHDRAWN` or not in game submits answer → server rejects.

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {withdrawn-player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{option-id}"}'
```

**Expected**: `400 Bad Request`
```json
{
  "status": 400,
  "detail": "Player is not in progress in this game."
}
```

---

### V-007: Game Not Active (P1)

**Scenario**: Player submits answer when Game is FINISHED/CANCELLED → server rejects.

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{finished-gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{option-id}"}'
```

**Expected**: `400 Bad Request`
```json
{
  "status": 400,
  "detail": "Game is not in active state."
}
```

---

### V-008: Question Not Active (Round Completed) (P1)

**Scenario**: Player submits answer when Round is ROUND_COMPLETED → server rejects.

**Command**:
```bash
curl -X POST http://localhost:5000/api/games/{gameId}/answers \
  -H "Authorization: Bearer {player-jwt}" \
  -H "Content-Type: application/json" \
  -d '{"answerOptionId": "{option-id}"}'
```

**Expected**: `400 Bad Request`
```json
{
  "status": 400,
  "detail": "Round is not in progress."
}
```

---

### V-009: Get Answer Detail (P2)

**Scenario**: Query specific answer by ID → returns full answer details.

**Command**:
```bash
curl http://localhost:5000/api/games/{gameId}/answers/{answerId} \
  -H "Authorization: Bearer {player-jwt}"
```

**Expected**: `200 OK` with `AnswerDetailResponse` schema.

---

### V-010: Get Player Score (P2)

**Scenario**: Query player score → returns total points from PointTransaction ledger.

**Command**:
```bash
curl http://localhost:5000/api/games/{gameId}/score/{playerId} \
  -H "Authorization: Bearer {player-jwt}"
```

**Expected**: `200 OK`
```json
{
  "gameId": "...",
  "playerId": "...",
  "totalPoints": 50,
  "correctAnswers": 5,
  "incorrectAnswers": 2,
  "totalAnswered": 7
}
```

---

### V-011: Concurrency Conflict (RowVersion) (P2)

**Scenario**: Two concurrent SubmitAnswer from same player → second gets 409.

**Setup**: Two parallel requests with same `GameId + PlayerId + RoundId`.

**Expected**: First succeeds with `200 OK`, second returns `409 Conflict` with `ConcurrencyConflict` error.

---

### V-012: Answer Immutability (P2)

**Scenario**: Attempt to update an EVALUATED answer → server rejects.

**Note**: No `UpdateAnswer` endpoint exists by design. This is verified by:
1. No public setter on `Answer.Status`, `Answer.Correct`, `Answer.Points` after `EVALUATED`
2. Architecture test: `Answer` entity has no `Update*` or `Set*` methods for evaluated fields
3. Direct DB update attempt violates `AnswerImmutabilityRule`

---

## Test Commands

```bash
# Unit tests (Domain)
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Answer"

# Unit tests (Application)
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~SubmitAnswer"

# Integration tests (if Testcontainers available)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter "FullyQualifiedName~Answer"

# Architecture tests (dependency rules)
dotnet test tests/OroQuizClash.Architecture.Tests

# Full test suite
dotnet test
```

## Success Criteria Verification

| SC | Metric | Verification |
|----|--------|-------------|
| SC-001 | Correct → `correct=true` <1s p95 | V-001: measure response time |
| SC-002 | Incorrect → `correct=false` <1s p95 | V-002: measure response time |
| SC-003 | Timeout → `AnswerTimeout` <1s p95 | V-003: measure response time |
| SC-004 | Duplicate → idempotent, no PointTransaction duplication | V-004: verify same result, no extra rows |
| SC-005 | Invalid AnswerOption → `InvalidAnswer` | V-005: verify error response |
| SC-006 | 0% mutation post-EVALUATED | V-012: architecture test |
| SC-007 | PointTransaction only when EVALUATED | V-001/V-002: check PointTransaction creation |
| SC-008 | Client never determines correct/elapsedTime/points | Verify request schema has no these fields |
| SC-009 | All failure modes → specific error | V-003→V-008: verify error codes |
