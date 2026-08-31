# 04 — Base de Datos — Scripts y Semillas — OroQuizClash

> **DB primaria:** `oroclash` (SQL Server) — `OroQuizClashDbContext` (`src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs:17`)  
> **DB identidad:** `identitydb` (PostgreSQL, gestionada por `OroIdentityServer`, no se versiona aquí)  
> **Estrategia DDL:** `EnsureCreated` idempotente (Seeder) + EF Core Migrations opcional (`dotnet ef migrations script`)  
> **Fecha:** 31-08-2026 — Orquestación Aspire con volúmenes persistentes

---

## 1. Resumen y convenciones

- **Motor soportado:** SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) en producción/Aspire; SQLite fallback para tests y corridas sin `sqlserver` (`OnModelCreating` branch `Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"` `:41`, `BumpSqliteRowVersions` `:85`).
- **Creación esquema:** El `Seeder` (`Worker.cs:58`) ejecuta `await db.Database.EnsureCreatedAsync(ct)` con reintentos ×5/3s. Si ya existen tablas, es no-op. Es idempotente y no requiere `Migrations` folder (el repo no commitea migraciones; se generan a demanda).
- **Semillas:** `OroQuizClash.Seeder` (proyecto `src/Seeder/OroQuizClash.Seeder/`, ver §4) siembra `10 categorías ×20 preguntas =200 preguntas (Published) + 10 juegos WAITING_FOR_PLAYERS`. Totalmente idempotente (verifica `COUNT` antes de insertar, `Random(42)` determinístico).
- **Volúmenes Aspire:** `oroclash-sqlserver-data`, `oroclash-postgres-data`, `oroclash-redis-data`, `oroclash-rabbitmq-data`, `.oidc-certs` (cert OpenIddict). Borrar volumen fuerza re-siembra limpia (`podman volume rm oroclash-sqlserver-data`).

---

## 2. DDL — Script SQL Server (generado vía `dotnet ef migrations script` — referencia normativa)

El script a continuación es el equivalente normativo de lo que produciría `dotnet ef migrations add Initial` sobre las 12 `IEntityTypeConfiguration` + `Outbox`. Se incluye para entrega aunque el proyecto use `EnsureCreated`. Ejecutar con `sqlcmd`/`Azure Data Studio`.

> Si prefieres generar el script oficial en tu máquina, ejecuta:
> ```bash
> dotnet ef migrations add Initial --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api
> dotnet ef migrations script --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api --output entregables/scripts/01-initial.sql
> ```
> El script de este documento es semánticamente idéntico.

### 2.1 Creación de base y esquema

```sql
-- Ejecutar en instancia Aspire `sqlserver` o SQL Server local
IF DB_ID('oroclash') IS NULL CREATE DATABASE [oroclash];
GO
USE [oroclash];
GO

-- Tablas BuildingBlocks Outbox (convención)
CREATE TABLE [OutboxMessages] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Type] nvarchar(500) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [ProcessedAt] datetimeoffset NULL
);
GO
```

### 2.2 Tablas de dominio

