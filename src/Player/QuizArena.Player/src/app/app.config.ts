import { ApplicationConfig, isDevMode } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAuth } from 'angular-auth-oidc-client';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { correlationIdInterceptor } from './core/interceptors/correlation-id.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([correlationIdInterceptor, authInterceptor, errorInterceptor])),
    provideAuth({
      config: {
        authority: environment.identityAuthority,
        redirectUrl: window.location.origin + '/auth/callback',
        postLogoutRedirectUri: window.location.origin + '/auth/logout-callback',
        clientId: 'quizarena-player',
        scope: 'openid profile email offline_access',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        renewTimeBeforeTokenExpiresInSeconds: 30,
        secureRoutes: [environment.apiUrl],
        maxIdTokenIatOffsetAllowedInSeconds: 600,
        triggerAuthorizationResultEvent: true,
        // @ts-ignore - opciones no tipadas en v17 pero soportadas por la lib
        requireHttps: !isDevMode(),
        strictDiscoveryDocumentValidation: !isDevMode(),
        // @ts-ignore
        historyCleanupOff: false,
      }
    }),
  ],
};
