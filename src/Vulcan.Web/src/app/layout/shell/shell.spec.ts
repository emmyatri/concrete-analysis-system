import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { routes } from '../../app.routes';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { SHELL_MENU } from './menu-items';

describe('Shell', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
      ],
    });

    harness = await RouterTestingHarness.create('/');
    http = TestBed.inject(HttpTestingController);
  });

  const page = () => harness.routeNativeElement as HTMLElement;
  const menuLink = (label: string) =>
    Array.from(page().querySelectorAll<HTMLAnchorElement>('.shell-menu-link')).find(
      (link) => link.textContent?.trim() === label,
    )!;

  it('shows every menu item from the mockup, in order', () => {
    const labels = Array.from(page().querySelectorAll('.shell-menu-link')).map((link) => link.textContent?.trim());

    expect(labels).toEqual(['Home', 'New batch', 'Search batches', 'Break results', 'Curing readings']);
  });

  for (const item of SHELL_MENU) {
    it(`"${item.label}" navigates to its page and is highlighted`, async () => {
      menuLink(item.label).click();
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(TestBed.inject(Router).url).toBe(item.route);
      expect(page().querySelector('.page-heading')?.textContent).toBe(item.label);

      const active = page().querySelectorAll('.shell-menu-link.active');
      expect(active.length).toBe(1);
      expect(active[0].textContent?.trim()).toBe(item.label);
      expect(active[0].getAttribute('aria-current')).toBe('page');
    });
  }

  it('shows the app version in the menu footer', () => {
    expect(page().querySelector('.shell-version')?.textContent).toContain('v0.1.0');
  });

  it('shows "Local API ready" once /api/health answers', () => {
    http.expectOne('/api/health').flush({ status: 'ok' });
    harness.detectChanges();

    expect(page().querySelector('[data-testid="api-status"]')?.textContent).toContain('Local API ready');
  });

  it('shows "Starting local service…" while waiting for the API', () => {
    expect(page().querySelector('[data-testid="api-status"]')?.textContent).toContain('Starting local service');
  });
});
