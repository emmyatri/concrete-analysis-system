import { HttpErrorResponse, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';

import { CreateBatchRequest } from '../batches';
import { BatchDetail, BatchSummary, CreateSpecimenRequest, RecordWarning } from '../models';
import { TECHNICIAN_HEADER } from '../../session/technician.interceptor';
import { USE_MOCKS } from '../use-mocks';
import { MOCK_BATCH_DETAILS, mockBatchDetailFromRequest } from './mock-batch-details';
import { MOCK_BATCHES, MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from './mock-data';


/** Batches saved while the app is open, so a new batch shows up in the list until you reload. */
const savedBatches: BatchSummary[] = [];
/** The same batches in full, so Batch Detail can open one right after it's saved. */
const savedDetails: BatchDetail[] = [];
let nextId = 1000;
/** Cylinders added while the app is open, so tests can take them back off the fake batches. */
const addedSpecimenIds = new Set<number>();

/** Forgets the batches and cylinders saved so far. Tests call this so one test's saves don't show up in another. */
export function clearSavedMockBatches(): void {
  savedBatches.length = 0;
  savedDetails.length = 0;
  for (const batch of MOCK_BATCH_DETAILS) {
    const kept = batch.specimens.filter((s) => !addedSpecimenIds.has(s.id));
    const summary = MOCK_BATCHES.find((row) => row.id === batch.id);
    if (summary) summary.cylinderCount -= batch.specimens.length - kept.length;
    batch.specimens = kept;
  }
  addedSpecimenIds.clear();
}

/**
 * Answers API calls with fake data (M1-60) when environment.useMocks is true. Development builds only.
 * Anything not handled here goes to the real server.
 */
export const mockApiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!inject(USE_MOCKS)) return next(request);

  const path = request.url.split('?')[0];
  if (request.method === 'GET' && path === '/api/validation-limits') return ok(MOCK_VALIDATION_LIMITS);
  if (request.method === 'GET' && path === '/api/specific-gravities') return ok(MOCK_SPECIFIC_GRAVITIES);
  if (request.method === 'GET' && path === '/api/batches') return ok(findBatches(request));
  if (request.method === 'POST' && path === '/api/batches') return createBatch(request);
  if (request.method === 'POST' && /^\/api\/batches\/[^/]+\/specimens$/.test(path)) return createSpecimen(request, path);
  if (request.method === 'GET' && path.startsWith('/api/batches/')) return getBatch(request, path);

  return next(request);
};

function ok<T>(body: T, status = 200): Observable<HttpResponse<T>> {
  return of(new HttpResponse({ status, body }));
}

function findBatches(request: HttpRequest<unknown>): BatchSummary[] {
  const param = (name: string) => request.params.get(name) ?? undefined;
  const search = param('search')?.trim().toLowerCase();
  const includeSynthetic = param('includeSynthetic') !== 'false';
  const castFrom = param('castFrom');
  const castTo = param('castTo');

  return [...savedBatches, ...MOCK_BATCHES]
    .filter((batch) => !search || batch.batchNumber.toLowerCase().includes(search))
    .filter((batch) => includeSynthetic || !batch.isSynthetic)
    .filter((batch) => !param('cementType') || batch.cementType === param('cementType'))
    .filter((batch) => param('hasWarnings') !== 'true' || batch.warningCount > 0)
    .filter((batch) => !castFrom || batch.castAt.slice(0, 10) >= castFrom)
    .filter((batch) => !castTo || batch.castAt.slice(0, 10) <= castTo)
    .sort((a, b) => b.castAt.localeCompare(a.castAt));
}

/** One batch by number, ignoring upper/lowercase, or a 404 like the server's. */
function getBatch(request: HttpRequest<unknown>, path: string) {
  const batchNumber = decodeURIComponent(path.slice('/api/batches/'.length));
  const batch = [...savedDetails, ...MOCK_BATCH_DETAILS].find(
    (row) => row.batchNumber.toLowerCase() === batchNumber.toLowerCase(),
  );
  // A copy, like a real response, so a screen that reloads sees a new object.
  if (batch) return ok(structuredClone(batch));

  return throwError(
    () =>
      new HttpErrorResponse({
        status: 404,
        statusText: 'Not Found',
        url: request.url,
        error: { title: 'Not Found', status: 404, detail: `Batch ${batchNumber} was not found.` },
      }),
  );
}

/**
 * Checks a new batch the way the server will: duplicate numbers and Reject limits fail with a 400,
 * Warn limits save and come back as warnings.
 */
