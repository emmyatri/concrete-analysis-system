import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { TechnicianSession } from './technician-session';

/** Must match TechnicianHeader.Name in Vulcan.Api. */
export const TECHNICIAN_HEADER = 'X-Technician';

/**
 * Sends the technician's name with every API call, so saved records say who made them (M1-22 stamps it).
 * URL-encoded because headers only allow plain ASCII and names may not be ("José"). The API decodes it.
 */
export const technicianInterceptor: HttpInterceptorFn = (request, next) => {
  const name = inject(TechnicianSession).name();
  if (!name || !request.url.startsWith('/api/')) return next(request);

  return next(request.clone({ setHeaders: { [TECHNICIAN_HEADER]: encodeURIComponent(name) } }));
};
