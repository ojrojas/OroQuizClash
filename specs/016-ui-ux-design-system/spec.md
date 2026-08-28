# Feature Specification: UI/UX Design System

**Feature Branch**: `016-ui-ux-design-system`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "016 — UI/UX Design System Objetivo Definir el sistema de diseño visual, interacción, accesibilidad, responsive design, motion y experiencia de usuario que será compartido por las dos aplicaciones web de QuizArena. Descripción Esta especificación establece la identidad visual de QuizArena y constituye la fuente de verdad para la implementación de: Aplicación administrativa. Aplicación de juego. Componentes compartidos conceptualmente. Design tokens. Tipografía. Colores. Espaciado. Iconografía. Estados. Animaciones. Accesibilidad. Responsive design. El proceso de diseño deberá utilizar UI/UX Pro Max Skill como herramienta de apoyo para generar y validar el Design System. La especificación deberá establecer dos expresiones visuales: QuizArena Design System ├── Administration Experience │       └── Blazor └── Player Experience └── Angular 22 La experiencia del jugador deberá tener una estética cinemática, premium y de concurso de conocimiento, inspirada en la tensión y progresión de los game shows, sin copiar identidad visual, branding o assets de programas existentes. La aplicación administrativa deberá tener una estética enterprise SaaS moderna, profesional, productiva y orientada a datos. BLOQUE A — ADMINISTRACIÓN La aplicación administrativa será una Blazor Web App sobre .NET 11. Su responsabilidad será configurar, administrar, operar y supervisar QuizArena."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Administration Experience — Enterprise SaaS operativa (Priority: P1) 🎯 MVP

Como administrador, gestor de contenido o operador de QuizArena, quiero una interfaz administrativa con estética enterprise SaaS moderna, profesional, productiva y orientada a datos, de forma que pueda configurar, administrar, operar y supervisar juegos, categorías, preguntas, jugadores y recompensas con eficiencia y sin fatiga visual en sesiones prolongadas.

**Why this priority**: Es la herramienta de operación diaria; sin una experiencia administrativa coherente, densa en datos pero legible, la configuración (SPEC-001/002/003) y la supervisión (SPEC-014/015) se vuelven propensas a error. Entrega valor independiente: aun sin la experiencia de jugador, el backoffice ya debe ser usable y accesible para crear y operar partidas.

**Independent Test**: Con un operador autenticado (ADMIN/GAME_MANAGER), recorrer flujos críticos en desktop 1440px y laptop 1280px: crear juego (SPEC-001), publicar categoría (SPEC-002), crear pregunta con 4 opciones (SPEC-003), consultar audit trail y reportes (SPEC-014/015). Verificar densidad de información, jerarquía, estados, navegación y legibilidad sin necesidad de la app de juego. Medir SUS/task-completion.

**Acceptance Scenarios**:

1. **Given** un administrador en `/admin/games` con lista de 50 juegos, **When** filtra por estado `FINISHED` y ordena por `CreatedAt`, **Then** ve tabla densa con paginación, filtros persistentes, empty-state y skeleton coherentes, con contraste ≥ 4.5:1 y navegación por teclado completa.
2. **Given** creación de juego con 12 campos (nombre, categoría, rondas, dificultad, tiempo, políticas), **When** el formulario presenta errores (minRondas < 5, categoría no lista), **Then** cada campo muestra estado `error` con mensaje inline, ícono y `aria-describedby`, y el foco se mueve al primer error.
3. **Given** un operador en sesión de 30 minutos configurando contenido, **When** interactúa con modales, drawers y tablas, **Then** la experiencia mantiene jerarquía tipográfica consistente, espaciado 4/8px, y no presenta fatiga por bajo contraste o animaciones intrusivas; métrica SUS ≥ 75 en prueba con 5 operadores.

---

### User Story 2 — Player Experience — Cinemática premium de concurso (Priority: P1)

Como jugador participante en QuizArena, quiero una experiencia de juego cinemática, premium y de concurso de conocimiento que comunique tensión, progresión y recompensa, inspirada en la progresión de los game shows pero con identidad propia, de forma que cada ronda, pregunta y puntuación se sienta significativa y emocionante sin copiar branding de programas existentes.

**Why this priority**: Es la cara pública del producto y el diferenciador emocional; sin una estética de concurso bien resuelta (luz, progresión, feedback), el motor de juego (SPEC-005/006/007/011/012) se percibe como formulario. Es P1 junto con US1 porque ambas expresiones son la promesa del sistema dual. Independiente de la administración: puede validarse en flujo de juego simulado sin backoffice.

**Independent Test**: Con un jugador (PLAYER) en partida de 5 rondas en desktop, tablet y móvil, recorrer lobby → ronda activa → pregunta con timer → evaluación → score/leaderboard → withdraw/finish, con estados de tensión (countdown), celebración y penalización. Validar identidad visual propia (no plagio), motion significativo y premium feel mediante test A/B y heurística estética con 8 jugadores.

**Acceptance Scenarios**:

