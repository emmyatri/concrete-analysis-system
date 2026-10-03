import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { ALLOW_START_WITHOUT_API, TECHNICIAN_SETTINGS_URL, TechnicianSession } from '../../core/session/technician-session';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { StartSession } from './start-session';

describe('StartSession', () => {
  let fixture: ComponentFixture<StartSession>;
  let http: HttpTestingController;

  // The desktop app's behaviour: no starting a session without the local API.
  const setUp = (allowWithoutApi = false) => {
    TestBed.configureTestingModule({
      imports: [StartSession],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: ALLOW_START_WITHOUT_API, useValue: allowWithoutApi },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(StartSession);
    fixture.detectChanges();
  };

  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector<HTMLInputElement>('#technicianName')!;
  const message = () => el().querySelector('#technicianName-message')?.textContent?.trim();
  const startButton = () => el().querySelector<HTMLButtonElement>('[data-testid="start-session"] button')!;

  const apiReady = () => {
    http.expectOne('/api/health').flush({ status: 'ok' });
    fixture.detectChanges();
  };
  const lastUsed = (name: string | null) => {
    http.expectOne({ method: 'GET', url: TECHNICIAN_SETTINGS_URL }).flush({ name });
    fixture.detectChanges();
  };
  const type = (value: string) => {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  it('fills in the last name used on this computer', () => {
    setUp();
    lastUsed('J. Rivera');

    expect(input().value).toBe('J. Rivera');
  });

  it('keeps Start session disabled while the local API is starting', () => {
    setUp();
    lastUsed('J. Rivera');

    expect(el().querySelector('[data-testid="api-status"]')?.textContent).toContain('Starting local service');
    expect(startButton().disabled).toBe(true);
  });

  it('blocks a name of 1 or 51 characters with a red message, and accepts 2 to 50', () => {
    setUp();
    lastUsed(null);
    apiReady();

    type('J');
    expect(message()).toBe('At least 2 characters');
    expect(startButton().disabled).toBe(true);

    type('A'.repeat(51));
    expect(message()).toBe('50 characters max');
    expect(startButton().disabled).toBe(true);

    type('Jo');
    expect(message()).toBe('Required');
    expect(startButton().disabled).toBe(false);

    type('A'.repeat(50));
    expect(startButton().disabled).toBe(false);
  });

  it('saves the name trimmed, starts the session and opens the app', () => {
    setUp();
    lastUsed(null);
    apiReady();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    type(' Jordan ');
    startButton().click();

    const save = http.expectOne({ method: 'PUT', url: TECHNICIAN_SETTINGS_URL });
    expect(save.request.body).toEqual({ name: 'Jordan' });
    save.flush(null, { status: 204, statusText: 'No Content' });

    expect(TestBed.inject(TechnicianSession).name()).toBe('Jordan');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('goes back to the page that asked, but never off the app', () => {
    setUp();
    lastUsed(null);
    apiReady();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    fixture.componentRef.setInput('returnUrl', '/batches/new');
    type('Jordan');
    startButton().click();
    http.expectOne({ method: 'PUT', url: TECHNICIAN_SETTINGS_URL }).flush(null);
    expect(navigate).toHaveBeenLastCalledWith('/batches/new');

    fixture.componentRef.setInput('returnUrl', '//evil.example');
    startButton().click();
    http.expectOne({ method: 'PUT', url: TECHNICIAN_SETTINGS_URL }).flush(null);
    expect(navigate).toHaveBeenLastCalledWith('/');
  });

  it('shows an error and Retry when the local API never answers', () => {
    vi.useFakeTimers();
    try {
      setUp();
      lastUsed(null);
      // First try plus 5 retries, one second apart.
      for (let attempt = 0; attempt < 6; attempt++) {
        http.expectOne('/api/health').flush(null, { status: 503, statusText: 'Unavailable' });
        vi.advanceTimersByTime(1000);
      }
      fixture.detectChanges();
      type('Jordan');

      expect(el().querySelector('[data-testid="api-failed"]')).not.toBeNull();
      expect(startButton().disabled).toBe(true);

      el().querySelector<HTMLButtonElement>('[data-testid="retry"] button')!.click();
      fixture.detectChanges();

      http.expectOne('/api/health').flush({ status: 'ok' });
      fixture.detectChanges();
      expect(startButton().disabled).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('lets a browser-only dev build start without the API', () => {
    vi.useFakeTimers();
    try {
      setUp(true);
      lastUsed(null);
      for (let attempt = 0; attempt < 6; attempt++) {
        http.expectOne('/api/health').flush(null, { status: 0, statusText: 'Unknown Error' });
        vi.advanceTimersByTime(1000);
      }
      fixture.detectChanges();
      vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

      type('Jordan');
      startButton().click();
      http.expectOne({ method: 'PUT', url: TECHNICIAN_SETTINGS_URL }).flush(null, { status: 0, statusText: 'Unknown Error' });

      expect(TestBed.inject(TechnicianSession).name()).toBe('Jordan');
    } finally {
      vi.useRealTimers();
    }
  });
});
