import { AbstractControl, FormControl, NonNullableFormBuilder, ValidatorFn, Validators } from '@angular/forms';
import { numberField } from '../../core/validation/number-field';
import { parseNumber } from '../../core/validation/parse-number';
import { CementType, CreateBatchRequest, ScmType } from '../../core/api/batches';

export const CEMENT_TYPES: { label: string; value: CementType }[] = [
  { label: 'Type I', value: 'TypeI' },
  { label: 'Type II', value: 'TypeII' },
  { label: 'Type III', value: 'TypeIII' },
];

export const SCM_TYPES: { label: string; value: ScmType }[] = [
  { label: 'None', value: 'None' },
  { label: 'Fly ash', value: 'FlyAsh' },
  { label: 'Slag', value: 'Slag' },
  { label: 'Silica fume', value: 'SilicaFume' },
];

/** Schema rules that aren't numeric limits (schema.md, Batch.BatchNumber). */
export const BATCH_NUMBER_HINT = 'Letters, digits, dashes · max 32';
const BATCH_NUMBER_MAX = 32;

export const batchNumberField: ValidatorFn = (control) => {
  const value = String(control.value ?? '').trim();
  if (!value) return null;
  if (value.length > BATCH_NUMBER_MAX)
    return { batchNumber: { message: `Max ${BATCH_NUMBER_MAX} characters` } };
  if (!/^[A-Za-z0-9-]+$/.test(value))
    return { batchNumber: { message: 'Letters, digits and dashes only' } };
  return null;
};

export const notInFuture: ValidatorFn = (control) => {
  const value = control.value as Date | null;
  return value && value.getTime() > Date.now()
    ? { future: { message: "Can't be in the future" } }
    : null;
};

/** SCM type must be None when SCM % is 0, and can't be None when SCM % is more than 0 (schema.md, Decision 1a). */
export const scmTypeMatchesPct: ValidatorFn = (control) => {
  const pct = parseNumber(control.parent?.get('scmPct')?.value);
  if (pct === null) return null;
  if (pct === 0 && control.value !== 'None')
    return { scmType: { message: 'Must be None when SCM is 0%' } };
  if (pct > 0 && control.value === 'None')
    return { scmType: { message: 'Choose the SCM type when SCM is more than 0%' } };
  return null;
};

function startOfMinute(date = new Date()): Date {
  date.setSeconds(0, 0);
  return date;
}

/** The New batch form. Range checks come from AppField (ValidationLimit); these are the format rules. */
export function createBatchForm(fb: NonNullableFormBuilder) {
  const numeric = (maxDecimals?: number) => fb.control('', [numberField(maxDecimals)]);

  return fb.group({
    batch: fb.group({
      batchNumber: fb.control('', [Validators.required, batchNumberField]),
      castAt: new FormControl<Date | null>(startOfMinute(), [Validators.required, notInFuture]),
      finalWeightLb: numeric(2),
      expectedFinalBreakPsi: numeric(0),
      cementType: new FormControl<CementType | null>(null, [Validators.required]),
      mixWaterPh: numeric(),
      cementSurfacePh: numeric(2),
    }),
    composition: fb.group({
      cementPct: numeric(),
      waterPct: numeric(),
      coarseAggregatePct: numeric(),
      fineAggregatePct: numeric(),
      scmType: fb.control<ScmType>('None', [scmTypeMatchesPct]),
      // Starts at 0 to match SCM type None (schema.md: "enter 0 if none").
      scmPct: fb.control('0', [numberField()]),
    }),
    oxides: fb.group({
      aluminaPct: numeric(),
      ironOxidePct: numeric(),
      magnesiumOxidePct: numeric(),
      sulfurTrioxidePct: numeric(),
    }),
  });
}

export type BatchForm = ReturnType<typeof createBatchForm>;

/**
 * Each control under the key the server uses for it ("Batch.BatchNumber", "Composition.CementPct",
 * "Oxide.AluminaPct"), so a 400 from the server can be shown next to the right field.
 */
export function controlsByKey(form: BatchForm): Map<string, AbstractControl> {
  const prefixes = { batch: 'Batch', composition: 'Composition', oxides: 'Oxide' } as const;
  const byKey = new Map<string, AbstractControl>();
  for (const [group, prefix] of Object.entries(prefixes)) {
    const controls = form.controls[group as keyof typeof prefixes].controls;
    for (const [name, control] of Object.entries(controls)) {
      byKey.set(`${prefix}.${name[0].toUpperCase()}${name.slice(1)}`, control as AbstractControl);
    }
  }
  return byKey;
}

export function toCreateBatchRequest(form: BatchForm): CreateBatchRequest {
  const { batch, composition, oxides } = form.getRawValue();
  const num = (text: string) => parseNumber(text) as number;

  return {
    batch: {
      batchNumber: batch.batchNumber.trim(),
      castAt: (batch.castAt as Date).toISOString(),
      finalWeightLb: num(batch.finalWeightLb),
      expectedFinalBreakPsi: num(batch.expectedFinalBreakPsi),
      cementType: batch.cementType as CementType,
      mixWaterPh: num(batch.mixWaterPh),
      cementSurfacePh: num(batch.cementSurfacePh),
    },
    composition: {
      cementPct: num(composition.cementPct),
      waterPct: num(composition.waterPct),
      coarseAggregatePct: num(composition.coarseAggregatePct),
      fineAggregatePct: num(composition.fineAggregatePct),
      scmType: composition.scmType,
      scmPct: num(composition.scmPct),
    },
    oxides: {
      aluminaPct: num(oxides.aluminaPct),
      ironOxidePct: num(oxides.ironOxidePct),
      magnesiumOxidePct: num(oxides.magnesiumOxidePct),
      sulfurTrioxidePct: num(oxides.sulfurTrioxidePct),
    },
  };
}
