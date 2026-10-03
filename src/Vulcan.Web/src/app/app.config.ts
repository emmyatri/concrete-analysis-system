import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { apiErrorLoggingInterceptor } from './core/api/api-error-logging.interceptor';
import { ReferenceDataService } from './core/api/reference-data.service';
import { USE_MOCKS } from './core/api/use-mocks';
import { VulcanPreset } from './theme/vulcan-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: route data (e.g. a page heading) arrives as component inputs.
    provideRouter(routes, withComponentInputBinding()),
    // Fake data in development builds (environment.useMocks), and logging of connection problems (M1-60).
    provideHttpClient(withInterceptors([apiErrorLoggingInterceptor, ...environment.devInterceptors])),
    { provide: USE_MOCKS, useValue: environment.useMocks },
    // Validation rules and specific gravities load once at startup, without holding up the first screen.
    provideAppInitializer(() => inject(ReferenceDataService).load()),
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
