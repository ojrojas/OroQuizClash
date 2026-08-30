import { Injectable } from '@angular/core';

function safeUUID(): string {
  try {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  } catch {}
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Injectable({ providedIn: 'root' })
export class IdempotencyService {
  private prefix = 'idemp-';

  getOrCreate(keySuffix: string): string {
    const key = this.prefix + keySuffix;
    try {
      const existing = sessionStorage.getItem(key);
      if (existing) return existing;
      const created = safeUUID();
      sessionStorage.setItem(key, created);
      return created;
    } catch {
      return safeUUID();
    }
  }

  get(keySuffix: string): string | null {
    try { return sessionStorage.getItem(this.prefix + keySuffix); } catch { return null; }
  }

  clear(keySuffix: string) {
    try { sessionStorage.removeItem(this.prefix + keySuffix); } catch {}
  }

  getOrCreateRound(roundId: string): string {
    return this.getOrCreate(roundId);
  }

  clearRound(roundId: string) {
    this.clear(roundId);
  }
}
