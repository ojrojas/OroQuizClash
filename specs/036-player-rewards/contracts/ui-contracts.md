# UI Contracts: Player Rewards (036)

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Flujo `Points Wallet` → `Rewards Catalog` 4 métricas + `Reward Detail` + `Redeem` 2 pasos + `Confirmation` + `Redemption History` + `Consolation`, `data-theme="player"` `prefers-reduced-motion`.

## 1. Points Wallet + Rewards Catalog (`app-rewards-catalog`)

### Route

```
/rewards?q=gameId (optional) — authGuard + mustChangePasswordGuard
```

### Layout

- `data-theme="player"` root `display:grid` `gap:var(--space-4)` `padding:var(--space-4)` `max-width:1200px` centrado.
- Wallet header `card` `background:var(--color-surface)` `border:1px solid var(--color-border)` `border-radius:var(--radius-lg)` `padding:var(--space-6)` `display:flex; justify-content:space-between` `Available Points` grande `font-size:var(--text-2xl)` `color:var(--color-primary)` con `aria-live="polite"` `role="status"`.

### Catalog Grid

- `display:grid` `grid-template-columns:1fr` 375px → `1fr 1fr` 768px → `1fr 1fr 1fr 1fr` 1536px `gap:var(--space-3)` sin scroll horizontal.
- Card per `Reward` `min-height:160px` `padding:var(--space-3)` `border-radius:var(--radius-md)` `border:1px solid var(--color-border)` `background:var(--color-surface)` + badge `Reward Status`.

### Card Content

- `h3` nombre recompensa.
- `Required Points: {{reward.pointsRequired}} pts` `aria-label="Required Points 800"` `role="status" aria-live="polite"`.
- `Reward Status` badge:
  - `Canjeable` → `background:var(--color-success)` `color:white` `aria-label="Canjeable"`
  - `Puntos insuficientes` → `background:var(--color-warning)` `color:white` `aria-label="Puntos insuficientes, te faltan 700"`
  - `Agotada` → `background:var(--color-destructive)` `color:white`
  - `No disponible` → `background:var(--color-muted)` `color:var(--color-muted-foreground)`
- `Remaining Points` en card: si canjeable `Quedan 400 pts` `aria-label="Remaining Points 400"`; si faltante `Te faltan 700 pts`.
- Botón `Ver detalle` `min-height:44px` `aria-label="Ver detalle Pack Oro"` → navega `/rewards/:rewardId`.

### Empty / Loading / Error

- Loading: `app-loading-skeleton` `aria-busy="true"` `aria-live="polite"` 6 skeletons.
- Empty: `app-empty-state` "No hay recompensas disponibles" + CTA a `/player` si `rewards.length===0`.
- Error: `app-error-state` con `CorrelationId/TraceId` + `Retry` 44px `aria-live="assertive"`.

### Accessibility

- `role="group" aria-label="Catálogo de recompensas"` para grid.
- `prefers-reduced-motion: reduce` `animation:none`.

## 2. Reward Detail + Redeem 2 pasos (`app-reward-detail`)

### Route

```
/rewards/:rewardId?gameId=...
```

### Layout

- `max-width:600px` centrado `padding:var(--space-6)` `gap:var(--space-4)` vertical.
- Header nombre + descripción.
- Métricas `role="group" aria-label="Puntuaciones"` `display:flex; flex-direction:column; gap:var(--space-2)`:
  - `Available Points {{wallet.availablePoints}} pts` `aria-live="polite"` `aria-label="Available Points 1200"`
  - `Required Points {{reward.pointsRequired}} pts` `aria-label="Required Points 800"`
  - `Remaining Points {{remainingDisplay}}` `remainingDisplay = canjeable ? (Available-Required)+" pts" : "Te faltan "+(Required-Available)+" pts"` `aria-live="polite"`
  - `Reward Status` badge igual que catalog.

### Redeem Button

- `Canjear` `min-height:44px` `min-width:44px` `aria-label="Canjear recompensa"` `disabled` si `!isRedeemable` (`Available < Required` o no `available`) con tooltip `Necesitas 700 puntos más` o `Agotada`.
- Click `Canjear` → abre diálogo `showConfirm=true`.

