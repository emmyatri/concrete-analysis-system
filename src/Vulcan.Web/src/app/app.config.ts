import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { routes } from './app.routes';
import { VulcanPreset } from './theme/vulcan-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: route data (e.g. a page heading) arrives as component inputs.
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(),
    providePrimeNG({
      theme: {
        preset: VulcanPreset,
        options: {
          // Light mode only. Without this, the app turns dark on Windows dark mode.
          darkModeSelector: false,
        },
      },
    }),
  ],
};