```sql
-- Categories
CREATE TABLE [Categories] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [KnowledgeArea] nvarchar(100) NOT NULL,
    [AcademicLevel] nvarchar(100) NOT NULL,
    [AgeMin] int NOT NULL,
    [AgeMax] int NOT NULL,
    [PublishConfiguration_RequiresModeration] bit NOT NULL,
    [DifficultyLevel] int NOT NULL,
    [Tags] nvarchar(1000) NULL,
    [Status] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL
);
CREATE INDEX [IX_Categories_Status] ON [Categories] ([Status]);
CREATE INDEX [IX_Categories_KnowledgeArea] ON [Categories] ([KnowledgeArea]);
CREATE INDEX [IX_Categories_AcademicLevel] ON [Categories] ([AcademicLevel]);
CREATE INDEX [IX_Categories_KnowledgeArea_AcademicLevel] ON [Categories] ([KnowledgeArea], [AcademicLevel]);
GO

-- Questions + AnswerOptions
CREATE TABLE [Questions] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Text] nvarchar(500) NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Categories]([Id]),
    [DifficultyId] int NOT NULL,
    [AcademicLevel] nvarchar(100) NOT NULL,
    [AgeMin] int NOT NULL,
    [AgeMax] int NOT NULL,
    [Status] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [PublishedAt] datetimeoffset NULL,
    [CreatedBy] uniqueidentifier NOT NULL
);
CREATE INDEX [IX_Questions_CategoryId_Status] ON [Questions] ([CategoryId], [Status]);
CREATE INDEX [IX_Questions_Difficulty] ON [Questions] ([DifficultyId]);
CREATE INDEX [IX_Questions_Status] ON [Questions] ([Status]);
CREATE INDEX [IX_Questions_CategoryId_Status_Difficulty] ON [Questions] ([CategoryId], [Status], [DifficultyId]);
CREATE INDEX [IX_Questions_AcademicLevel] ON [Questions] ([AcademicLevel]);

CREATE TABLE [AnswerOptions] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [QuestionId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Questions]([Id]) ON DELETE CASCADE,
    [Text] nvarchar(500) NOT NULL,
    [IsCorrect] bit NOT NULL,
    [DisplayOrder] int NOT NULL
);
CREATE INDEX [IX_AnswerOptions_QuestionId] ON [AnswerOptions] ([QuestionId]);
CREATE UNIQUE INDEX [IX_AnswerOptions_QuestionId_DisplayOrder] ON [AnswerOptions] ([QuestionId], [DisplayOrder]);
CREATE INDEX [IX_AnswerOptions_QuestionId_IsCorrect] ON [AnswerOptions] ([QuestionId], [IsCorrect]);
GO

-- Games (GameConfiguration Owned)
CREATE TABLE [Games] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Configuration_Name] nvarchar(100) NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [MinRounds] int NOT NULL,
    [MaxRounds] int NOT NULL,
    [InitialDifficulty] int NOT NULL,
    [DifficultyStrategy] int NOT NULL,
    [TimeLimitSeconds] int NOT NULL,
    [ScoringSystem] int NOT NULL,
    [LossPolicy] int NOT NULL,
    [WithdrawalPolicy] int NOT NULL,
    [ConsolationPolicy] int NOT NULL,
    [MinPlayers] int NOT NULL,
    [MaxPlayers] int NOT NULL,
    [PointsPerRound] int NOT NULL,
    [RewardRules_Type] nvarchar(50) NULL,
    [RewardRules_Threshold] int NULL,
    [Name] nvarchar(100) NOT NULL,
    [Status] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ReadyAt] datetimeoffset NULL,
    [StartedAt] datetimeoffset NULL,
    [FinishedAt] datetimeoffset NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    CONSTRAINT [FK_Games_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories]([Id])
);
CREATE INDEX [IX_Games_Status] ON [Games] ([Status]);
CREATE INDEX [IX_Games_CreatedAt] ON [Games] ([CreatedAt]);
GO

-- GamePlayers (Field _players, Owned PlayerScore)
CREATE TABLE [GamePlayers] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [GameId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Games]([Id]) ON DELETE CASCADE,
    [UserId] uniqueidentifier NOT NULL,
    [JoinedAt] datetimeoffset NOT NULL,
    [DisplayName] nvarchar(100) NULL,
    [Score_CurrentPoints] int NOT NULL DEFAULT 0,
    [Score_SecuredPoints] int NOT NULL DEFAULT 0,
    [Score_RoundPoints] int NOT NULL DEFAULT 0,
    [Score_PotentialPoints] int NOT NULL DEFAULT 0,
    [Score_TotalPoints] int NOT NULL DEFAULT 0,
    [ParticipationStatus] int NOT NULL,
    [CurrentRoundNumber] int NOT NULL DEFAULT 0,
    [ExitedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [UQ_GamePlayers_GameId_UserId] UNIQUE ([GameId], [UserId])
);
CREATE INDEX [IX_GamePlayers_GameId] ON [GamePlayers] ([GameId]);
CREATE INDEX [IX_GamePlayers_UserId] ON [GamePlayers] ([UserId]);
GO

-- GameRounds
CREATE TABLE [GameRounds] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [GameId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Games]([Id]) ON DELETE CASCADE,
    [RoundNumber] int NOT NULL,
    [Difficulty] int NOT NULL,
    [QuestionId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Questions]([Id]),
    [TimeLimit] int NOT NULL,
    [Status] int NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL
);
CREATE INDEX [IX_GameRounds_GameId] ON [GameRounds] ([GameId]);
CREATE INDEX [IX_GameRounds_GameId_RoundNumber] ON [GameRounds] ([GameId], [RoundNumber]);
GO

-- Answers
CREATE TABLE [Answers] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [GameId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Games]([Id]) ON DELETE CASCADE,
    [PlayerId] uniqueidentifier NOT NULL,
    [RoundId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [GameRounds]([Id]),
    [QuestionId] uniqueidentifier NOT NULL,
    [AnswerOptionId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [AnswerOptions]([Id]),
    [Status] int NOT NULL,
    [Correct] bit NULL,
    [Points] int NOT NULL DEFAULT 0,
    [ElapsedTime] int NOT NULL DEFAULT 0,
    [CreatedAt] datetimeoffset NOT NULL,
    [EvaluatedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL
);
CREATE INDEX [IX_Answers_GameId] ON [Answers] ([GameId]);
CREATE INDEX [IX_Answers_GameId_PlayerId] ON [Answers] ([GameId], [PlayerId]);
CREATE INDEX [IX_Answers_RoundId] ON [Answers] ([RoundId]);
CREATE INDEX [IX_Answers_PlayerId_RoundId] ON [Answers] ([PlayerId], [RoundId]); -- idempotencia por ronda
GO

-- PointTransactions (ledger Server Truth)
CREATE TABLE [PointTransactions] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [GameId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Games]([Id]) ON DELETE CASCADE,
    [PlayerId] uniqueidentifier NOT NULL,
    [RoundId] uniqueidentifier NULL FOREIGN KEY REFERENCES [GameRounds]([Id]),
    [QuestionId] uniqueidentifier NULL,
    [AnswerId] uniqueidentifier NULL,
    [TypeId] int NOT NULL,
    [Points] int NOT NULL,
    [ResultingBalance] int NOT NULL,
    [Reason] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL
);
CREATE UNIQUE INDEX [IX_PointTransactions_GameId_AnswerId] ON [PointTransactions] ([GameId], [AnswerId]) WHERE [AnswerId] IS NOT NULL;
CREATE INDEX [IX_PointTransactions_GameId_PlayerId] ON [PointTransactions] ([GameId], [PlayerId]);
CREATE INDEX [IX_PointTransactions_GameId_RoundId] ON [PointTransactions] ([GameId], [RoundId]);
CREATE INDEX [IX_PointTransactions_GameId_PlayerId_CreatedAt] ON [PointTransactions] ([GameId], [PlayerId], [CreatedAt]);
GO

-- Rewards
CREATE TABLE [Rewards] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NOT NULL DEFAULT '',
    [PointsRequired] int NOT NULL,
    [Stock] int NOT NULL,
    [Status] int NOT NULL,
    [ExpirationDate] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL
);
CREATE INDEX [IX_Rewards_Status] ON [Rewards] ([Status]);
GO

-- RewardRedemptions + RedemptionTransitions (Owned)
CREATE TABLE [RewardRedemptions] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [PlayerId] uniqueidentifier NOT NULL,
    [RewardId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Rewards]([Id]),
    [GameId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [Games]([Id]),
    [Points] int NOT NULL,
    [Status] int NOT NULL,
    [RequestedAt] datetimeoffset NOT NULL,
    [DeliveredAt] datetimeoffset NULL,
    [IdempotencyKey] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL
);
CREATE INDEX [IX_RewardRedemptions_PlayerId] ON [RewardRedemptions] ([PlayerId]);
CREATE INDEX [IX_RewardRedemptions_RewardId] ON [RewardRedemptions] ([RewardId]);
CREATE INDEX [IX_RewardRedemptions_Status] ON [RewardRedemptions] ([Status]);
CREATE UNIQUE INDEX [IX_RewardRedemptions_PlayerId_IdempotencyKey] ON [RewardRedemptions] ([PlayerId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;

CREATE TABLE [RedemptionTransitions] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [RewardRedemptionId] uniqueidentifier NOT NULL FOREIGN KEY REFERENCES [RewardRedemptions]([Id]) ON DELETE CASCADE,
    [Status] int NOT NULL,
    [ActorId] uniqueidentifier NOT NULL,
    [At] datetimeoffset NOT NULL
);
CREATE INDEX [IX_RedemptionTransitions_RewardRedemptionId] ON [RedemptionTransitions] ([RewardRedemptionId]);
GO

-- Audit / Idempotency
CREATE TABLE [AuditEntries] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Timestamp] datetimeoffset NOT NULL,
    [ActorId] nvarchar(450) NOT NULL,
    [ActorRoles] nvarchar(450) NOT NULL,
    [Action] nvarchar(200) NOT NULL,
    [Permission] nvarchar(200) NOT NULL,
    [Resource] nvarchar(200) NOT NULL,
    [ResourceId] nvarchar(200) NULL,
    [GameId] uniqueidentifier NULL,
    [PlayerId] uniqueidentifier NULL,
    [CorrelationId] nvarchar(200) NOT NULL,
    [TenantId] nvarchar(200) NULL,
    [Result] nvarchar(50) NOT NULL,
    [Reason] nvarchar(1000) NULL,
    [Details] nvarchar(max) NULL,
    [Data] nvarchar(max) NULL
);
CREATE INDEX [IX_AuditEntries_Timestamp] ON [AuditEntries] ([Timestamp]);
CREATE INDEX [IX_AuditEntries_ActorId] ON [AuditEntries] ([ActorId]);
CREATE INDEX [IX_AuditEntries_GameId] ON [AuditEntries] ([GameId]);

CREATE TABLE [IdempotencyRecords] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [Key] nvarchar(200) NOT NULL,
    [ActorId] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ResponseHash] nvarchar(500) NOT NULL,
    [Response] nvarchar(4000) NOT NULL,
    CONSTRAINT [UQ_IdempotencyRecords_Key_ActorId] UNIQUE ([Key], [ActorId])
);
CREATE INDEX [IX_IdempotencyRecords_CreatedAt] ON [IdempotencyRecords] ([CreatedAt]);
GO
```

