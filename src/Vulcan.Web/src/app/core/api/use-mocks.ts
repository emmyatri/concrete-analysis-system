import { InjectionToken } from '@angular/core';

/** True when screens should get fake data (environment.useMocks). Provided in app.config.ts. */
export const USE_MOCKS = new InjectionToken<boolean>('USE_MOCKS', {
  providedIn: 'root',
  factory: () => false,
});
