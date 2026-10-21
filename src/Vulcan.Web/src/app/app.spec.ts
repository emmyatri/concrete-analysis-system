import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { App } from './app';
import { routes } from './app.routes';
import { TECHNICIAN_SETTINGS_URL, TechnicianSession } from './core/session/technician-session';
import { VulcanPreset } from './theme/vulcan-preset';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
      ],
    });
  });

  const startSession = () => {
    TestBed.inject(TechnicianSession).start('J. Rivera').subscribe();
    TestBed.inject(HttpTestingController).expectOne(TECHNICIAN_SETTINGS_URL).flush(null);
  };

  it('opens Start session before any other screen', async () => {
    const harness = await RouterTestingHarness.create('/batches');

    expect(TestBed.inject(Router).url).toBe('/start?returnUrl=%2Fbatches');
    expect((harness.routeNativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Start session',
    );
  });

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the shell on the home route', async () => {
    startSession();
    const harness = await RouterTestingHarness.create('/');
    const root = harness.routeNativeElement as HTMLElement;

    expect(root.querySelector('.shell-logo')?.getAttribute('src')).toBe('assets/vulcan-logo.svg');
  });

  it('sends unknown routes home', async () => {
    startSession();
    const harness = await RouterTestingHarness.create('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness).toBeTruthy();
  });
});
