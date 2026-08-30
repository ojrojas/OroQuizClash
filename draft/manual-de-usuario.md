# Manual de Usuario — OroQuizClash QuizArena

Guía completa para configurar, administrar y jugar en la plataforma de trivia multiplayer.

---

## 1. Configuración Inicial del Sistema

### 1.1 Requisitos Previos

| Componente | Versión | Propósito |
|------------|---------|-----------|
| .NET SDK | 10.0+ | Backend + AppHost |
| Node.js | 22 LTS | Player SPA + Admin |
| Angular CLI | 22 | `npm i -g @angular/cli@22` |
| Podman/Docker | 4.x+ | OroIdentityServer container |
| Aspire workload | 9+ | `dotnet workload install aspire` |

### 1.2 Levantar Infraestructura

```bash
# 1. Clonar el repo
git clone <repo-url> && cd OroQuizClash

# 2. Generar secretos
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"

# 3. Levantar todo con Aspire
aspire start
```

Esto levanta:

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| `sqlserver` | — | SQL Server (datos del juego) |
| `postgres` | — | PostgreSQL (identitydb) |
| `redis` | — | Cache futuro |
| `rabbitmq` | — | EventBus para eventos de integración |
| `identity-api` | 5080 (HTTP) / 5086 (HTTPS) | OroIdentityServer |
| `oroclash-api` | 5000 | API principal del juego |
| `quizarena-admin` | 7172 | Panel de administración Blazor |
| `quizarena-player` | 4200 | SPA Angular del jugador |

**Health checks:**
```bash
curl http://localhost:5000/health          # API
curl http://localhost:5080/.well-known/openid-configuration  # Identity
curl http://localhost:7172/health          # Admin
```

### 1.3 Registrar Aplicación OIDC del Player

El cliente `quizarena-player` debe registrarse una vez en OroIdentityServer:

1. Abrir Admin UI: `http://localhost:5080` (o `https://localhost:5086`)
2. Login: `admin` / `Admin@123456`
3. Ir a **Applications** → **Create**
4. Configurar:

| Campo | Valor |
|-------|-------|
| Client ID | `quizarena-player` |
| Client Type | Public (PKCE) |
| Grant Types | `authorization_code`, `refresh_token` |
| Response Types | `code` |
| Code Challenge Methods | `S256` |
| Redirect URIs | `http://localhost:4200/auth/callback` |
| Post-Logout Redirect URIs | `http://localhost:4200/auth/logout-callback` |
| Scopes | `openid`, `profile`, `email`, `offline_access` |

5. Guardar

**Alternativa vía API:**
```bash
# Obtener token de admin
TOKEN=$(curl -sk -X POST https://localhost:5086/connect/token \
  -d "grant_type=client_credentials&client_id=quizarena-admin&client_secret=<SECRET>&scope=admin" \
  | jq -r .access_token)

# Crear aplicación
curl -X POST https://localhost:5086/api/applications \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "quizarena-player",
    "clientType": "public",
    "displayName": "QuizArena Player",
    "permissions": [
      "ept:authorization", "ept:token", "ept:end_session", "ept:userinfo",
      "gt:authorization_code", "gt:refresh_token",
      "scp:openid", "scp:profile", "scp:email", "scp:offline_access"
    ],
    "redirectUris": ["http://localhost:4200/auth/callback"],
    "postLogoutRedirectUris": ["http://localhost:4200/auth/logout-callback"]
  }'
```

### 1.4 Registrar Aplicación OIDC del Admin

```bash
# Ejecutar script automático
./scripts/register-admin-oidc-client.sh
```

Esto registra `quizarena-admin` (confidencial, `client_secret`) automáticamente.

---

## 2. Gestión de Usuarios y Roles

### 2.1 Usuario Admin (Bootstrap)

| Campo | Valor |
|-------|-------|
| Username | `admin` |
| Password | `Admin@123456` |
| Email | `admin@oroclash.local` |
| Rol | `Administrator` |

> **Nota:** El admin es la única cuenta exenta de cambio de contraseña forzado.

### 2.2 Crear Usuarios Jugadores

1. Login como `admin` en `http://localhost:5080`
2. Ir a **Users** → **Create**
3. Configurar usuario:

