import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  selector: 'app-callback',
  standalone: true,
  template: `<p>Autenticando...</p>`,
})
export class CallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  ngOnInit() {
    this.oidc.checkAuth().subscribe(({ isAuthenticated }) => {
      this.router.navigateByUrl(isAuthenticated ? '/lobby' : '/');
    });
  }
}