function createBatch(request: HttpRequest<unknown>) {
  const body = (request.body ?? {}) as Record<string, Record<string, unknown> | undefined>;
  const batchNumber = String(body['batch']?.['batchNumber'] ?? '').trim();
  const errors: Record<string, string[]> = {};
  const warnings: RecordWarning[] = [];

  const exists = [...savedBatches, ...MOCK_BATCHES].some(
    (batch) => batch.batchNumber.toLowerCase() === batchNumber.toLowerCase(),
  );
  if (exists) errors['Batch.BatchNumber'] = [`Batch number ${batchNumber} already exists`];

  // { batch: { mixWaterPh } } → "Batch.MixWaterPh", { oxides: { aluminaPct } } → "Oxide.AluminaPct"
  const groups: Record<string, string> = { batch: 'Batch', composition: 'Composition', oxides: 'Oxide' };
  for (const [group, prefix] of Object.entries(groups)) {
    for (const [field, value] of Object.entries(body[group] ?? {})) {
      if (typeof value !== 'number') continue;
      const key = `${prefix}.${field[0].toUpperCase()}${field.slice(1)}`;
      for (const limit of MOCK_VALIDATION_LIMITS.filter((row) => row.key === key)) {
        const outside = (limit.min !== null && value < limit.min) || (limit.max !== null && value > limit.max);
        if (!outside) continue;
        if (limit.severity === 'Reject') errors[key] = [...(errors[key] ?? []), limit.message];
        else warnings.push({ key, message: limit.message });
      }
    }
  }

  if (Object.keys(errors).length > 0) {
    return throwError(
      () =>
        new HttpErrorResponse({
          status: 400,
          statusText: 'Bad Request',
          url: request.url,
          error: { title: 'One or more validation errors occurred.', status: 400, errors },
        }),
    );
  }

  const id = nextId++;
  savedBatches.unshift({
    id,
    batchNumber,
    castAt: String(body['batch']?.['castAt'] ?? new Date().toISOString()),
    cementType: (body['batch']?.['cementType'] as BatchSummary['cementType']) ?? 'TypeI',
    expectedFinalBreakPsi: Number(body['batch']?.['expectedFinalBreakPsi'] ?? 0),
    waterCementRatio: null,
    cylinderCount: 0,
    breaksRecorded: 0,
    average28DayPsi: null,
    warningCount: warnings.length,
    isSynthetic: false,
  });
  // The full batch too, so Batch Detail can open it straight after saving.
  const saved = request.body as CreateBatchRequest;
  const technician = request.headers.get(TECHNICIAN_HEADER);
  const createdBy = technician ? decodeURIComponent(technician) : 'Technician';
  savedDetails.unshift(
    mockBatchDetailFromRequest(id, { ...saved, batch: { ...saved.batch, batchNumber } }, createdBy),
  );
  return ok({ id, batchNumber, warnings }, 201);
}

/**
 * Adds a cylinder the way the server will: 404 for an unknown batch, 400 for a label already used on this
 * batch or a size outside its Reject limits. The cylinder is synthetic only when its batch is (FR-20).
 */
function createSpecimen(request: HttpRequest<unknown>, path: string) {
  const batchNumber = decodeURIComponent(path.split('/')[3]);
  const batch = [...savedDetails, ...MOCK_BATCH_DETAILS].find(
    (row) => row.batchNumber.toLowerCase() === batchNumber.toLowerCase(),
  );
  if (!batch) {
    return throwError(
      () =>
        new HttpErrorResponse({
          status: 404,
          statusText: 'Not Found',
          url: request.url,
          error: { title: 'Not Found', status: 404, detail: `Batch ${batchNumber} was not found.` },
        }),
    );
  }

  const body = request.body as CreateSpecimenRequest;
  const label = String(body.label ?? '').trim();
  const errors: Record<string, string[]> = {};
  if (batch.specimens.some((s) => s.label.toLowerCase() === label.toLowerCase())) {
    errors['Specimen.Label'] = [`Cylinder ${label} already exists on this batch`];
  }
  for (const [key, value] of [
    ['Specimen.LengthIn', body.lengthIn],
    ['Specimen.WidthIn', body.widthIn],
  ] as const) {
    for (const limit of MOCK_VALIDATION_LIMITS.filter((row) => row.key === key && row.severity === 'Reject')) {
      if ((limit.min !== null && value < limit.min) || (limit.max !== null && value > limit.max)) {
        errors[key] = [...(errors[key] ?? []), limit.message];
      }
    }
  }
  if (Object.keys(errors).length > 0) {
    return throwError(
      () =>
        new HttpErrorResponse({
          status: 400,
          statusText: 'Bad Request',
          url: request.url,
          error: { title: 'One or more validation errors occurred.', status: 400, errors },
        }),
    );
  }

  const id = nextId++;
  batch.specimens.push({
    id,
    label,
    lengthIn: body.lengthIn,
    widthIn: body.widthIn,
    testAgeDays: body.testAgeDays,
    isSynthetic: batch.isSynthetic,
    breakResult: null,
    curingReadings: [],
  });
  addedSpecimenIds.add(id);
  const summary = [...savedBatches, ...MOCK_BATCHES].find((row) => row.id === batch.id);
  if (summary) summary.cylinderCount += 1;
  return ok({ id, label }, 201);
}