| Campo | Ejemplo |
|-------|---------|
| Username | `player1` |
| Email | `player1@example.com` |
| Password | `Player@123456` |
| Roles | `PLAYER` |

4. Repetir para `player2`, `player3`, etc.

> **Nota:** Todos los usuarios (excepto admin) deben cambiar contraseña en el primer login.

### 2.3 Roles del Sistema

| Rol | Permisos |
|-----|----------|
| `ADMIN` | Acceso total: CRUD categorías, preguntas, juegos, recompensas, usuarios, auditoría |
| `GAME_MANAGER` | Crear/editar/iniciar/finalizar juegos |
| `REWARD_MANAGER` | Gestionar recompensas y canjes |
| `PLAYER` | Participar en juegos, ver leaderboard, canjear recompensas |

### 2.4 Asignar Roles

1. En Admin UI → **Users** → seleccionar usuario → **Edit**
2. Seleccionar roles checkboxes → **Save**

O vía API:
```bash
curl -X PUT https://localhost:5086/api/users/{userId}/roles \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '["PLAYER"]'
```

---

## 3. Configuración de Categorías

### 3.1 Categorías pre-cargadas (Seeder)

El seeder crea automáticamente 10 categorías de Ciencias:

| # | Categoría | Dificultad | Estado |
|---|-----------|------------|--------|
| 1 | Biología Celular | 3 | ACTIVE |
| 2 | Genética y Herencia | 3 | ACTIVE |
| 3 | Ecología y Medio Ambiente | 2 | ACTIVE |
| 4 | Anatomía Humana | 3 | ACTIVE |
| 5 | Química Inorgánica | 3 | ACTIVE |
| 6 | Química Orgánica | 4 | ACTIVE |
| 7 | Física Mecánica | 3 | ACTIVE |
| 8 | Física Electricidad y Magnetismo | 4 | ACTIVE |
| 9 | Ciencias de la Tierra | 2 | ACTIVE |
| 10 | Astronomía | 2 | ACTIVE |

### 3.2 Crear Nueva Categoría

1. Admin UI → **Categories** → **Create**
2. Completar campos:

| Campo | Descripción | Requisitos |
|-------|-------------|------------|
| Name | Nombre de la categoría | 3-100 caracteres |
| Description | Descripción opcional | 0-500 caracteres |
| Knowledge Area | Área de conocimiento | Ej: "Ciencias", "Historia" |
| Academic Level | Nivel académico | Ej: "Secundaria", "Universidad" |
| Age Range | Rango de edad (min-max) | 0-120, min ≤ max |
| Difficulty Level | Nivel de dificultad | 1-5 |
| Tags | Etiquetas | Set de strings |

3. Guardar (estado: `DRAFT`)

### 3.3 Flujo de Estado de Categoría

```
DRAFT → ACTIVE ↔ INACTIVE → ARCHIVED
```

- **DRAFT**: Solo visible para admins. Edición libre.
- **ACTIVE**: Visible para jugadores. Requiere ≥5 preguntas válidas para publicar.
- **INACTIVE**: No visible para jugadores. Edición permitida.
- **ARCHIVED**: Terminal. No se puede volver a ACTIVE.

### 3.4 Publicar Categoría

Solo se puede publicar si tiene ≥5 preguntas válidas (ACTIVE, 4 opciones, 1 correcta).

```bash
POST /api/categories/{id}/activate
```

---

## 4. Configuración de Preguntas

### 4.1 Preguntas pre-cargadas (Seeder)

El seeder crea 200 preguntas (20 por categoría):

- Todas con dificultad 1-4
- Nivel académico: "Secundaria"
- Rango edad: 12-17
- Tiempo: 30 segundos por pregunta
- 4 opciones de respuesta cada una
- Publicadas inmediatamente

### 4.2 Crear Nueva Pregunta

1. Admin UI → **Question Bank** → **Create**
2. Completar campos:

| Campo | Descripción | Requisitos |
|-------|-------------|------------|
| Text | Texto de la pregunta | 10-500 caracteres |
| Category | Categoría asociada | Debe existir y no estar ARCHIVED |
| Difficulty | Dificultad | 1-5 |
| Academic Level | Nivel académico | Ej: "Secundaria" |
| Age Range | Rango edad (min-max) | 0-120 |
| Time Per Question | Tiempo límite | 5-300 segundos |
| Explanation | Explicación (opcional) | 0-1000 caracteres, se muestra después de responder |
| Option A | Primera opción | 1-200 caracteres |
| Option B | Segunda opción | 1-200 caracteres |
| Option C | Tercera opción | 1-200 caracteres |
| Option D | Cuarta opción | 1-200 caracteres |
| Correct Option | Cuál es la correcta | Exactamente 1 |

