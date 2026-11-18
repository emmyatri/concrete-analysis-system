import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';

import { CreateBatchRequest, UpdateBatchRequest } from '../batches';
import {
  BatchDetail,
  BatchSummary,
  CreateBreakResultRequest,
  CreateCuringReadingRequest,
  CreateSpecimenRequest,
  PendingBreak,
  RecordWarning,
} from '../models';
import { TECHNICIAN_HEADER } from '../../session/technician.interceptor';
import { USE_MOCKS } from '../use-mocks';
import { MOCK_BATCH_DETAILS, mockBatchDetailFromRequest } from './mock-batch-details';
import { MOCK_BATCHES, MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from './mock-data';
import { mockEarlyWarnings, mockStrength } from './mock-strength';

// Keep Search and Home warning counts aligned with Batch Detail.
// The detailed warnings are calculated using the current validation limits.
for (const summary of MOCK_BATCHES) {
  const detail = MOCK_BATCH_DETAILS.find((batch) => batch.batchNumber === summary.batchNumber);

  if (detail) {
    summary.warningCount = detail.warnings.length;
  }
}

/** Batches saved while the app is open, so a new batch shows up in the list until you reload. */
const savedBatches: BatchSummary[] = [];
/** The same batches in full, so Batch Detail can open one right after it's saved. */
const savedDetails: BatchDetail[] = [];
let nextId = 1000;

/** Settings' cloud folder in mock mode. Any path counts as existing. */
let mockCloudFolder: string | null = null;

/** The fake batches as they were at start-up. Saved cylinders and breaks change them, so tests put them back. */
const originalBatches = structuredClone(MOCK_BATCHES);
const originalDetails = structuredClone(MOCK_BATCH_DETAILS);

/**
 * Forgets the batches, cylinders and breaks saved so far. Tests call this so one test's saves don't
 * show up in another.
 */
export function clearSavedMockBatches(): void {
  mockCloudFolder = null;
  savedBatches.length = 0;
  savedDetails.length = 0;
  MOCK_BATCHES.splice(0, MOCK_BATCHES.length, ...structuredClone(originalBatches));
  MOCK_BATCH_DETAILS.splice(0, MOCK_BATCH_DETAILS.length, ...structuredClone(originalDetails));
}

/**
 * Answers API calls with fake data (M1-60) when environment.useMocks is true. Development builds only.
 * Anything not handled here goes to the real server.
 */
export const mockApiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!inject(USE_MOCKS)) return next(request);

  const path = request.url.split('?')[0];
  if (request.method === 'GET' && path === '/api/health') return ok({ status: 'ok' });
  if (request.method === 'GET' && path === '/api/validation-limits')
    return ok(MOCK_VALIDATION_LIMITS);
  if (request.method === 'GET' && path === '/api/specific-gravities')
    return ok(MOCK_SPECIFIC_GRAVITIES);
  if (request.method === 'GET' && path === '/api/batches') return ok(findBatches(request));
  if (request.method === 'POST' && path === '/api/batches') return createBatch(request);
  if (request.method === 'PUT' && /^\/api\/batches\/[^/]+$/.test(path))
    return updateBatch(request, path);
  if (request.method === 'POST' && /^\/api\/batches\/[^/]+\/specimens$/.test(path))
    return createSpecimen(request, path);
  if (request.method === 'GET' && /^\/api\/batches\/[^/]+\/strength$/.test(path))
    return getStrength(request, path);
  if (request.method === 'GET' && path.startsWith('/api/batches/')) return getBatch(request, path);
  if (request.method === 'GET' && path === '/api/breaks/pending') return ok(pendingBreaks(request));
  if (request.method === 'POST' && /^\/api\/specimens\/\d+\/break$/.test(path))
    return createBreak(request, path);
  if (request.method === 'POST' && /^\/api\/specimens\/\d+\/readings$/.test(path))
    return createReading(request, path);
  if (path === '/api/settings/cloud-folder') return cloudFolder(request);
  if (request.method === 'GET' && path === '/api/warnings/early')
    return ok(
      mockEarlyWarnings(
        [...savedDetails, ...MOCK_BATCH_DETAILS],
        request.params.get('includeSynthetic') !== 'false',
      ),
    );

  return next(request);
};

