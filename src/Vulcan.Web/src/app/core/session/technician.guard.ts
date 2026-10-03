import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { TechnicianSession } from './technician-session';

/** Every screen needs a technician: until there is one, open Start session first (M1-42). */
export const technicianGuard: CanActivateFn = (_route, state) => {
  if (inject(TechnicianSession).isStarted()) return true;

  const returnUrl = state.url === '/' ? undefined : state.url;
  return inject(Router).createUrlTree(['/start'], { queryParams: { returnUrl } });
};