3. Guardar (estado: `DRAFT`)

### 4.3 Invariantes de Pregunta

- **QST-001**: Exactamente 4 opciones de respuesta
- **QST-002**: Exactamente 1 respuesta correcta
- **QST-003**: Debe pertenecer a una categoría válida
- **QST-004**: Dificultad debe estar definida (1-5)
- **QST-005**: Una pregunta publicada no puede perder su respuesta correcta
- **QST-006**: Debe ser validada antes de estar disponible para juegos

### 4.4 Flujo de Estado de Pregunta

```
DRAFT → ACTIVE → PUBLISHED → INACTIVE → ARCHIVED
```

### 4.5 Publicar Pregunta

```bash
POST /api/questions/{id}/publish
```

### 4.6 Seleccionar Preguntas para Juego

El motor de selección se invoca automáticamente al iniciar una ronda:

```bash
POST /api/questions/select
{
  "categoryId": "...",
  "difficulty": 3,
  "academicLevel": "Secundaria",
  "ageRange": {"min": 12, "max": 17},
  "previousQuestions": [...],
  "gameId": "...",
  "roundNumber": 1
}
```

---

## 5. Configuración de Juegos

### 5.1 Juegos pre-cargados (Seeder)

El seeder crea 10 juegos (uno por categoría ACTIVE):

| Campo | Valor |
|-------|-------|
| Nombre | "Torneo {Categoría} - Secundaria {NN}" |
| Rondas | 5-8 |
| Dificultad inicial | 1-3 (aleatoria) |
| Estrategia | Linear |
| Tiempo/pregunta | 30s |
| Scoring | Standard |
| Loss Policy | LoseUnsecuredPoints |
| Withdrawal Policy | KeepSecuredScore |
| Max Players | 10 |
| Estado | WAITING_FOR_PLAYERS |

### 5.2 Crear Nuevo Juego

1. Admin UI → **Games** → **Create**
2. Configurar 16 atributos:

| Campo | Descripción | Requisitos |
|-------|-------------|------------|
| Name | Nombre del juego | 3-100 caracteres |
| Description | Descripción | 0-500 caracteres |
| Category | Categoría | Debe ser ACTIVE con ≥5 preguntas |
| Number of Rounds | Rondas | 5-10 |
| Max Players | Máximo de jugadores | ≥2, ≤1000 |
| Time Per Question | Tiempo por pregunta | 5-300 segundos |
| Initial Difficulty | Dificultad inicial | 1-5 |
| Difficulty Progression | Estrategia de dificultad | Linear / Progressive / Adaptive / CategorySpecific |
| Scoring System | Sistema de puntuación | Standard / ProgressiveBonus |
| Secured Points Policy | Política de puntos asegurados | Configurable |
| Withdrawal Policy | Política de retiro | LOSE_ALL / KEEP_CURRENT_SCORE / KEEP_SECURED_SCORE / KEEP_CHECKPOINT_SCORE |
| Loss Policy | Política de pérdida | LOSE_ALL / LOSE_CURRENT_ROUND / LOSE_UNSECURED_POINTS / FALLBACK_TO_CHECKPOINT |
| Consolation Policy | Política de consolación | Configurable |
| Final Reward | Recompensa final (opcional) | Referencia a Reward ACTIVE |
| Consolation Reward | Recompensa de consolación (opcional) | Referencia a Reward |
| ScheduledAt | Hora de inicio programada | UTC, ≥5 min en futuro |

3. Guardar (estado: `DRAFT`)

### 5.3 Flujo de Estado de Juego

```
DRAFT → CONFIGURED → SCHEDULED → READY → RUNNING ↔ PAUSED → FINISHED
                                                      ↓
                                                  CANCELLED
```

**Transiciones permitidas:**
- `DRAFT/CONFIGURED/SCHEDULED` → `CANCELLED`
- `RUNNING` → `PAUSED` → `RUNNING`
- `RUNNING` → `FINISHED` (cuando todas las rondas completan o admin finaliza)

