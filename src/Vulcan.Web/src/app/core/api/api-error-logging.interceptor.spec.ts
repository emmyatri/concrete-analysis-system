import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { apiErrorLoggingInterceptor } from './api-error-logging.interceptor';

describe('apiErrorLoggingInterceptor', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorLoggingInterceptor])), provideHttpClientTesting()],
    });
  });

  it('logs when the local service can\'t be reached, but not a normal 400', () => {
    const log = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const client = TestBed.inject(HttpClient);
    const http = TestBed.inject(HttpTestingController);

    client.get('/api/batches').subscribe({ error: () => undefined });
    http.expectOne('/api/batches').flush(null, { status: 0, statusText: 'Unknown Error' });
    client.post('/api/batches', {}).subscribe({ error: () => undefined });
    http.expectOne('/api/batches').flush({ errors: {} }, { status: 400, statusText: 'Bad Request' });

    expect(log).toHaveBeenCalledTimes(1);
    expect(log.mock.calls[0][0]).toContain("Can't reach the local service: GET /api/batches");
    log.mockRestore();
  });
});
