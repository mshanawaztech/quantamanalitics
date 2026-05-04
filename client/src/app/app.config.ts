import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { authHttpInterceptorFn, provideAuth0 } from '@auth0/auth0-angular';

import { routes } from './app.routes';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // withFetch enables server-rendered apps (when SSR is added later) to share
    // the fetch implementation between client and server. Cheap to opt-in now.
    // Auth0's standalone quickstart wires the HTTP interceptor through
    // withInterceptors([authHttpInterceptorFn]); this is the path the SDK
    // explicitly documents for bootstrapApplication-style Angular apps.
    provideHttpClient(withFetch(), withInterceptors([authHttpInterceptorFn])),

    // Auth0 SDK — wires up login/logout, the user/token observables, and the
    // HTTP interceptor that attaches a Bearer JWT to every request matching
    // `httpInterceptor.allowedList`. When Auth0 isn't configured (empty
    // domain), the SDK still installs but loginWithRedirect throws on call —
    // we guard the UI so the buttons render in a "not configured" state.
    provideAuth0({
      domain: environment.auth0.domain,
      clientId: environment.auth0.clientId,
      authorizationParams: {
        // After login Auth0 redirects back to whichever page kicked off the flow.
        redirect_uri: typeof window !== 'undefined' ? window.location.origin : '',
        // The `audience` is what tells Auth0 to mint an access token for our
        // API (vs. just an ID token for the SPA itself). Without this, /me
        // would reject the token because aud != our API identifier.
        audience: environment.auth0.audience,
      },
      // Restore the user back to the page they were on before login.
      useRefreshTokens: true,
      cacheLocation: 'localstorage',
      httpInterceptor: {
        // Attach Bearer token only to API calls — never to public assets,
        // SWA static files, or third-party endpoints. Use the explicit object
        // form so the interceptor requests a token with our API audience for
        // every protected API call, including /me on the deployed origin.
        allowedList: [
          {
            uri: `${environment.apiBase}/*`,
            tokenOptions: {
              authorizationParams: {
                audience: environment.auth0.audience,
              },
            },
          },
        ],
      },
    }),
  ],
};
