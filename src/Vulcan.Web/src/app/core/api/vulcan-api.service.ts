import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { CreateBatchRequest, CreateBatchResponse } from './batches';
import {
  BatchDetail,
  BatchQuery,
  BatchSummary,
  CreateBreakResultRequest,
  CreateBreakResultResponse,
  CreateCuringReadingRequest,
  CreateCuringReadingResponse,
  CreateSpecimenRequest,
  CreateSpecimenResponse,
  PendingBreak,
  SpecificGravity,
  ValidationLimit,
} from './models';

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

  /** Saves a new batch with its composition and oxides. A 400 lists the bad values by ValidationLimit key. */
  createBatch(request: CreateBatchRequest) {
    return this.http.post<CreateBatchResponse>('/api/batches', request);
  }


  /** Cylinders not broken yet, soonest due first. Follows the Synthetic data switch like Search. */
  getPendingBreaks(includeSynthetic: boolean) {
    const params = new HttpParams().set('includeSynthetic', String(includeSynthetic));
    return this.http.get<PendingBreak[]>('/api/breaks/pending', { params });
  }

  /**
   * Records a cylinder's break. A 400 lists bad values by key ("Break.StrengthPsi", "Break.BrokenAt"),
   * a 409 means the cylinder already has a result, and a 404 means there's no such cylinder.
   */
  createBreakResult(specimenId: number, request: CreateBreakResultRequest) {
    return this.http.post<CreateBreakResultResponse>(`/api/specimens/${specimenId}/break`, request);
  }


  /**
   * Adds one curing reading to a cylinder. A 400 lists bad values by key ("Curing.AmbientTempF",
   * "Curing.TakenAt", …); a 404 means there's no such cylinder.
   */
  createCuringReading(specimenId: number, request: CreateCuringReadingRequest) {
    return this.http.post<CreateCuringReadingResponse>(
      `/api/specimens/${specimenId}/readings`,
      request,
    );
  }


  /**
   * Adds a cylinder to a batch. A 400 lists bad values by ValidationLimit key ("Specimen.Label" for a label
   * already used on this batch); a 404 means there's no batch with that number.
   */
  createSpecimen(batchNumber: string, request: CreateSpecimenRequest) {
    return this.http.post<CreateSpecimenResponse>(
      `/api/batches/${encodeURIComponent(batchNumber)}/specimens`,
      request,
    );
  }
}