1. **Given** una partida en `IN_PROGRESS` ronda 3/5 con `TimeLimit=30s`, **When** se presenta la pregunta, **Then** el jugador ve layout centrado cinemático con progreso (3/5), timer prominente con cambio de estado a `warning` <10s y `critical` <5s, 4 opciones con estados `default/hover/selected/disabled`, sin revelar `IsCorrect` hasta evaluación del servidor.
2. **Given** una respuesta evaluada como correcta con `PointsAwarded`, **When** se muestra el resultado, **Then** aparece feedback celebratorio contenido (micro-animación + cambio cromático + delta de puntos) ≤ 600 ms, sin bloquear la siguiente ronda, y el leaderboard refleja el nuevo puntaje derivado del ledger.
3. **Given** identidad visual del juego en cualquier pantalla, **When** un auditor compara con game shows existentes, **Then** no hay copia de logos, paletas propietarias, tipografías registradas ni assets; la estética es original (oscuro premium + acento propio) y documentada como referencia de inspiración, no réplica.

---

### User Story 3 — Fundación compartida — Design Tokens y componentes conceptuales (Priority: P1)

Como diseñador y desarrollador del sistema, quiero una fundación compartida de design tokens (color, tipografía, espaciado, elevación, iconografía, estados, motion, breakpoints) y componentes conceptuales documentados como fuente de verdad, de forma que ambas aplicaciones (Blazor y Angular 22) implementen la misma identidad con expresiones distintas sin divergir.

**Why this priority**: Es el contrato que evita drift entre Administration y Player; sin tokens, cada app inventa su gris, su radio y su sombra. Es P1 porque bloquea la implementación consistente de US1 y US2. Valor independiente: los tokens pueden auditarse estáticamente sin app corriendo.

**Independent Test**: Auditar el catálogo de tokens y componentes contra el spec: verificar existencia de `color.*`, `typography.*`, `spacing.*`, `radius.*`, `elevation.*`, `motion.*`, `breakpoint.*`, con nombres, valores y uso documentado; comparar snapshot de tokens entre Blazor y Angular (valores deben coincidir salvo `theme` de expresión); verificar que no existen colores/espaciados hardcodeados fuera de tokens en 10 pantallas de muestra.

**Acceptance Scenarios**:

1. **Given** el catálogo de tokens publicado, **When** un desarrollador necesita un color de error, **Then** usa `color.feedback.error.500` (y no un hex literal) y el valor es idéntico en las dos expresiones salvo adaptación de tema.
2. **Given** un componente conceptual `Button` con variantes `primary/secondary/ghost/destructive` y estados `default/hover/active/focus/disabled/loading`, **When** se implementa en Blazor y Angular, **Then** ambas variantes respetan mismos tokens de tipografía, espaciado y motion, difiriendo solo en tratamiento visual de expresión (SaaS sobrio vs cinemático) documentado.
3. **Given** un nuevo componente `QuestionCard`, **When** se documenta, **Then** incluye anatomía, props conceptuales, estados, a11y, responsive y motion, y referencia explícita a tokens usados.

---

### User Story 4 — Accesibilidad, responsive y motion inclusivo (Priority: P2)

Como jugador o administrador con diversidad de capacidades, dispositivos y preferencias de movimiento, quiero que ambas experiencias sean accesibles (WCAG 2.2 AA), completamente responsivas y con motion respetuoso, de forma que pueda jugar u operar desde móvil, teclado, lector de pantalla y con `prefers-reduced-motion` sin perder funcionalidad ni información.

**Why this priority**: Es requisito de cumplimiento y alcance (móvil es canal primario de jugador); sin a11y/responsive/motion, el sistema excluye usuarios y falla auditorías. Es P2 porque depende de que existan tokens y pantallas (US1–US3) que hacer accesibles, pero entrega valor propio verificable con tests automáticos y manuales.

**Independent Test**: Ejecutar suite a11y (axe) + navegación teclado + lector (NVDA/VoiceOver) sobre 8 flujos clave en los 4 breakpoints normativos Addendum 2 §7 (`375px`, `768px`, `1024px`, `1440px`) con `prefers-reduced-motion` on/off. Verificar contraste, foco visible, labels, landmarks, y que animaciones se reducen a `fade` o se deshabilitan; verificar que no hay scroll horizontal y que layouts adaptan (no solo escalan).

**Acceptance Scenarios**:

1. **Given** cualquier pantalla en `light` o `dark`/`cinematic`, **When** se mide contraste texto/fondo, **Then** ≥ 4.5:1 para texto normal, ≥ 3:1 para grande, y foco visible ≥ 3:1 con anillo `focus-ring` de token.
2. **Given** navegación solo con teclado, **When** se recorre un flujo de 5 pasos (crear juego o responder 5 preguntas), **Then** todo control es alcanzable, orden lógico, sin trampa de foco, y modales/drawers gestionan `aria-modal` y retorno de foco.
3. **Given** `prefers-reduced-motion: reduce` activado, **When** ocurre una transición cinemática (cambio de ronda, feedback de respuesta), **Then** la animación se sustituye por `opacity` ≤ 200 ms o se omite, sin pérdida de información y sin parpadeo.

---

### User Story 5 — Validación con UI/UX Pro Max Skill y handoff (Priority: P2)

Como responsable de diseño, quiero que el proceso de definición y validación del Design System utilice UI/UX Pro Max Skill como apoyo para generar, auditar y validar tokens, componentes, paletas, tipografías y patrones, de forma que el sistema tenga respaldo metodológico, trazabilidad y criterio experto antes de construir.

**Why this priority**: Es la garantía de calidad del sistema dual (evita decisiones arbitrarias de color/tipografía/motion); es P2 porque presupone que hay un sistema que validar, pero asegura que el handoff a Blazor/Angular no parta de supuestos sin validar.

