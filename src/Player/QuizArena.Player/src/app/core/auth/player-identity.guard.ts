import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { PlayerGameStore } from '../../stores/player-game.store';

export function assertPlayerIdentity(store: PlayerGameStore): boolean {
  const oidc = inject(OidcSecurityService);
  const payload = oidc.getPayloadFromIdToken() as any;
  const sub = payload?.sub;
  const playerId = store.player()?.playerId;
  if (sub && playerId && sub !== playerId) {
    console.warn('[PlayerIdentityGuard] impersonation attempt', { sub, playerId });
    return false;
  }
  return true;
}