### 5.4 Abrir Lobby (para que jugadores se unan)

```bash
POST /api/games/{id}/open-lobby
```

El juego pasa de `READY` → `WAITING_FOR_PLAYERS`. Los jugadores pueden unirse desde la SPA.

### 5.5 Iniciar Juego

```bash
POST /api/games/{id}/start
```

El juego pasa a `IN_PROGRESS`. **Las rondas NO comienzan automáticamente** — el manager debe iniciar cada ronda manualmente desde el Live Game Dashboard.

### 5.6 Live Game Dashboard (Panel en Vivo)

Después de iniciar un juego, el manager es redirigido automáticamente a `/admin/live/{gameId}`. Este panel muestra en tiempo real:

| Elemento | Descripción |
|----------|-------------|
| **Status** | Estado actual del juego (Running, Paused, etc.) |
| **Current Round** | "Round 1 / 8" — ronda actual / total |
| **Current Question** | Texto de la pregunta + 4 opciones |
| **Players** | Jugadores conectados, respondidos, esperando |
| **Scores** | Leaderboard en tiempo real |
| **Timer** | Cuenta regresiva de la ronda |

**Operaciones disponibles:**

| Botón | Acción | Requisitos |
|-------|--------|------------|
| **Start Round** | Inicia la siguiente ronda, selecciona y presenta una pregunta | Estado: Running sin ronda activa |
| **Complete Round** | Finaliza la ronda actual, asegura puntos | Estado: Running con ronda activa |
| **Pause** | Pausa el juego, congela timer | Estado: Running |
| **Resume** | Reanuda el juego | Estado: Paused |
| **Cancel** | Cancela el juego (terminal) | Estado: Running o Paused |
| **Force Finish** | Finaliza forzadamente (requiere reason) | Estado: Running o Paused |

### 5.7 Flujo Completo del Manager

```
1. Crear juego → DRAFT
2. Open Lobby → WAITING_FOR_PLAYERS (jugadores se unen)
3. Start Game → IN_PROGRESS (redirige a Live Dashboard)
4. Start Round → ROUND_IN_PROGRESS (aparece pregunta)
   → Jugadores responden
5. Complete Round → ROUND_COMPLETED (asegura puntos)
6. Repetir pasos 4-5 hasta completar todas las rondas
7. Finish Game → FINISHED (muestra resultados)
```

### 5.8 Finalizar Juego

```bash
POST /api/games/{id}/finish
```

---

## 6. Flujo de Juego (Jugador)

### 6.1 Login

1. Abrir `http://localhost:4200`
2. Redirige a `OroIdentityServer` → login con `player1` / `Player@123456`
3. Redirige de vuelta al lobby

### 6.2 Lobby

- Se muestran juegos en estado `WAITING_FOR_PLAYERS`
- 8 columnas: Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status
- **Join Game**: `POST /api/games/{id}/players` (idempotente con `X-Idempotency-Key`)
- **View Game Information**: Detalle extendido de la configuración

### 6.3 Pantalla de Juego

Una vez dentro de un juego:

| Elemento | Descripción |
|----------|-------------|
| **Current Round** | "Ronda 3/10" |
| **Current Level** | Nivel de dificultad actual |
| **Player Status** | ACTIVO / RETIRADO / ELIMINADO |
| **Timer** | Cuenta regresiva (30s por defecto) |
| **Question** | Texto + 4 opciones (radiogroup) |
| **Score Panel** | Current Points, Secured Points, Potential Points, Round Points, Total Points |
| **Ladder** | Progresión de rondas 1..N |
| **Leaderboard** | Ranking de jugadores en tiempo real |
| **Withdrawal** | Botón para retirarse |

### 6.4 Responder Preguntas

1. Seleccionar una de las 4 opciones (click o teclado)
2. **Confirmar** → lock visual → envío `POST /api/games/{id}/answers`
3. Resultado: Correcto (+puntos) / Incorrecto (sin puntos) / Expirado (sin respuesta)
4. Score se actualiza inmediatamente (ledger `PointTransaction`)

### 6.5 Retirarse

