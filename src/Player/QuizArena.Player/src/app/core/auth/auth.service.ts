import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private oidc = inject(OidcSecurityService);

  isAuthenticated$ = this.oidc.isAuthenticated$;
  userData$ = this.oidc.userData$;

  login() { this.oidc.authorize(); }
  logout() { this.oidc.logoff(); }
  getAccessToken(): import('rxjs').Observable<string> { return this.oidc.getAccessToken(); }
  getPayload(): import('rxjs').Observable<any> { return this.oidc.getPayloadFromIdToken(); }
}
