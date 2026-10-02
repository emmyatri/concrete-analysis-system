import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { routes } from './app.routes';
import { VulcanPreset } from './theme/vulcan-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
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