**Independent Test**: Ejecutar checklist de UI/UX Pro Max Skill sobre el Design System con el prompt canónico Addendum 2 §12 (QuizArena premium multiplayer, Angular 22 cinemático vs Blazor SaaS, evitar Bootstrap/glass/neón/gradientes AI, generar style/palette/typography/spacing/radius/elevation/motion/iconography/buttons/cards/forms/dialogs/tables/navigation/notifications/game-controls/timers/progress/score/reward/leaderboard/responsive/a11y/game-states/admin-states): validar paletas (192 palettes reasoning), tipografías (74 pairings), espaciado, componentes, motion (17 presets) y responsive; registrar hallazgos y correcciones; verificar `design-system/MASTER.md` + overrides por página.

**Acceptance Scenarios**:

1. **Given** la propuesta de paleta Administration (enterprise) y Player (cinemática), **When** se valida con UI/UX Pro Max, **Then** cada paleta pasa auditoría de contraste AA, armonía y uso (primary/neutral/feedback) documentada con alternativas descartadas.
2. **Given** el set tipográfico propuesto, **When** se valida, **Then** el pairing elegido está entre los recomendados o justificado por ADR con métricas de legibilidad y jerarquía.
3. **Given** el handoff a implementación, **When** se entrega, **Then** existe `design-tokens.json` (o equivalente agnóstico), spec de componentes en formato consumible por ambas apps, y reporte de validación UI/UX Pro Max archivado en `specs/016-ui-ux-design-system/`.

---

### Edge Cases

- ¿Qué ocurre cuando el usuario activa `high contrast` o `forced-colors` del SO? Los tokens deben tener fallback `forced-colors` y bordes visibles sin depender solo de color; el foco y los estados permanecen distinguibles (Addendum 2 §8).
- ¿Qué ocurre cuando `prefers-reduced-motion` está activo en la experiencia Player cinemática? Las animaciones de tensión (timer pulsante, transiciones de ronda) degradan a `opacity`/`transform` sutiles ≤200 ms o se omiten; el timer sigue comunicando `warning/critical` por color + ícono + texto/aria, no solo por animación; nunca bloquea la ventana de respuesta (Addendum 2 §6).
- ¿Qué ocurre en viewport 320px/375px (móvil pequeño) con pregunta de texto largo + 4 opciones? El layout reflow sin scroll horizontal, opciones apiladas, tipografía fluida `clamp()`, y tiempo/leaderboard colapsables pero accesibles; verificado en 375/768/1024/1440.
- ¿Qué ocurre cuando el contenido es muy denso (admin tabla 100 filas, 12 columnas)? La tabla ofrece paginación, densidad `comfortable/compact`, sticky header, y no rompe layout en 1024px; se evita overflow no gestionado.
- ¿Qué ocurre cuando un token es usado fuera de su propósito (ej. `color.feedback.error` para texto decorativo)? La guía de uso lo prohíbe y la auditoría de tokens lo marca como violación; el Visual Quality Gate lo detecta (Addendum 2 §12).
- ¿Qué ocurre cuando una nueva feature propone un color/espaciado no tokenizado? Debe proponerse como nuevo token vía ADR y validación UI/UX Pro Max, no hardcodearse; CI de tokens falla si hay literales fuera de catálogo; actualizado vía SDD en `design-system/MASTER.md`.
- ¿Qué ocurre cuando Player y Administration necesitan el mismo componente con distinta expresión (ej. Button)? El componente conceptual es único; la expresión se resuelve por `theme` (SaaS `Command Center` vs Cinematic `Game Show`) con mismos props/estados (Addendum 2 §9) pero distintos valores de elevación/radio/motion documentados en overrides.
- ¿Qué ocurre con modo oscuro en Administration vs cinemático oscuro por defecto en Player? Administration soporta `light` por defecto + `dark` opcional con mismos tokens; Player es `dark cinematic` por defecto con `light` solo si se justifica, sin duplicar tokens base (Addendum 2 §10).
- ¿Qué ocurre cuando la UI en tiempo real recibe un evento pero el estado autoritativo difiere? La UI MUST reconciliar desde `Backend State → Realtime Event → Client State → UI` (Addendum 2 §11) y nunca inferir corrección/puntos/avance solo desde animación o timer local; player-specific events solo actualizan la sesión privada.

## Requirements *(mandatory)*

### Functional Requirements

**Fundación — Design Tokens (fuente de verdad agnóstica)**

