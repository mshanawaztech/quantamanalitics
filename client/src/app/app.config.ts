import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // withFetch enables server-rendered apps (when SSR is added later) to share
    // the fetch implementation between client and server. Cheap to opt-in now.
    provideHttpClient(withFetch()),
  ],
};
