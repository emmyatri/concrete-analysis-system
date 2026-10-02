import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { App } from './app';
import { routes } from './app.routes';
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

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the shell on the home route', async () => {
    const harness = await RouterTestingHarness.create('/');
    const root = harness.routeNativeElement as HTMLElement;

    expect(root.querySelector('.shell-logo')?.getAttribute('src')).toBe('assets/vulcan-logo.svg');
  });

  it('sends unknown routes home', async () => {
    const harness = await RouterTestingHarness.create('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness).toBeTruthy();
  });
});
