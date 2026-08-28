---
name: ngrx-signal-store
description: NgRx SignalStore — store creation, entity management, effects, testing
paths: ["**/*.ts", "**/*.store.ts"]
---

## NgRx SignalStore — Quick Reference

Concise notes on NgRx SignalStore based on the official documentation.

### Installation

```bash
npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop
npm install @ngrx/eslint-plugin --save-dev   # Optional lint rules
```

### Creating a Store

Use `signalStore(...)` combining features: `withState`, `withComputed`, `withMethods`, `withProps`. Each state slice becomes a `Signal`/`DeepSignal`, accessible as `store.prop()`.

### Lifecycle Hooks

Use lifecycle hooks to initialize resources or clean up subscriptions when the store is instantiated or destroyed.

### Custom Store Properties

`withProps` adds static properties, observables, or injected dependencies. Useful for exposing services or internal constants.

### Linked State

Link state between stores or signals using `computed` to keep reactive relationships without duplicating data.

### State Tracking

SignalStore generates deep signals (`DeepSignal`) for nested properties; tracking is granular and lazy for deep properties.

### Private Members

Declare private members inside `withProps`/`withMethods` to encapsulate internal logic. Keep the public API minimal.

### Custom Store Features

Create reusable features (combinations of `withX`) to encapsulate common behaviors and reduce repetition.

### Entity Management

Use the `entities` plugin to manage normalized collections (efficient CRUD, upserts, optimized selectors).

### Events and Effects

For complex side-effects use `rxMethod` (RxJS interop) or injected services inside `withMethods`. Handle errors with `tapResponse` and update state with `patchState`.

### Testing

Stores are injectable: provide them locally in tests and use `inject()` to obtain instances. Test signals, methods, and effects in isolation.

### Compact Example

```ts
import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';

export const BookSearchStore = signalStore(
  withState({ books: [], isLoading: false, filter: { query: '', order: 'asc' } }),
  withComputed(({ books, filter }) => ({ booksCount: computed(() => books().length) })),
  withMethods((store, booksService = inject(BooksService)) => ({
    updateQuery(query: string) { patchState(store, (s) => ({ filter: { ...s.filter, query } })); },
    loadByQuery: rxMethod<string>(/* rx pipeline */),
  }))
);

## CLI Commands

```bash
# Generate a store manually (create file, no ng generate for signal stores yet)
touch src/app/features/{name}/{name}.store.ts

# Run tests after creating store
ng test --include="**/{name}.store.spec.ts"
```
