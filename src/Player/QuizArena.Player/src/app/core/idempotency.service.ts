import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class IdempotencyService {
  private prefix = 'idemp-';

  getOrCreate(roundId: string): string {
    const key = this.prefix + roundId;
    let existing = sessionStorage.getItem(key);
    if (!existing) {
      existing = crypto.randomUUID();
      sessionStorage.setItem(key, existing);
    }
    return existing;
  }

  clear(roundId: string) {
    sessionStorage.removeItem(this.prefix + roundId);
  }
}