- **FR-001**: El sistema MUST definir y documentar design tokens como fuente de verdad agnóstica (no atada a framework) para: `color` (primitive + semantic), `typography` (familia, escala, peso, línea, tracking), `spacing` (escala 4/8), `radius`, `elevation` (sombra/blur), `border`, `opacity`, `z-index`, `breakpoint`, `motion` (duración, easing, preset) e `iconography` (grid, tamaño, trazo).
- **FR-002**: El sistema MUST exponer tokens en formato serializable y versionado (ej. `design-tokens.json` / Style Dictionary) con nombres estables (`color.primary.500`, `spacing.4`, `typography.heading.l`) y valores únicos; cualquier color/espaciado/radio/sombra usado en UI MUST provenir de un token, no de literales dispersos.
- **FR-003**: El sistema MUST definir escala de color con roles: `primary`, `neutral`, `feedback` (`success/warning/error/info`), `surface`/`background`/`foreground`, y estados `default/hover/active/focus/disabled` derivados por token, no por hardcode; la paleta MUST pasar WCAG AA en combinaciones texto/fondo documentadas.
- **FR-004**: El sistema MUST definir escala tipográfica fluida y jerarquía (display/heading/title/body/label/caption) con familia base legible para SaaS y complementaria para cinemática, pesos 400/500/600/700, y `line-height`/`tracking` por nivel; los tamaños MUST usar `clamp()` o escala responsive documentada.
- **FR-005**: El sistema MUST definir escala de espaciado 4px base (4, 8, 12, 16, 24, 32, 48, 64...), radios (4/8/12/16/24), elevaciones (0–4 niveles) y breakpoints compartidos por ambas expresiones alineados con Constitution Addendum 2 §7: mínimo normativo `375px`, `768px`, `1024px`, `1440px` (con extensiones `xs 360`, `sm 640`, `xl 1280`, `2xl 1536` cuando aporten valor) y grid 4/8/12 cols; los layouts MUST adaptar (no solo escalar) preservando legibilidad de pregunta, accesibilidad de respuestas, visibilidad de timer/score/acción primaria.
- **FR-006**: El sistema MUST definir motion tokens: duraciones (100/200/300/500 ms), easings (`ease-out`, `ease-in-out`, `spring` sutil) y presets por propósito (`fade`, `slide`, `scale`, `timer-pulse`); toda animación MUST referenciar un motion token y respetar `prefers-reduced-motion`.

**Dos expresiones visuales**

- **FR-007**: El sistema MUST establecer dos expresiones visuales sobre los mismos tokens base: `Administration` (enterprise SaaS — clara, densa, sobria, orientada a datos, light por defecto) y `Player` (cinemática premium — oscura, inmersiva, con tensión/progresión, sin copiar identidad de programas existentes), documentando diferencias por `theme` (color surface, elevación, radio, motion) sin duplicar tokens primitivos.
- **FR-008**: La expresión **Administration (Blazor)** MUST priorizar densidad productiva: tablas densas, filtros persistentes, navegación lateral/colapsable, jerarquía tipográfica sobria, superficies claras con acento contenido, y feedback inline (no modal abusivo); la estética MUST comunicar profesionalismo y confianza.
- **FR-009**: La expresión **Player (Angular 22)** MUST priorizar inmersión de concurso: layout centrado, progresión visible (ronda/total, nivel), timer prominente con estados `default/warning/critical`, feedback celebratorio/contenido, tipografía con mayor contraste y presencia, superficies oscuras con acento luminoso; MUST ser original y documentar referencias de inspiración genérica (tensión/progresión) sin replicar branding.
- **FR-010**: Ambas expresiones MUST compartir conceptualmente el mismo catálogo de componentes (Button, Input, Card, Table, Modal, Drawer, Badge, Progress, Timer, QuestionCard, Leaderboard, etc.) con anatomía, variantes, props conceptuales y estados idénticos; la diferencia es solo de `theme` (valores de token aplicados), no de API conceptual.

**Componentes, estados e iconografía**

- **FR-011**: Cada componente MUST documentar: anatomía, variantes (`primary/secondary/ghost/destructive` para Button, etc.), tamaños (`sm/md/lg`), comportamiento responsive y a11y (rol, aria, foco), y tokens consumidos. Cada pantalla interactiva MUST definir explícitamente sus estados por Constitution Addendum 2 §9: globales `Loading`, `Ready`, `Empty`, `Error`, `Disabled`, `Active`, `Selected`, `Success`, `Failure`, `Processing`, `Completed` y, para pantallas de juego, `QuestionActive`, `AnswerSelected`, `AnswerLocked`, `Evaluating`, `Correct`, `Incorrect`, `Timeout`, `RoundCompleted`, `WithdrawConfirmation`, `Withdrawn`, `Winner`, `Eliminated`, `Consolation`.
- **FR-012**: El sistema MUST definir iconografía con grid 24px, trazo 1.5–2px, tamaños tokenizados (16/20/24/32), y criterios de uso (no solo decorativo; con `aria-hidden` o `aria-label` según caso); los íconos MUST ser coherentes en estilo entre ambas expresiones. Queda prohibido usar emoji como ícono primario (Addendum 2 §13).
- **FR-013**: El sistema MUST definir estados globales con tratamiento tokenizado: `loading` (skeleton/shimmer), `empty` (ilustración sutil + CTA), `error` (feedback con recovery), `disabled` (no solo color, también `cursor` y `aria-disabled`), `focus` (anillo visible), y `offline` cuando aplique.

**Accesibilidad (WCAG 2.2 AA) — Addendum 2 §8**

- **FR-014**: El sistema MUST cumplir WCAG 2.2 AA en ambas expresiones: contraste texto ≥ 4.5:1 (≥ 3:1 para grande), foco visible ≥ 3:1, navegación completa por teclado sin trampa, landmarks/heading order, labels asociados, `aria-*` correcto, soporte `forced-colors`/`high-contrast` con bordes visibles, targets touch-friendly ≥ 44px, y feedback no solo por color. El checklist pre-delivery de UI/UX Pro Max SHOULD incorporarse al proceso de revisión.
- **FR-015**: El sistema MUST garantizar que ninguna información se comunique solo por color (timer `warning/critical` combina color + ícono + texto/aria), y que el orden de foco sea lógico en todos los breakpoints normativos (375/768/1024/1440).

