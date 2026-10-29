# Plain-language early-warning messages

SCRUM-182 · Requirement: FR-16 · Implemented in `EarlyWarningService` (SCRUM-181)

## Purpose

Every early warning returned by `GET /api/batches/{batchNumber}/strength` and `GET /api/warnings/early` carries a sentence an operator can read without knowing internal codes. Messages never show a limit key and never invent a value, threshold or cause.

## Messages shipped in M2

### Prediction and target warnings (built with the batch's numbers)

| Limit key | Message | Example (VM-SYN-006) |
|---|---|---|
| `Prediction.MinimumPsi` | Projected 28-day strength of {projected} PSI is below the {minimum} PSI minimum | "Projected 28-day strength of 2,950 PSI is below the 3,000 PSI minimum" (illustrative; VM-SYN-006 doesn't trigger it) |
| `Prediction.ShortfallPct` | Projected 28-day strength of {projected} PSI is {pct}% below the expected {expected} PSI | "Projected 28-day strength of 4,449 PSI is 28.24% below the expected 6,200 PSI" |
| `Batch.ExpectedFinalVsWcmPct` | Expected final of {expected} PSI is {pct}% above the {aci} PSI typical for a w/cm of {ratio} | "Expected final of 6,200 PSI is 67.6% above the 3,700 PSI typical for a w/cm of 0.603" |

### Input warnings (configured messages from `validation-limits.json`)

These are the 10 measurements with a `Warn` range. The message is the configured text; the screen shows the measured value next to it.

| Limit key | Message |
|---|---|
| `Batch.CementSurfacePh` | Cement surface pH is outside the normal range [7.1-8.0] |
| `Batch.FinalWeightLb` | Sample weight is outside the normal range [20-30] for a 12x6 cylinder |
| `Batch.MixWaterPh` | pH is outside the normal range [7.0-7.5] |
| `Composition.WaterCementRatio` | The water to cement ratio is outside the normal range [0.4-0.6] |
| `Oxide.AluminaPct` | The aluminum oxide percent is outside the normal range [3.0-7.5] |
| `Oxide.IronOxidePct` | The iron oxide percent is outside the normal range [0.5-6.0] |
| `Oxide.MagnesiumOxidePct` | The magnesium oxide percent is outside the normal range [0.1-4.0] |
| `Oxide.SulfurTrioxidePct` | The sulfur trioxide percent is outside the normal range [1.3-3.0] |
| `Curing.AmbientTempF` | Ambient temperature is outside the normal range [60-80] |

`Break.StrengthPsi` also has a warning range, but saved break warnings are not early warnings (client-decisions §13). They appear in the Warnings tab and on the strength chart, not in the early-warning list.

Internal temperature, moisture, relative humidity, resistivity, CO₂, SCM percentage and aggregate percentages have no warning range in M2, so they never produce an early warning.

## Formatting

- PSI uses thousands separators and is rounded to whole PSI, matching the strength endpoint (SCRUM-169).
- Shortfall percentage is rounded to 2 decimal places; expected-vs-w/cm percentage to 1.
- The w/cm ratio is rounded to 3 decimal places before the ACI table lookup, so the message matches the w/c shown on screen.
- Formatting uses the invariant culture so output does not change with the computer's regional settings.
- Wording matches `mock-strength.ts`, which the M2 acceptance script (`docs/m2-acceptance.md`) is verified against. Change both together.

## Ordering

1. Prediction warnings: minimum PSI, then projected shortfall
2. Saved input warnings, in the order they were recorded
3. Expected final vs. w/cm

## Missing data

- Not enough curing data for a 28-day estimate: no prediction warnings. Input warnings and the w/cm check still apply.
- w/cm below 0.33: the w/cm check is skipped (ADR-005 §4.5). Above 0.82, the 2,000 PSI table value is used.

## Decisions (previously open questions)

| Question | Decision |
|---|---|
| Factor codes and shared types | `ValidationLimit` keys plus the three keys above; returned as `EarlyWarningDTO` (SCRUM-170) |
| Supporting values | `EarlyWarningDTO.Value` and `Limit`; prediction values from `EarlyWarningEvaluation` |
| Which measurements have warning ranges | The `Warn` rows in `validation-limits.json` |
| Ordering | As above |
| Missing data | As above; no invented numbers |
| CO₂ units | ppm; no warning range in M2 |

## M3 follow-ups

- Include the measured value in input-warning messages (for example "Mix water pH is 7.6, above the normal maximum of 7.5"). Update the acceptance script at the same time.
- Return the crossed bound in `Limit` for saved input warnings (currently 0).
- Move message building out of `EarlyWarningService` into a dedicated formatter if more warning types are added.