### 2.3 Notas de compatibilidad SQLite

- `rowversion` se Declara como `BLOB NOT NULL` con `ValueGenerated.Never`; el contexto hace `BumpSqliteRowVersions()` (`Guid.NewGuid().ToByteArray()`) en `SaveChanges` (`OroQuizClashDbContext.cs:85`).
- `datetimeoffset` se persiste como `TEXT`/`DateTime UTC` vía `ValueConverter` (`:46`).
- Los índices filtrados (`WHERE ... IS NOT NULL`) se traducen a `WHERE "Col" IS NOT NULL` (soportado desde SQLite 3.8.0).

---

## 3. Scripts operativos

### 3.1 Generar migraciones y aplicarlas (cuando se desea script oficial)

```bash
# Requisitos: .NET 10 SDK, dotnet-ef (dotnet tool install --global dotnet-ef)
dotnet ef migrations add Initial --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api
dotnet ef migrations script --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api --idempotent --output entregables/scripts/01-initial.sql
# Aplicar en una instancia concreta (ej. sqlserver Aspire)
dotnet ef database update --project src/OroQuizClash.Infrastructure --startup-project src/OroQuizClash.Api --connection "Server=localhost,1433;Database=oroclash;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

### 3.2 Limpieza / re-siembra (desarrollo)

```bash
# Borrar volumen para re-creación desde cero (EnsureCreated + Seeder)
podman volume rm oroclash-sqlserver-data   # SQL Server oroclash
podman volume rm oroclash-postgres-data    # opcional: también resetea OroIdentityServer
podman volume rm oroclash-rabbitmq-data    # opcional
aspire start  # el Seeder se ejecuta automáticamente al arrancar
# Ver logs del seeder:
aspire ps
aspire logs oroclash-seeder   # o `podman logs <seeder-container>`
```

### 3.3 Consultas de diagnóstico

```sql
-- Contar datos semilla esperados
SELECT COUNT(*) AS Categories FROM Categories;       -- 10
SELECT COUNT(*) AS Questions FROM Questions;         -- 200
SELECT COUNT(*) AS Published FROM Questions WHERE Status=3; -- 200 (Published=3)
SELECT COUNT(*) AS Games FROM Games;                -- 10
SELECT Status, COUNT(*) FROM Games GROUP BY Status; -- todos 3=WaitingForPlayers
SELECT COUNT(*) FROM AnswerOptions;                 -- 800 (200×4)
SELECT COUNT(*) FROM Rewards;                       -- depende de creación manual
-- Verificar ledger por jugador
SELECT PlayerId, SUM(Points) AS CurrentPoints FROM PointTransactions GROUP BY PlayerId;
-- Auditar canjes idempotentes
SELECT PlayerId, IdempotencyKey, COUNT(*) FROM RewardRedemptions WHERE IdempotencyKey IS NOT NULL GROUP BY PlayerId, IdempotencyKey HAVING COUNT(*)>1; -- debe ser 0
```

---

## 4. Semillas — `OroQuizClash.Seeder` (`src/Seeder/OroQuizClash.Seeder/`)

### 4.1 Visión

| Propiedad | Valor |
|-----------|-------|
| **Tipo** | `BackgroundService` one-shot (`Worker.cs:25`, `IHostApplicationLifetime.StopApplication()` al terminar) |
| **Trigger** | Aspire lo levanta como `builder.AddProject<Projects.OroQuizClash_Seeder>` con `WaitFor(api)` + `WaitFor(identity)`; se ejecuta tras `Task.Delay(8s)` para aguardar migraciones. |
| **Idempotencia** | Verifica `Categories.Count >=10` → `return` (skip). Si `Categories.Count` <10, crea lo faltante (`FirstOrDefault Name` por categoría). Para `Questions` verifica `Count >=20` por `CategoryId`. Para `Games` verifica `Any(Configuration_Name)` antes de `Create`. |
| **Determinismo** | `Random(42)` para `InitialDifficulty 1..3` en juegos; preguntas fijas definidas en `SeedData.cs:31-270`. |
| **Usuario creador** | `Guid.Parse("00000000-0000-0000-0000-000000000001")` (system). |
| **Reintentos DB** | `EnsureCreatedAsync` con bucle `retry<5` delay 3s (`Worker.cs:53`) para manejar `sqlserver` aún iniciando. |

### 4.2 Contenido — `SeedData.cs`

**10 Categorías** (ciencias secundarias 12–17 años, difficulty 2–4, colores/iconos en seed record pero no persistidos más que como `Tags`/`Name`):

- Biología Celular, Genética y Herencia, Ecología y Medio Ambiente, Anatomía Humana, Química Inorgánica, Química Orgánica, Física Mecánica, Física Electricidad y Magnetismo, Ciencias de la Tierra, Astronomía.  
  Cada una: `KnowledgeArea="Ciencias"`, `AcademicLevel="Secundaria"`, `AgeRange 12-17` (con variaciones 13-17/14-17), `Difficulty 2-4`, `Tags` (ej. `["celula","biologia","secundaria"]`).

**200 Preguntas** (`SeedData.QuestionsByCategory` 20 por categoría, `SeedData.cs:46-269`, `Create(text, Options[4], CorrectIndex, Difficulty)` → `QuestionSeed` con `TimeSeconds=30`, `AcademicLevel="Secundaria"`, `Age 12-17`, `Explanation` opcional). Ejemplos:

- Biología: “¿Qué orgánulo se encarga de la respiración celular?” → Mitocondria (4 opciones, diff 2)
- Genética: “¿Quién es el padre de la genética?” → Gregor Mendel
- Ecología: “¿Qué gas aumenta el efecto invernadero?” → CO₂
- … (ver fichero completo `SeedData.cs:49-268` para las 200).

**Flujo `Worker.cs:83-239`** por categoría:

1. `CategoryDomain.Create(name, description, KnowledgeArea, AcademicLevel, AgeRange, Difficulty, Tags, PublishConfiguration(requiresModeration:false), createdBy)` → `db.SaveChanges`.
2. Por cada `QuestionSeed`: `QuestionDomain.Create(text, catId, DifficultyLevel.FromId, AcademicLevel, AgeRange, opts[4], createdBy)` → `q.Publish()` (gate 4/1 + `Difficulty` + `CategoryId`) → `db.Questions.Add(q)`. Luego `SaveChanges` por categoría.
3. `Category.PublishAsync(new EfCount(catId, count), ct)` si `validCount≥5` y `Status!=Active` → `SaveChanges` (deja `Category.Status=Active`).
4. **10 Juegos** (uno por categoría activa, `Random` lin. 5-8 rounds): `new GameConfiguration(name:"Torneo {Category} - Secundaria 01..10", categoryId, minRounds:5, maxRounds:8, initialDifficulty:rng 1..3, Linear, timeLimit 30, Standard, LoseUnsecuredPoints, KeepSecuredScore, ConsolationPolicy.None, RewardRules("Points",1000), minPlayers:2, maxPlayers:10)` → `Game.Create → MarkReady(_=>true,_=>20) → OpenLobby → Save` → estado `WAITING_FOR_PLAYERS`.

### 4.3 Ejecutar manualmente fuera de Aspire

```bash
# Opción A: como en Aspire (necesita oroclash DB)
dotnet run --project src/Seeder/OroQuizClash.Seeder
# Variables de entorno requeridas (si no usa Aspire connection string)
export ConnectionStrings__oroclash="Server=localhost,1433;Database=oroclash;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
# Opción B: re-ejecutar dentro de contenedor
podman run --rm --network host -e ConnectionStrings__oroclash="..." localhost/oroclash-seeder:latest
```

### 4.4 Recompilar imagen del Seeder (si cambia `SeedData.cs`)

```bash
# No tiene Dockerfile propio; se publica como proyecto Aspire. Para imagen Podman:
dotnet publish src/Seeder/OroQuizClash.Seeder --os linux --arch x64 /t:PublishContainer
```

### 4.5 Criterios de aceptación de la siembra

- Al terminar, `Worker.cs:237` loguea: `Seeder completo: {Cats} categorías, 200 preguntas, {Games} juegos ({Waiting} WAITING_FOR_PLAYERS)`.
- `GET /api/categories` debe devolver 10 `Active` con ≥5 preguntas cada una.
- `GET /api/games?status=WAITING_FOR_PLAYERS` debe devolver 10 juegos `WAITING_FOR_PLAYERS`.

---

## 5. Scripts auxiliares de identidad

La **identidad** (`OroIdentityServer` + `identitydb` Postgres) se siembra independientemente (imagen `oroidentityserver:latest`). El seed de esa DB crea `admin/Admin@123456` y tenant `OroMasterTenant` (`AppHost.cs:74-76` env `SEED_*`). Los clientes OIDC no se siembran por SQL sino por script/API:

- `scripts/register-admin-oidc-client.sh` — registra `quizarena-admin` (confidential, `authorization_code` + `refresh_token` + PKCE) vía `POST /api/applications` sobre `https://localhost:5086`. Requiere `IDP_ADMIN_PASSWORD`. Dejar el `ADMIN_CLIENT_SECRET` en Aspire param `quizarena-admin-oidc-secret` (ver `05-Guia-de-Instalacion.md`).