**Responsive design — Addendum 2 §7**

- **FR-016**: El sistema MUST ser mobile-first y responsive sin scroll horizontal en 320px; la experiencia Player MUST ser plenamente usable en `375px` (móvil), `768px`, `1024px` y `1440px` (Addendum 2 §7) y la Administration MUST ser usable ≥ 1024px con adaptación progresiva a 768px sin perder densidad crítica; los layouts MUST adaptar, no solo escalar.
- **FR-017**: Cada componente y layout MUST definir comportamiento por breakpoint normativo (ej. tabla → cards en `375px`, sidebar → drawer en `768px`, QuestionCard apilada en `375px` y 2-column en `1024px`) documentado en spec del componente; la pantalla de juego MUST preservar siempre legibilidad de pregunta, accesibilidad de respuestas, visibilidad de timer/score/acción primaria.

**Motion — Addendum 2 §6**

- **FR-018**: El sistema MUST usar motion para comunicar cambios de estado, no como decoración gratuita; animaciones MUST respetar `prefers-reduced-motion`, nunca bloquear la capacidad de responder dentro del `TimeLimit` configurado, duraciones ≤ 500 ms para micro-interacciones y ≤ 800 ms para transiciones de ronda; toda animación MUST tener alternativa `reduced-motion` (fade ≤200 ms u omisión) sin pérdida semántica.

**Proceso y validación con UI/UX Pro Max Skill — Addendum 2 §2-§3**

- **FR-019**: El proceso de diseño MUST seguir `Product Requirements → UX Analysis → UI/UX Pro Max Design System → Visual Direction → IA → Interaction → Component → Screen → Implementation → UX Review` (Addendum 2 §3) y utilizar UI/UX Pro Max Skill como ayuda de inteligencia de diseño para style, paleta, tipografía, patrones UX, responsive, a11y, interacción, animación y componentes; lo generado MUST persistirse como fuente visual de verdad.
- **FR-020**: El sistema MUST incluir guía de uso y gobierno: cuándo crear un nuevo token/componente (vía ADR), cómo versionar tokens, cómo auditar literales fuera de catálogo, y checklist de handoff a `QuizArena.Admin` (Blazor Web App .NET 11 Interactive Server) y `QuizArena.Player` (Angular 22) para evitar drift.

**Fuente de verdad, calidad y arquitectura (Addendum 2 §10–§14)**

- **FR-021**: El sistema MUST persistirse como `design-system/MASTER.md` con estructura `design-system/{MASTER.md, components/, screens/, tokens/, overrides/}` y overrides por página (`design-system/pages/` o `design-system/overrides/`) según Addendum 2 §14; toda decisión visual mayor MUST actualizarse vía SDD.
- **FR-022**: El sistema MUST establecer dos expresiones relacionadas sobre un MASTER compartido (no dos sistemas independientes): `ADMIN OVERRIDES` (Blazor — professional, dense, data-oriented, productivity, tables/forms/filters/dashboards/CRUD) y `PLAYER OVERRIDES` (Angular 22 — cinematic, game-show, immersive, high emotional feedback, large typography, countdown/progression/score/reward/celebration), compartiendo tokens, tipografía, semantics, spacing, radius, iconografía, a11y y principios de motion.
- **FR-023**: El sistema MUST respetar la arquitectura de aplicaciones Addendum 2 §1/§18: `QuizArena.Admin` (Blazor .NET 11) y `QuizArena.Player` (Angular 22) consumen `QuizArena.Api` (Backend Modular Monolith con `BuildingBlocks.Kernel.Domain` → Domain, `BuildingBlocks.CQRS` → Application/Vertical Slices, `BuildingBlocks.Kernel.Infrastructure` → EF Core/Outbox, `BuildingBlocks.EventBus.RabbitMQ` → RabbitMQ, `BuildingBlocks.ServiceDefaults` → OTel/health/resilience/IEndpoint) — nunca `Blazor → DB` ni `Angular → DB`.
- **FR-024**: La pantalla de juego activa (Addendum 2 §5) MUST tratarse como experiencia primaria y proveer: jerarquía clara de pregunta, cuatro opciones, progresión visible, nivel actual, puntos actuales, puntos asegurados, recompensa potencial, countdown, estado del jugador, leaderboard opcional, acción de retiro y feedback claro tras evaluación; los efectos visuales SHOULD realzar la emoción sin reducir usabilidad.
- **FR-025**: La UI en tiempo real MUST seguir `Backend State → Realtime Event → Client State → UI` (Addendum 2 §11); el cliente MUST NOT inferir estado autoritativo solo desde animaciones o timers locales; los eventos globales (`GameStarted`, `RoundStarted`, `RoundCompleted`, `GameFinished`) vs. player-specific (`PlayerQuestionPresented`, `PlayerScoreUpdated`, etc.) se distinguen según SPEC-012 revisado.
- **FR-026**: Cada jugador MUST tener sesión y pantalla privada aislada (`Player A/B/C` → `Session A/B/C` → `Angular Screen A/B/C`) sobre el mismo `GameId` (Addendum 2 §4/§6); la UI MUST distinguir información pública (leaderboard, ronda, jugadores restantes) de privada (mi respuesta, mi score, mis secured points, mi timer, mi retiro, mi recompensa).
- **FR-027**: El sistema MUST superar el Visual Quality Gate (Addendum 2 §12) para considerarse completo: correctitud funcional, consistencia visual, responsive, a11y, feedback de interacción, motion, loading/error/empty y `reduced-motion` verificados; no basta con compilar.
- **FR-028**: Quedan prohibidos salvo justificación por ADR los anti-patterns del Addendum 2 §13: UI genérica tipo Bootstrap, apariencia por defecto de librería, forms sin estilo, gradientes aleatorios, glassmorphism/neón excesivo, emoji como ícono primario, animaciones innecesarias, spacing/tipografía inconsistente, estados de carga ocultos, estados de error faltantes, y layouts móviles que son desktop comprimido.

