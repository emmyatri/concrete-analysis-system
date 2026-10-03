import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { BatchDetail, BatchQuery, BatchSummary, SpecificGravity, ValidationLimit } from './models';

/**
 * Every call the screens make to the local API (M1-60), one method per request. All paths are under /api.
 * In development builds these can be answered with fake data instead (environment.useMocks).
 */
@Injectable({ providedIn: 'root' })
export class VulcanApiService {
  private readonly http = inject(HttpClient);

  getValidationLimits() {
    return this.http.get<ValidationLimit[]>('/api/validation-limits');
  }

  getSpecificGravities() {
    return this.http.get<SpecificGravity[]>('/api/specific-gravities');
  }

  /** Newest first. With no query, these are the recent batches for Home. */
  getBatches(query: BatchQuery = {}) {
    let params = new HttpParams();
    for (const [name, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') params = params.set(name, String(value));
    }
    return this.http.get<BatchSummary[]>('/api/batches', { params });
  }

  /** Everything about one batch. Fails with a 404 if there's no batch with that number. */
  getBatch(batchNumber: string) {
    return this.http.get<BatchDetail>(`/api/batches/${encodeURIComponent(batchNumber)}`);
  }
}