function ok<T>(body: T, status = 200): Observable<HttpResponse<T>> {
  return of(new HttpResponse({ status, body }));
}

function cloudFolder(request: HttpRequest<unknown>) {
  if (request.method === 'PUT') {
    mockCloudFolder = (request.body as { path: string }).path;
    return ok(null, 204);
  }
  if (request.method === 'DELETE') {
    mockCloudFolder = null;
    return ok(null, 204);
  }
  return ok({ path: mockCloudFolder, exists: mockCloudFolder !== null });
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
 * Saves an edited batch in place, like the server's PUT: its number, cast date, cylinders, history
 * and IsSynthetic stay as they were. Warnings on the batch, recipe and oxides are worked out again.
 */
function updateBatch(request: HttpRequest<unknown>, path: string) {
  const batchNumber = decodeURIComponent(path.slice('/api/batches/'.length));
  const list = [savedDetails, MOCK_BATCH_DETAILS].find((details) =>
    details.some((row) => row.batchNumber.toLowerCase() === batchNumber.toLowerCase()),
  );
  if (!list) {
    return problem(request, 404, 'Not Found', { detail: `Batch ${batchNumber} was not found.` });
  }

  const index = list.findIndex(
    (row) => row.batchNumber.toLowerCase() === batchNumber.toLowerCase(),
  );
  const existing = list[index];
  const body = request.body as UpdateBatchRequest;
  const rebuilt = mockBatchDetailFromRequest(
    existing.id,
    {
      batch: {
        batchNumber: existing.batchNumber,
        castAt: existing.castAt,
        finalWeightLb: body.finalWeightLb,
        expectedFinalBreakPsi: body.expectedFinalBreakPsi,
        cementType: body.cementType,
        mixWaterPh: body.mixWaterPh,
        cementSurfacePh: body.cementSurfacePh,
      },
      composition: body.composition,
      oxides: body.oxides,
    },
    existing.createdBy,
  );

  const batchLevel = ['Batch', 'Composition', 'CementOxideAnalysis'];
  list[index] = {
    ...rebuilt,
    isSynthetic: existing.isSynthetic,
    createdAtUtc: existing.createdAtUtc,
    modifiedAtUtc: new Date().toISOString(),
    modifiedBy: existing.createdBy,
    specimens: existing.specimens,
    warnings: [
      ...rebuilt.warnings,
      ...existing.warnings.filter((warning) => !batchLevel.includes(warning.entityType)),
    ],
  };
  return ok(structuredClone(list[index]));
}

/**
 * A batch's strength prediction, worked out from its current readings and breaks (mock-strength.ts),
 * or a 404 like the server's.
 */
function getStrength(request: HttpRequest<unknown>, path: string) {
  const batchNumber = decodeURIComponent(path.split('/')[3]);
  const batch = [...savedDetails, ...MOCK_BATCH_DETAILS].find(
    (row) => row.batchNumber.toLowerCase() === batchNumber.toLowerCase(),
  );
  if (batch) return ok(mockStrength(batch));
  return problem(request, 404, 'Not Found', { detail: `Batch ${batchNumber} was not found.` });
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
  const groups: Record<string, string> = {
    batch: 'Batch',
    composition: 'Composition',
    oxides: 'Oxide',
  };
  for (const [group, prefix] of Object.entries(groups)) {
    for (const [field, value] of Object.entries(body[group] ?? {})) {
      if (typeof value !== 'number') continue;
      const key = `${prefix}.${field[0].toUpperCase()}${field.slice(1)}`;
      for (const limit of MOCK_VALIDATION_LIMITS.filter((row) => row.key === key)) {
        const outside =
          (limit.min !== null && value < limit.min) || (limit.max !== null && value > limit.max);
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
    for (const limit of MOCK_VALIDATION_LIMITS.filter(
      (row) => row.key === key && row.severity === 'Reject',
    )) {
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
  const summary = [...savedBatches, ...MOCK_BATCHES].find((row) => row.id === batch.id);
  if (summary) summary.cylinderCount += 1;
  return ok({ id, label }, 201);
}

/** Every cylinder without a break, soonest due first (cast date + test age). */
function pendingBreaks(request: HttpRequest<unknown>): PendingBreak[] {
  const includeSynthetic = request.params.get('includeSynthetic') !== 'false';
  const due = (row: PendingBreak) =>
    Date.parse(row.castAt) + TEST_AGE_DAYS[row.testAgeDays] * DAY_MS;
  return [...savedDetails, ...MOCK_BATCH_DETAILS]
    .filter((batch) => includeSynthetic || !batch.isSynthetic)
    .flatMap((batch) =>
      batch.specimens
        .filter((specimen) => !specimen.breakResult)
        .map((specimen) => ({
          specimenId: specimen.id,
          batchNumber: batch.batchNumber,
          label: specimen.label,
          testAgeDays: specimen.testAgeDays,
          castAt: batch.castAt,
          isSynthetic: specimen.isSynthetic,
        })),
    )
    .sort((a, b) => due(a) - due(b));
}

const DAY_MS = 24 * 60 * 60 * 1000;
const TEST_AGE_DAYS = { Day3: 3, Day7: 7, Day28: 28 } as const;

/**
 * Records a break the way the server will: 404 for an unknown cylinder, 409 if it's already broken,
 * 400 for a strength outside 1–10,000 or not whole, or a date before the cast date or in the future.
 * A 28-day break below 3,000 PSI saves with a warning (failing breaks are kept). Earlier breaks are
 * expected to be lower and aren't flagged (client-decisions §3).
 */
function createBreak(request: HttpRequest<unknown>, path: string) {
  const specimenId = Number(path.split('/')[3]);
  const batch = [...savedDetails, ...MOCK_BATCH_DETAILS].find((row) =>
    row.specimens.some((s) => s.id === specimenId),
  );
  const specimen = batch?.specimens.find((s) => s.id === specimenId);
  if (!batch || !specimen) {
    return problem(request, 404, 'Not Found', { detail: `Cylinder ${specimenId} was not found.` });
  }
  if (specimen.breakResult) {
    return problem(request, 409, 'Conflict', {
      detail: `Cylinder ${specimen.label} already has a break result.`,
    });
  }

  const body = request.body as CreateBreakResultRequest;
  const errors: Record<string, string[]> = {};
  const warnings: RecordWarning[] = [];
  const strength = body.strengthPsi;
  if (!Number.isInteger(strength)) errors['Break.StrengthPsi'] = ['PSI must be a whole number'];
  for (const limit of MOCK_VALIDATION_LIMITS.filter((row) => row.key === 'Break.StrengthPsi')) {
    const outside =
      (limit.min !== null && strength < limit.min) || (limit.max !== null && strength > limit.max);
    if (!outside) continue;
    if (limit.severity === 'Reject')
      errors[limit.key] = [...(errors[limit.key] ?? []), limit.message];
    else if (specimen.testAgeDays === 'Day28')
      warnings.push({ key: limit.key, message: limit.message });
  }
  const brokenAt = new Date(body.brokenAt);
  const day = (date: Date) =>
    new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  if (Number.isNaN(brokenAt.getTime())) errors['Break.BrokenAt'] = ['Break date is required'];
  else if (day(brokenAt) < day(new Date(batch.castAt)))
    errors['Break.BrokenAt'] = ["Break date can't be before the cast date"];
  else if (day(brokenAt) > day(new Date()))
    errors['Break.BrokenAt'] = ["Break date can't be in the future"];

  if (Object.keys(errors).length > 0) {
    return problem(request, 400, 'Bad Request', {
      title: 'One or more validation errors occurred.',
      errors,
    });
  }

  const id = nextId++;
  specimen.breakResult = { id, brokenAt: brokenAt.toISOString(), strengthPsi: strength };
  for (const warning of warnings) {
    batch.warnings.push({
      id: nextId++,
      entityType: 'BreakResult',
      entityId: id,
      fieldName: 'StrengthPsi',
      limitKey: warning.key,
      value: strength,
      message: warning.message,
      createdAtUtc: new Date().toISOString(),
    });
  }
  const summary = [...savedBatches, ...MOCK_BATCHES].find((row) => row.id === batch.id);
  if (summary) {
    summary.breaksRecorded += 1;
    summary.warningCount += warnings.length;
    const day28 = batch.specimens.filter((s) => s.testAgeDays === 'Day28' && s.breakResult);
    if (day28.length > 0) {
      const total = day28.reduce((sum, s) => sum + s.breakResult!.strengthPsi, 0);
      summary.average28DayPsi = Math.round(total / day28.length);
    }
  }
  return ok({ id, warnings }, 201);
}

/** An error response shaped like ASP.NET's ProblemDetails. */
function problem(
  request: HttpRequest<unknown>,
  status: number,
  statusText: string,
  body: Record<string, unknown>,
) {
  return throwError(
    () =>
      new HttpErrorResponse({
        status,
        statusText,
        url: request.url,
        error: { title: statusText, status, ...body },
      }),
  );
}

/** Each reading field and the ValidationLimit key that checks it. */
const READING_FIELDS = [
  ['internalTempF', 'Curing.InternalTempF'],
  ['ambientTempF', 'Curing.AmbientTempF'],
  ['moisturePct', 'Curing.MoisturePct'],
  ['relativeHumidityPct', 'Curing.RelativeHumidityPct'],
  ['resistivityOhmPerFt', 'Curing.ResistivityOhmPerFt'],
  ['co2Ppm', 'Curing.Co2Ppm'],
] as const;

/**
 * Adds a curing reading the way the server will: 404 for an unknown cylinder, 400 for a value outside its
 * Reject limits or a time before the cast date or in the future. Warn limits (ambient 60–80 °F) save and
 * come back as warnings.
 */
function createReading(request: HttpRequest<unknown>, path: string) {
  const specimenId = Number(path.split('/')[3]);
  const batch = [...savedDetails, ...MOCK_BATCH_DETAILS].find((row) =>
    row.specimens.some((s) => s.id === specimenId),
  );
  const specimen = batch?.specimens.find((s) => s.id === specimenId);
  if (!batch || !specimen) {
    return problem(request, 404, 'Not Found', { detail: `Cylinder ${specimenId} was not found.` });
  }

  const body = request.body as CreateCuringReadingRequest;
  const errors: Record<string, string[]> = {};
  const warnings: RecordWarning[] = [];
  for (const [field, key] of READING_FIELDS) {
    const value = body[field];
    if (typeof value !== 'number' || !Number.isFinite(value)) {
      errors[key] = ['A number is required'];
      continue;
    }
    for (const limit of MOCK_VALIDATION_LIMITS.filter((row) => row.key === key)) {
      const outside =
        (limit.min !== null && value < limit.min) || (limit.max !== null && value > limit.max);
      if (!outside) continue;
      if (limit.severity === 'Reject') errors[key] = [...(errors[key] ?? []), limit.message];
      else warnings.push({ key, message: limit.message });
    }
  }
  const takenAt = new Date(body.takenAt);
  if (Number.isNaN(takenAt.getTime()))
    errors['Curing.TakenAt'] = ['Enter when the reading was taken'];
  else if (takenAt < new Date(batch.castAt))
    errors['Curing.TakenAt'] = ["Reading time can't be before the batch was cast"];
  else if (takenAt > new Date()) errors['Curing.TakenAt'] = ["Reading time can't be in the future"];

  if (Object.keys(errors).length > 0) {
    return problem(request, 400, 'Bad Request', {
      title: 'One or more validation errors occurred.',
      errors,
    });
  }

  const id = nextId++;
  specimen.curingReadings.push({
    id,
    takenAt: takenAt.toISOString(),
    internalTempF: body.internalTempF,
    ambientTempF: body.ambientTempF,
    moisturePct: body.moisturePct,
    relativeHumidityPct: body.relativeHumidityPct,
    resistivityOhmPerFt: body.resistivityOhmPerFt,
    co2Ppm: body.co2Ppm,
  });
  specimen.curingReadings.sort((a, b) => a.takenAt.localeCompare(b.takenAt));
  for (const warning of warnings) {
    const field = READING_FIELDS.find(([, key]) => key === warning.key)![0];
    batch.warnings.push({
      id: nextId++,
      entityType: 'CuringReading',
      entityId: id,
      fieldName: warning.key.split('.')[1],
      limitKey: warning.key,
      value: body[field],
      message: warning.message,
      createdAtUtc: new Date().toISOString(),
    });
  }
  const summary = [...savedBatches, ...MOCK_BATCHES].find((row) => row.id === batch.id);
  if (summary) summary.warningCount += warnings.length;
  return ok({ id, warnings }, 201);
}