### Key Entities

- **DesignToken**: Unidad atómica de decisión visual. Atributos: `name` (ej. `color.primary.500`), `value` (hex/rgba/rem/ms), `type` (`color/typography/spacing/radius/elevation/motion/breakpoint`), `theme` (`base` vs `administration` vs `player` override), `description` y `usage` (cuándo usar / no usar). Primitive vs semantic. Versionado en `design-system/tokens/` y serializado como `design-tokens.json`/`MASTER.md`.
- **Theme / Expresión visual**: Conjunto de overrides semánticos sobre tokens base. Atributos: `name` (`Administration` | `Player`), `surface` (light vs dark cinematic), `accent`, `elevation`, `radius`, `motionIntensity`. Misma API de tokens, distinta aplicación; Administración = `Command Center` (operational UX), Player = `Game Show` (emotional UX) según Addendum 2 §10.
- **Componente conceptual**: Patrón reutilizable agnóstico a framework. Atributos: `name` (`Button`, `QuestionCard`, `Leaderboard`), `anatomy` (slots), `variants`, `sizes`, `states` (Addendum 2 §9), `props conceptuales`, `tokens consumidos`, `a11y` (rol/aria/foco), `responsive` (comportamiento por breakpoint 375/768/1024/1440), `motion` (preset).
- **TypographyScale**: Escala tipográfica. Atributos: `family` (sans/serif/display), `level` (`display/heading/title/body/label/caption`), `size` (`clamp`), `weight` (400/500/600/700), `lineHeight`, `tracking`, `usage`.
- **ColorPalette**: Paleta con roles. Atributos: `primitive` (50–900), `semantic` (`primary/neutral/feedback/surface`), `contrastPairs` (combinaciones AA validadas), `stateVariants`.
- **Breakpoint & Layout**: Rango viewport. Atributos: `name` (`375`/`768`/`1024`/`1440` + extensiones), `minWidth`, `layout` (grid 4/8/12 cols), `behavior` por componente; adapta, no solo escala.
- **MotionToken**: Token de animación. Atributos: `duration` (ms), `easing` (`ease-out`/`spring`), `preset` (`fade/slide/scale/timer-pulse`), `reducedMotionFallback`; nunca bloquea ventana de respuesta.
- **IconographySet**: Set de íconos. Atributos: `grid` (24px), `stroke`, `sizes` (16/20/24/32), `style` (outline/filled), `a11y` (`aria-hidden` vs `aria-label`); sin emoji primario.
- **DesignSystem Source of Truth**: Estructura `design-system/MASTER.md` + `components/` + `screens/` + `tokens/` + `overrides/` (+ `pages/` para overrides por página) según Addendum 2 §14; versionada vía SDD y validada por UI/UX Pro Max.
- **Visual Quality Gate**: Checklist de aceptación UI (Addendum 2 §12): correctitud funcional, consistencia visual, responsive (375/768/1024/1440), a11y, feedback interacción, motion, loading/error/empty, reduced-motion.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un operador completa los flujos administrativos críticos (crear juego, publicar categoría, crear pregunta, consultar audit/reportes) en desktop sin ayuda en ≥ 90% de intentos y con SUS ≥ 75 (n=8).
- **SC-002**: Un jugador completa una partida de 5 rondas en móvil (360px) y desktop (1440px) sin scroll horizontal, con timer y progresión siempre visibles, en ≥ 95% de intentos; satisfacción estética ≥ 4.2/5 en test de 8 jugadores y 0 reportes de copia de branding.
- **SC-003**: El 100% de los colores, espaciados, radios, elevaciones y motion usados en 10 pantallas auditadas provienen de tokens documentados; 0 literales hardcodeados fuera de catálogo (verificable por inspección de `design-tokens.json` y snapshots de pantalla).
- **SC-004**: Auditoría automática de contraste (axe) pasa AA en 100% de las combinaciones texto/fondo documentadas en ambas expresiones (≥ 4.5:1 normal, ≥ 3:1 grande) y foco visible ≥ 3:1.
- **SC-005**: Navegación completa por teclado de los flujos críticos (admin: crear juego; player: 5 rondas) se completa sin trampa de foco y con orden lógico en 100% de los casos, verificable con tab + screen reader (NVDA/VoiceOver).
- **SC-006**: Con `prefers-reduced-motion: reduce`, el 100% de las animaciones degradan a `fade` ≤200 ms u omisión sin pérdida de información (timer comunica estado por color+ícono+texto, no solo animación).
- **SC-007**: Responsive: 0 scroll horizontal en 320px–1536px en los 8 flujos clave; cada componente documenta comportamiento por breakpoint normativo (375/768/1024/1440) y pasa inspección visual en esos 4 + extensiones (360/1440 ya cubiertos); la pantalla de juego preserva pregunta/opciones/timer/score/acción primaria en todos.
- **SC-007b**: Visual Quality Gate (Addendum 2 §12) superado: cada feature UI pasa checklist funcional + visual + responsive + a11y + interacción + motion + loading/error/empty + reduced-motion antes de marcarse completa.
- **SC-008**: Reporte de validación UI/UX Pro Max Skill archivado con hallazgos y decisiones; `design-system/MASTER.md` generado vía skill siguiendo flujo Product→UX→System→Visual→IA→Interaction→Component→Screen→Implementation→Review (Addendum 2 §3); ≥ 90% de las recomendaciones críticas aplicadas o justificadas por ADR antes de handoff a Blazor/Angular 22.
- **SC-009**: Tiempo de handoff: un desarrollador nuevo implementa `Button` + `QuestionCard` en ambas expresiones usando solo tokens y spec conceptual sin preguntar valores, en < 30 minutos por componente (medido en onboarding).
- **SC-010**: 0 anti-patterns del Addendum 2 §13 presentes en auditoría visual de 10 pantallas (no Bootstrap genérico, no glass/neón excesivo, no emoji primario, no animación innecesaria, no spacing/tipografía inconsistente, no estados faltantes, no mobile = desktop comprimido).
- **SC-011**: Arquitectura verificada: `QuizArena.Admin` (Blazor .NET 11 Interactive Server) y `QuizArena.Player` (Angular 22) consumen `QuizArena.Api` con BuildingBlocks correctos (Domain←Kernel.Domain, Application←CQRS, Infra←Kernel.Infrastructure/EventBus.RabbitMQ, Host←ServiceDefaults); 0 accesos directos a DB desde UI.

