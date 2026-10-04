import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Production build (the desktop app). Always talks to the real local API.
 * Development builds use environment.development.ts instead (angular.json fileReplacements).
 */
export const environment = {
  /** True: screens get fake data (core/api/mock). False: they talk to the real server. */
  useMocks: false,
  /** Extra HTTP interceptors for this build. Empty here, so no fake data ships in the desktop app. */
  devInterceptors: [] as HttpInterceptorFn[],
};