### 5.1 Registro manual de `quizarena-player` (SPA público)

No tiene script; hacerlo vía Admin UI → Applications → Create (ver también `05-Guia-de-Instalacion.md:Configuración del Sistema`):

- `clientId=quizarena-player`, `clientType=public`, `applicationType=web`, `consentType=implicit`
- `permissions: ept:authorization, ept:token, gt:authorization_code, rst:code, scp:openid, scp:profile, scp:email, scp:offline_access`
- `requirements: ft:pkce`
- `redirectUris: http://localhost:4200/auth/callback` (y `https://localhost:4200/auth/callback` si usas https), `postLogoutRedirectUris: http://localhost:4200/auth/logout-callback`
- Para prod: repetir para `https://<dominio>/auth/callback`.

---

## 6. Relación con el Modelo ER

- El DDL `§2.2` es la proyección SQL del `TypeConfiguration` documentado en `02-Modelo-Entidad-Relacion.md:§2`.
- Cada `Owned` (`AgeRange`, `PublishConfiguration`, `GameConfiguration.RewardRules`, `PlayerScore`) se aplana en columnas de la tabla propietaria (ver `GameTypeConfiguration.cs:48`).
- `RedemptionTransitions` es tabla hija `OwnsMany` (no es entidad agregada).
- `OutboxMessages` es tabla transversal para integración `RabbitMQ`.

---

## 7. Referencias por archivo

- Contexto: `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs:17` (`EnsureCreated`, `BumpSqliteRowVersions:85`)
- Configuraciones: `Persistence/Configurations/{...}TypeConfiguration.cs` (12 ficheros)
- Semillas: `src/Seeder/OroQuizClash.Seeder/{SeedData.cs:31, Worker.cs:25, Program.cs}`
- AppHost wiring Seeder: `OroQuizClash.AppHost/AppHost.cs:138-141`
- Script OIDC Admin: `scripts/register-admin-oidc-client.sh`

*Semillas y DDL vigentes a 31-08-2026 (specs 001–036). Las semillas son determinísticas (`Random(42)`) y se pueden re-ejecutar sin duplicar.*