### Dialog Confirmación

- `role="dialog"` `aria-modal="true"` `aria-label="Confirmar canje"` `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)` `display:flex; align-items:center; justify-content:center` `gap:var(--space-3)` `max-width:400px` `background:var(--color-surface)` `border:1px solid var(--color-border)` `border-radius:var(--radius-lg)` `padding:var(--space-6)` `display:flex; flex-direction:column; gap:var(--space-3)` tokens.
- Contenido:
  - `h2` "Confirmar canje"
  - Resumen `Required 800 pts` + `Remaining 400 pts` + `Disponible 1200 pts` `role="group" aria-label="Resumen canje"`
  - Warning `role="alert" aria-live="assertive"` `color:var(--color-warning)` "¿Confirmar canje de Pack Oro por 800 puntos?"
  - Botones `Confirmar` `min-height:44px` `aria-label="Confirmar canje"` → `store.redeem()` `X-Idempotency-Key` `idemp-redeem-{rewardId}` + `Cancelar` `min-height:44px` `aria-label="Cancelar"` → `showConfirm=false` sin llamada; `Escape`/`backdrop` también cierra.
- Tokens only: `var(--space-*)`, `var(--color-*)`, `var(--radius-*)`; no literales excepto `400px`/`rgba(0,0,0,0.5)`.

### Confirmation View

- Tras `Redeem` 200 `REQUESTED`, muestra `Confirmation` `role="status" aria-live="assertive"`:
  - `¡Canje realizado!` `Pack Oro` `Consumidos 800 pts` `Restantes 400 pts` `Referencia {{redemptionId}}` `Estado Canjeada` `Fecha {{requestedAt}}`
  - CTA `Ver historial` → `/rewards/history` y `Seguir explorando` → `/rewards`.

### Error Handling

- `429` `Retry-After` visible; `409 RewardUnavailable`/`InsufficientPoints` → `ErrorState` `CorrelationId` + `Retry` reusa misma `X-Idempotency-Key` per `rewardId`; `401` → silentRenew.

## 3. Redemption History (`app-redemption-history`)

### Route

```
/rewards/history
```

### Layout

- `max-width:800px` centrado, lista vertical `gap:var(--space-3)` `aria-label="Historial de canjes"` `role="list"`.
- Row `role="listitem"` `padding:var(--space-3)` `border:1px solid var(--color-border)` `border-radius:var(--radius-md)` `display:flex; justify-content:space-between` `min-height:44px`.
- Columnas: `rewardName` + `points` `{{item.points}} pts` + `status` badge `Canjeada`/`Consolation` + `requestedAt` fecha + `reference` truncada.
- `Consolation` badge diferenciado `background:var(--color-info,#3B82F6)` `color:white` `aria-label="Recompensa de consolación"` + motivo `eligibilityReason` tooltip.

### Empty State

- `app-empty-state` "Aún no has canjeado recompensas" + CTA `Explorar recompensas` → `/rewards`.

### Pagination

- Paginado server `page/pageSize` pero UI primera carga `pageSize 20` + botón `Cargar más` `min-height:44px` si `hasNext`.

## 4. Consolation Badge (`app-consolation-badge`)

- Reutilizado en `Wallet` y `History` cuando `isConsolation===true`: `span` `background:var(--color-info)` `border-radius:var(--radius-full)` `padding:var(--space-1) var(--space-2)` `font-size:var(--text-xs)` `Consolation`.

## 5. Tokens & A11y Global

- `data-theme="player"` en root; todos los colores/espaciados/radios via `var(--*)` 0 literales.
- Targets ≥44px para todos los botones (`Canjear`, `Ver detalle`, `Confirmar`, `Cancelar`, `Retry`, `Cargar más`).
- `outline:2px solid var(--color-primary)` en focus visible.
- `axe` 0 violations `role="dialog"` `aria-modal` `aria-live` warnings `aria-label` descriptivos.

