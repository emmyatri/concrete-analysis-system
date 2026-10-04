import { HttpInterceptorFn } from '@angular/common/http';

import { mockApiInterceptor } from '../app/core/api/mock/mock-api.interceptor';

/**
 * Development build (`npm start`, live reload, `ng test`).
 * Set useMocks to false to use the real local API instead of fake data. No screen code changes.
 */
export const environment = {
  /** True: screens get fake data (core/api/mock). False: they talk to the real server. */
  useMocks: true,
  devInterceptors: [mockApiInterceptor] as HttpInterceptorFn[],
};
