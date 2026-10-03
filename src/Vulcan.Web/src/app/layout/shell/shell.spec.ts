import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { routes } from '../../app.routes';
import { TECHNICIAN_SETTINGS_URL, TechnicianSession } from '../../core/session/technician-session';
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

    http = TestBed.inject(HttpTestingController);

    // Every screen needs a technician (M1-42).
    TestBed.inject(TechnicianSession).start('J. Rivera').subscribe();
    http.expectOne(TECHNICIAN_SETTINGS_URL).flush(null);

    harness = await RouterTestingHarness.create('/');
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
      expect(page().querySelector('h1')?.textContent?.trim()).toBe(item.label);

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

  it('shows the technician in the top bar', () => {
    const chip = page().querySelector('[data-testid="technician"]');

    expect(chip?.textContent).toContain('J. Rivera');
    expect(chip?.querySelector('.technician-avatar')?.textContent?.trim()).toBe('JR');
  });

  it('"Switch technician" reopens Start session and comes back to the same page', async () => {
    await harness.navigateByUrl('/batches');
    page().querySelector<HTMLButtonElement>('[data-testid="technician"]')!.click();
    harness.detectChanges();

    const item = Array.from(document.querySelectorAll<HTMLElement>('.p-menu-item-link')).find((link) =>
      link.textContent?.includes('Switch technician'),
    );
    item!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/start?returnUrl=%2Fbatches');
  });
  
  it('shows "Starting local service…" while waiting for the API', () => {
    expect(page().querySelector('[data-testid="api-status"]')?.textContent).toContain('Starting local service');
  });
});