## Assumptions

- La identidad de marca base (nombre QuizArena/OroQuizClash, logo) existe a nivel conceptual; este SPEC define su aplicación en tokens/componentes, no crea marca desde cero si ya hay logo; si no, se propone logotipo tipográfico neutro como placeholder validado con UI/UX Pro Max (Addendum 2 §1 — UI es concern arquitectónico de primera clase).
- `QuizArena.Admin` es Blazor Web App .NET 11 Interactive Server y `QuizArena.Player` es Angular 22.x (publicado 2026-06-03, Addendum 2 nota final) — decisiones cerradas; el Design System es agnóstico y no impone librería específica (ej. FluentUI vs Angular Material) — los tokens son contrato, la implementación elige adaptador; ambas apps consumen `QuizArena.Api` con arquitectura BuildingBlocks (§3–5/§15/§18 del addendum).
- Se asume `light` por defecto para Administration y `dark cinematic` por defecto para Player; `dark` en admin y `light` en player son opcionales y no bloquean MVP si se documentan como extensión en `design-system/overrides/`.
- La escala de espaciado 4/8, breakpoints normativos 375/768/1024/1440 (Addendum 2 §7) y motion tokens propuestos son defaults validados con UI/UX Pro Max; pueden ajustarse vía ADR sin romper contrato de token; el flujo de generación usa el prompt canónico del Addendum 2 §12 (QuizArena premium multiplayer trivia, cinematic vs SaaS, evitar Bootstrap/glass/neón/gradientes AI, generar style/palette/typography/spacing/radius/elevation/motion/iconography/buttons/cards/forms/dialogs/tables/navigation/notifications/game controls/timers/progress/score/reward/leaderboard/responsive/a11y/game-states/admin-states).
- Accesibilidad objetivo es WCAG 2.2 AA (no AAA); `high-contrast`/`forced-colors` se soporta a nivel de fallback de borde/foco, no como tema separado completo en MVP; el pre-delivery checklist de UI/UX Pro Max se incorpora a la revisión.
- El catálogo inicial de componentes conceptuales cubre al menos: Button, Input, Select, Table, Card, Modal, Drawer, Badge, Tabs, Progress, Timer, QuestionCard, AnswerOption, Leaderboard, Toast — extensible sin romper spec; cada uno define estados del Addendum 2 §9.
- El artefacto de handoff es `design-system/MASTER.md` + `design-system/tokens/` (ej. `design-tokens.json`) + `components/`/`screens/`/`overrides/` y overrides por página `design-system/pages/` (Addendum 2 §14); la ruta exacta se define en plan sin afectar criterios de éxito; el SPEC es Diseño-Primero: no se implementan superficies mayores antes de establecer el Design System (§3).
- UI/UX Pro Max Skill está disponible como herramienta de validación; su reporte es artifact humano + checklist, no gate automático bloqueante si se justifica divergencia por ADR; la skill decide paleta/tipografía definitiva mediante su generador, no se impone artificialmente antes de ejecutarla (Addendum 2 §11).
- Cada jugador tiene sesión y pantalla privada aislada (Addendum 2 §4/§6); SPEC-004/011/012 serán actualizados para reflejar `Game` vs `PlayerGameSession` y eventos globales vs player-specific antes de implementar SPEC-017+.

## Dependencies