1. Click **Withdrawal Action**
2. Diálogo muestra: Current Points, Secured Points, Potential Points
3. 2 warnings: "puedes perder puntos" y "asegura X puntos"
4. Confirmar → `POST /api/games/{id}/withdraw` → estado `WITHDRAWN`
5. `Status` terminal: no puede responder más rondas
6. `Score = SecuredPoints` según política `KEEP_SECURED_SCORE`

### 6.6 Resultados

Al finalizar el juego, se muestra una de 4 pantallas:

| Estado | Pantalla |
|--------|----------|
| `WINNER` (posición 1) | **YOU WON** + confetti + Prize |
| `WITHDRAWN` | **YOU WALKED AWAY** + Secured Points + Rewards disponibles |
| `ELIMINATED` | **GAME OVER** + Final Score + Consolación |
| `FINISHED` (posición 2+) | **GAME FINISHED** + Posición + Reward |

### 6.7 Recompensas

1. Navegar a `/player/rewards`
2. **Points Wallet**: Puntos disponibles
3. **Rewards Catalog**: Grid de recompensas con status (Canjeable / Puntos insuficientes / Agotada)
4. **Reward Detail**: 4 métricas + botón **Canjear**
5. Confirmar canje → `POST /api/rewards/{id}/redeem` con `X-Idempotency-Key`
6. **History**: Historial de canjes anteriores

---

## 7. API Reference

### 7.1 Endpoints Principales

| Método | Endpoint | Descripción | Auth |
|--------|----------|-------------|------|
| `GET` | `/api/games` | Listar juegos (paginado, filtro status) | Bearer |
| `GET` | `/api/games/{id}` | Detalle de juego | Bearer |
| `GET` | `/api/games/{id}/live` | Live Game Dashboard (estado, pregunta actual, scores) | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games` | Crear juego | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/players` | Unirse a juego | Bearer |
| `GET` | `/api/games/{id}/players/me` | Estado del jugador en el juego | Bearer |
| `POST` | `/api/games/{id}/answers` | Enviar respuesta | Bearer |
| `POST` | `/api/games/{id}/withdraw` | Retirarse del juego | Bearer |
| `GET` | `/api/games/{id}/leaderboard` | Leaderboard del juego | Bearer |
| `POST` | `/api/games/{id}/start` | Iniciar juego | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/rounds/start` | Iniciar siguiente ronda | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/rounds/{roundId}/complete` | Finalizar ronda actual | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/finish` | Finalizar juego | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/cancel` | Cancelar juego | Bearer + ADMIN/GAME_MANAGER |
| `POST` | `/api/games/{id}/force-finish` | Forzar finalización (requiere reason) | Bearer + ADMIN/GAME_MANAGER |
| `GET` | `/api/categories` | Listar categorías | Bearer |
| `POST` | `/api/categories` | Crear categoría | Bearer + ADMIN |
| `GET` | `/api/questions` | Listar preguntas | Bearer |
| `POST` | `/api/questions` | Crear pregunta | Bearer + ADMIN |
| `GET` | `/api/rewards?gameId` | Recompensas disponibles | Bearer |
| `POST` | `/api/rewards/{id}/redeem` | Canjear recompensa | Bearer |
| `GET` | `/api/redemptions` | Historial de canjes | Bearer |

### 7.2 Headers Requeridos

| Header | Descripción |
|--------|-------------|
| `Authorization: Bearer <token>` | JWT de OroIdentityServer |
| `X-Correlation-Id` | UUID generado automáticamente por el interceptor |
| `X-Idempotency-Key` | UUID para operaciones idempotentes (join, answer, withdraw, redeem) |

### 7.3 Errores (RFC 7807)

```json
{
  "type": "about:blank",
  "title": "GameFull",
  "status": 400,
  "detail": "El juego ya alcanzó el máximo de jugadores",
  "code": "GameFull",
  "traceId": "...",
  "correlationId": "..."
}
```

---

## 8. Arquitectura Técnica

### 8.1 Componentes

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Player SPA     │────▶│  OroQuizClash.Api │────▶│  SQL Server     │
│  Angular 22     │     │  .NET 10          │     │  (oroclash)     │
│  port 4200      │     │  port 5000        │     └─────────────────┘
└─────────────────┘     └──────────────────┘             │
         │                      │                        │
         │               ┌──────┴──────┐          ┌──────┴──────┐
         │               │ OroIdentity │          │  RabbitMQ   │
         │               │ Server      │          │  (Outbox)   │
         │               │ port 5086   │          └─────────────┘
         │               └─────────────┘
         │                      │
         │               ┌──────┴──────┐
         │               │ PostgreSQL  │
         │               │ (identitydb)│
         │               └─────────────┘
         │
