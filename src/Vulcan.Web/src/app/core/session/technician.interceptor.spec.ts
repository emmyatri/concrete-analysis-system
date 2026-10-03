import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TECHNICIAN_SETTINGS_URL, TechnicianSession } from './technician-session';
import { TECHNICIAN_HEADER, technicianInterceptor } from './technician.interceptor';

describe('technicianInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([technicianInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });

  const startSession = (name: string) => {
    TestBed.inject(TechnicianSession).start(name).subscribe();
    http.expectOne(TECHNICIAN_SETTINGS_URL).flush(null);
  };

  it('sends no technician before the session starts', () => {
    client.get('/api/health').subscribe();

    expect(http.expectOne('/api/health').request.headers.has(TECHNICIAN_HEADER)).toBe(false);
  });

  it('sends the technician with every API call, URL-encoded', () => {
    startSession('José Rivera');

    client.post('/api/batches', {}).subscribe();

    expect(http.expectOne('/api/batches').request.headers.get(TECHNICIAN_HEADER)).toBe('Jos%C3%A9%20Rivera');
  });

  it('switching technician changes the name on the next call', () => {
    startSession('J. Rivera');
    startSession('M. Chen');

    client.post('/api/batches', {}).subscribe();

    expect(decodeURIComponent(http.expectOne('/api/batches').request.headers.get(TECHNICIAN_HEADER)!)).toBe('M. Chen');
  });
});