- Constitución v1.1.0 (`.specify/memory/constitution.md`) — Principios I–VI, Additional Constraints H/I/J (identidad delegada a OroIdentityServer, observabilidad con ServiceDefaults, API/Frontend presentation-only).
- Constitution Addendum — `draft/constitution-addendum.md` v1.0.0 (BuildingBlocks platform, no reinvención, Vertical Slice + CQRS, `net10.0`/`net11.0`, dependency inversion) — normativo para backend que ambas apps consumen.
- UI/UX Constitution Addendum — `draft/constitution-addendum2.md` (UI/UX como concern arquitectónico de primera clase, UI/UX Pro Max §2, Design System First §3, Player/Cinematic §4–§5, Motion §6, Responsive 375/768/1024/1440 §7, A11y §8, UI States §9, Separation of Experiences §10, Realtime UI §11, Visual Quality Gate §12, Anti-Patterns §13, Source of Truth `design-system/MASTER.md` §14, Definition of Done §15) — **normativo para este SPEC**; este SPEC es el primer paso tras actualizar Constitution + SPEC-004/011/012 según Addendum 2 §19.
- SPEC-001 Game Configuration, SPEC-002 Categories, SPEC-003 Question Bank — flujos administrativos que el Design System debe vestir (módulos Admin: Dashboard, Games, Game Configuration, Categories, Question Bank, Rewards, Players, Live Games, Reports, Audit).
- SPEC-004 Game Lifecycle (a actualizar: `Game` vs `PlayerGameSession`), SPEC-005 Round Engine, SPEC-006 Answer Evaluation, SPEC-007 Scoring, SPEC-011 Multiplayer (a actualizar: `GameSession` por jugador), SPEC-012 Realtime (a actualizar: GLOBAL vs PLAYER-SPECIFIC events), SPEC-013 Game Security (RBAC, `Audit.Read`/`Report.Read`) — determinan visibilidad y reglas que la UI no debe duplicar.
- SPEC-014 Audit Trail, SPEC-015 Operational Reporting — pantallas densas de admin que validan la estética enterprise SaaS.
- OroIdentityServer (Podman `oroidentityserver:latest`, `draft/oroidentityserver-specification.md`) — login/change-password/logout UI externa (`/Account/*`, `/auth/*`); el Design System no rediseña esa UI, solo asegura coherencia de redirección y manejo de claim `must_change_password`.
- UI/UX Pro Max Skill — `ui-ux-pro-max` — herramienta normativa para generar/validar el sistema según esta spec; genera `design-system/MASTER.md` y overrides por página (Addendum 2 §9/§12).
- Estructura de SPEC futura (Addendum 2 §8–§12): SPEC-017 admin-application, SPEC-018 admin-dashboard … SPEC-027 player-application … SPEC-036 player-rewards — este SPEC-016 es prerequisito de todas.

## Out of Scope

- Implementación concreta en `QuizArena.Admin` (Blazor .NET 11) / `QuizArena.Player` (Angular 22) — código, librerías, Storybook — este SPEC define tokens/componentes conceptuales y validación; la implementación es fase posterior (SPEC-017+ y plan/tasks); no se inicia UI mayor antes del Design System (Addendum 2 §3).
- Branding externo completo (manual de marca impreso, merchandising) más allá de la aplicación web.
- Copia o réplica de identidad visual, branding, gráficos, sonidos, layouts o assets de programas de concurso existentes — explícitamente prohibido (Addendum 2 §4); solo inspiración genérica de principios de tensión/progresión.
- Tema `high-contrast` completo separado o soporte `AAA` en MVP — solo fallback AA + `forced-colors` (Addendum 2 §8).
- Animaciones complejas con WebGL/Canvas o audio — motion se limita a CSS/transform/opacity con propósito y nunca bloquea ventana de respuesta (Addendum 2 §6).
- Duplicación de BuildingBlocks o acceso directo UI→DB — prohibido por Constitution + Addendum; ambas apps consumen `QuizArena.Api` con BuildingBlocks oficiales.

## References

- Constitución v1.1.0 — `.specify/memory/constitution.md` + Sync Impact Report (OroIdentityServer).
- Constitution Addendum — `draft/constitution-addendum.md` v1.0.0 (§1–§24, BuildingBlocks, Vertical Slice, multi-targeting `net10.0`/`net11.0`).
- UI/UX Constitution Addendum — `draft/constitution-addendum2.md` (§1 UI First-Class, §2 UI/UX Pro Max, §3 Design System First, §4 Player Experience, §5 Cinematic, §6 Motion, §7 Responsive 375/768/1024/1440, §8 A11y, §9 UI States, §10 Separation, §11 Realtime UI, §12 Quality Gate, §13 Anti-Patterns, §14 Source of Truth `design-system/MASTER.md`, §15 Done) — **normativo**.
- `draft/constitution.md` §30 Frontend como presentation-only + §5 State Machine, §9 Multiplayer — complementado por Addendum 2 para sesión privada por jugador.
- `draft/game-concept.md` — concepto de juego y progresión.
- `draft/oroidentityserver-specification.md` — identidad externa Podman `oroidentityserver:latest`, `/Account/*` y `/auth/*` UI, `must_change_password` gating.
- `draft/libraries/buildingblocks.md` — BuildingBlocks platform capabilities.
- UI/UX Pro Max Skill — `ui-ux-pro-max` (79 styles, 192 palettes, 74 font pairings, 17 GSAP presets, Angular stack) — generador de Design System y validador; prompt canónico Addendum 2 §12.