┌────────┴────────┐
│  Admin Blazor   │
│  port 7172      │
└─────────────────┘
```

### 8.2 Data Flow — Responder Pregunta

```
Player selects option
       │
       ▼
POST /api/games/{id}/answers
  { roundId, questionId, selectedOptionId, idempotencyKey }
       │
       ▼
┌─────────────────────────────────────┐
│  SubmitAnswer Handler (Application) │
│  1. Validate player is ACTIVE       │
│  2. Validate round IN_PROGRESS      │
│  3. Validate answerWindow open      │
│  4. Evaluate answer (Domain)        │
│  5. Award points via ledger         │
│  6. Publish ScoreUpdated event      │
│  7. Return Answer DTO               │
└─────────────────────────────────────┘
       │
       ▼
SignalR ScoreUpdated → All players
       │
       ▼
Player hydrates via GET /players/me
```

### 8.3 Server Truth (Constitución V)

- El **servidor** es la única fuente de verdad para: Correctitud, Puntos, Timer, Estado
- El **cliente** es solo presentación: nunca calcula ni persiste autoritativamente
- SignalR solo notifica; siempre se rehidrata via REST

---

## 9. Troubleshooting

### 9.1 "Token 400" al hacer login

**Causa:** El cliente `quizarena-player` no está registrado en OroIdentityServer.

**Solución:** Ver sección 1.3 — Registrar aplicación OIDC.

### 9.2 "store.hydrate is not a function"

**Causa:** El store tiene un bug de tipado con `rxMethod`.

**Solución:** Asegurar que `hydrate` esté definido como método plano o Subject, no como `rxMethod` dentro del mismo `withMethods`.

### 9.3 SignalR no conecta

**Causa:** El proxy no redirige WebSocket correctamente.

**Solución:** Verificar `proxy.conf.js` tenga `ws: true` para `/hubs`.

### 9.4 "GameNotWaitingForPlayers" al unirse

**Causa:** El juego ya no está en estado `WAITING_FOR_PLAYERS`.

**Solución:** Refrescar lobby. El juego puede haber sido iniciado o cancelado.

### 9.5 "must_change_password" redirige a cambio

**Causa:** Primer login de usuario (política de seguridad).

**Solución:** Completar el cambio de contraseña en la página de OroIdentityServer.

### 9.6 Preguntas no aparecen para jugadores

**Causa:** El manager inició el juego pero no inició la ronda.

**Solución:** El manager debe ir a `/admin/live/{gameId}` y hacer click en "Start Round". Las rondas NO comienzan automáticamente al iniciar el juego.

### 9.7 "EXPIRED" / "Tiempo agotado" al responder

**Causa:** El tiempo límite de la pregunta expiró antes de enviar la respuesta.

**Solución:** El jugador debe responder dentro del tiempo límite (default 30s). El timer se muestra en la pantalla de juego.

### 9.8 Force Finish retorna 400

**Causa:** El endpoint requiere un `reason` (3-500 caracteres).

**Solución:** Ingresar un motivo en el campo de texto antes de confirmar.

---

## 10. Desarrollo

### 10.1 Player SPA

```bash
cd src/Player/QuizArena.Player
pnpm install
pnpm start           # http://localhost:4200
pnpm test            # Vitest
pnpm run build       # Production build
```

### 10.2 Backend

```bash
dotnet build         # Compilar todo
dotnet test          # 864+ tests
dotnet run --project src/OroQuizClash.Api  # Solo API
```

### 10.3 Admin Blazor

```bash
dotnet run --project src/Admin/QuizArena.Admin  # https://localhost:7172
```

---

*Manual generado desde `specs/001-036/` y `draft/oroidentityserver-specification.md`.*
*Última actualización: 2026-08-30 — Corregido flujo de rondas (Start Round manual), agregado Live Game Dashboard, agregados endpoints de rondas, agregados troubleshooting 9.6-9.8.*
