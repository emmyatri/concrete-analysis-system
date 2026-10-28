# Synthetic concrete batch dataset

**Status:** M1 dataset approved — Amelia Black, 2026-09-2; M2 revision pending review
**Requirements:** FR-20, A-2
**Source of truth:** Approved M1-02 `docs/schema.md` (2026-09-28)
**Data file:** `data/synthetic/batches.json`

## Purpose and structure

Nine **fabricated** batches support demonstrations and tests; they are not actual Vulcan production data or engineering predictions. Each batch has one `Composition`, one `CementOxideAnalysis`, and nine `Specimens` (three each at `Day3`, `Day7`, and `Day28`). Every specimen has one `BreakResult` and seven `CuringReadings`, taken at 0, 12, 24, 36, 48, 60, and 72 hours after casting. This produces 81 specimens, 81 break results, and 567 curing readings.

The nested JSON uses the exact approved schema field names for data fields, with entity names used as grouping keys. Generated database IDs, foreign keys, and audit fields are intentionally omitted because the importer and EF Core persistence layer will assign them.

`IsSynthetic: true` is set on every `Batch` and `Specimen`. Per the approved schema, `BreakResult` and `CuringReading` inherit synthetic status from their specimen and therefore do not contain their own `IsSynthetic` field. Audit fields must be populated by the save interceptor rather than hand-entered.

## Approved limits and their provenance

The following ranges are transcribed from the approved M1-02 `docs/schema.md`, section 4 (`ValidationLimit`). Its `Source` column identifies the client's documents, functional requirements, cited standards, or team-defined plausibility limits. These are **project validation settings**, not a claim that all are universal DOT limits.

| Field or derived value | Approved range or rule | Source in approved schema |
|---|---|---|
| `Composition.CementPct` | 10–15% by absolute volume | Client doc / FR-2.1 |
| `Composition.FineAggregatePct` | 25–30% by absolute volume | Client doc / FR-2.1 |
| `Composition.CoarseAggregatePct` | 40–55% by absolute volume | Client doc / FR-2.1 |
| `Composition.WaterPct` | 15–20% by absolute volume | Client doc / FR-2.1 |
| `Composition.ScmPct` | 0–100%; must match `ScmType` | FR-2 / Decision 1a |
| Calculated `TotalPct` | At most 100%; this dataset leaves approximately 5–6% for air/other | Decision 2 (NRMCA); 5–8% residual is this ticket's constraint |
| Calculated `WaterCementRatio` | Warning outside 0.40–0.60 | Client doc / FR-8 |
| `Oxide.AluminaPct` | Normal 3.0–7.5%; warn outside | Client doc table |
| `Oxide.IronOxidePct` | Normal 0.5–6.0%; warn outside | Client doc table |
| `Oxide.MagnesiumOxidePct` | Normal 0.1–4.0%; warn outside; reject above 6.0% | Client doc table / ASTM C150 as recorded in schema |
| `Oxide.SulfurTrioxidePct` | Normal 1.3–3.0%; warn outside; reject above 4.5% | Client doc table / ASTM C150 Table 1 as recorded in schema |
| `Batch.MixWaterPh` | Normal 7.0–7.5; warn outside; reject outside 1–9 | Client doc / FR-7 |
| `Batch.ExpectedFinalBreakPsi` | 3,000–10,000 PSI | Client doc / FR-5 |
| `Break.StrengthPsi` | 1–10,000 PSI; warn below 3,000 | Client doc / FR-5, Decision 3 |
| `Curing.AmbientTempF` | Reject outside 0–150°F; warn outside 60–80°F | Team plausibility / ASTM C31 as recorded in schema |
| `Curing.InternalTempF` | Reject outside 0–200°F | Team plausibility |
| `Curing.MoisturePct`, `RelativeHumidityPct` | 0–100% | FR-6 |
| `Curing.ResistivityOhmPerFt` | At least 0.01 ohms/ft | FR-6 |
| `Curing.Co2Ppm` | Nonnegative ppm | FR-6 |

Cement chemistry values are percentages **of the cement**, not percentages of the complete concrete mix. The synthetic measurements were chosen for demonstration and are not measurements from supplier certificates. Temperatures, moisture, humidity, resistivity, CO2, and strength trajectories are illustrative synthetic values rather than validated physical simulations.

## Ratio calculation and SCM interpretation

Composition percentages are by **absolute volume**. Per the approved schema, the calculated field named `WaterCementRatio` uses specific gravity and includes SCM in its denominator. When SCM is present, this is technically a **water-to-cementitious-material ratio (w/cm)**:

`WaterPct × 1.00 / (CementPct × 3.15 + ScmPct × SG(ScmType))`

Specific gravities from the approved schema are:

- Cement: 3.15
- Water: 1.00
- Fly ash: 2.35
- Slag: 2.90
- Silica fume: 2.35

`TotalPct`, `AirOtherPct`, and `WaterCementRatio` are derived values and therefore are not stored as JSON fields. Eight of the nine batches yield calculated w/cm ratios between 0.40 and 0.60. VM-SYN-006 intentionally exceeds the 0.60 warning threshold (0.603). The revised mixes leave approximately 5–6% of the total absolute volume unassigned for air/other. The remainder should not be interpreted as a measured air-content result.

SCM quantities were intentionally varied by SCM type. Because `ScmPct` is stored as a percentage of total mix absolute volume while SCM guidance is expressed relative to cement, the SCM-to-cement comparison is calculated using the material-specific specific gravities:

`(ScmPct × SG(ScmType)) / (CementPct × 3.15) × 100`

The revised synthetic mixes provide approximately:

- Fly ash: 15.7–23.9% relative to cement mass
- Slag: 25.0–31.3% relative to cement mass
- Silica fume: 6.1–9.2% relative to cement mass

These values provide visible variation between SCM types while remaining compatible with the approved absolute-volume schema.

## Composition variation

The synthetic compositions were varied so data entry, validation, and review can be demonstrated across different mixes.

Across the dataset:

- Coarse aggregate ranges from 40–45%.
- Fine aggregate ranges from 25–30%.
- Water ranges from 15–19%.
- SCM content varies by SCM type and includes batches without SCM.
- Entered composition totals remain approximately 94–95%, leaving approximately 5–6% for air/other.

The approved coarse aggregate validation range extends to 55%; however, the complete 40–55% range cannot be represented while simultaneously satisfying the other approved minimum composition values and retaining the ticket's 5–8% air/other remainder. At the minimum values of 10% cement, 25% fine aggregate, and 15% water, a 50% coarse aggregate mix already totals 100% before any SCM is included. Therefore, 45% is the highest feasible coarse aggregate value used by this synthetic dataset while retaining the remaining composition constraints.

## Designed scenarios

| Batch | Cement | SCM | Coarse | Fine | Water | Total | Air/other | Calculated w/cm | Intended scenario |
|---|---|---|---:|---:|---:|---:|---:|---:|---|
| VM-SYN-001 | TypeI | None | 40.0 | 30.0 | 15.0 | 95.0 | 5.0 | 0.476 | High-fine, no-SCM control |
| VM-SYN-002 | TypeII | FlyAsh (2.1%) | 41.0 | 25.0 | 16.0 | 94.1 | 5.9 | 0.439 | Cold-cured; lower fly-ash scenario |
| VM-SYN-003 | TypeIII | Slag (2.72%) | 40.5 | 25.0 | 16.5 | 94.72 | 5.28 | 0.419 | Moisture loss; lower slag scenario |
| VM-SYN-004 | TypeI | SilicaFume (0.9%) | 41.0 | 25.0 | 16.0 | 93.9 | 6.1 | 0.435 | Lower silica-fume scenario; near lower alumina normal limit |
| VM-SYN-005 | TypeII | None | 45.0 | 25.0 | 15.0 | 95.0 | 5.0 | 0.476 | High-coarse, no-SCM scenario; near upper magnesium oxide normal limit |
| VM-SYN-006 | TypeIII | None | 40.0 | 25.0 | 19.0 | 94.0 | 6.0 | 0.603 | High-water scenario: w/cm warning and intentionally misses expected 28-day strength |
| VM-SYN-007 | TypeI | Slag (3.4%) | 40.0 | 25.0 | 16.6 | 95.0 | 5.0 | 0.401 | Higher slag scenario |
| VM-SYN-008 | TypeII | SilicaFume (1.3%) | 42.0 | 25.0 | 15.5 | 94.3 | 5.7 | 0.429 | Higher silica-fume scenario |
| VM-SYN-009 | TypeIII | FlyAsh (3.2%) | 40.0 | 25.0 | 16.0 | 94.2 | 5.8 | 0.410 | Higher fly-ash scenario; **mix-water pH warning at 7.6** |

## M2 synthetic curing and strength data

These measurements and strength effects are fabricated for testing purposes. These are not real Vulcan data, validated relationships, or actual engineering predictions.

The dataset retains 9 batches, 81 specimens, 81 break results, and 567 curing readings. Existing batch identities, compositions, chemistry, pH values, specimen dimensions, and timestamps are preserved.

### How strength is generated

The generator starts with the ADR-005 maturity and strength equations. Internal curing temperature determines equivalent age, which feeds the ACI strength equation using the batch’s cement type and expected final strength.

It then applies three fabricated effects:

- **Water-to-cementitious-material ratio:** Above 0.50, each additional 0.01 reduces strength by 2%. This multiplier cannot fall below 0.50.
- **Moisture loss:** Each percentage point lost between day 1 and day 3 reduces later strength by 12%. The effect increases proportionally through day 7, then remains constant. This multiplier also cannot fall below 0.50.
- **SCM content:** Early strength is reduced according to SCM’s share of cementitious-material mass. At day 3, the reduction equals half that share. It decreases linearly to zero at day 28. This simplified effect applies to every represented SCM type.

Each specimen’s result receives deterministic random scatter between -5% and +5%, then rounds to whole PSI. The fixed seed makes regeneration reproducible.

### Demo cases

| Batch | Intended case |
|---|---|
| VM-SYN-001 | On track: reference-temperature curing and no moisture loss |
| VM-SYN-002 | Cold-cured: lower temperatures reduce maturity and strength |
| VM-SYN-003 | Moisture loss: moisture falls from 4.5% to 2.5% between days 1 and 3 |
| VM-SYN-006 | High w/c: retains the existing composition and a calculated w/cm of approximately 0.603 |

The three problem cases have lower measured final strengths relative to their targets. Confirming their automatic early-warning flags requires the warning evaluator and its configured rules.

### Regeneration and loading

From the repository root:

`node data/synthetic/generate.mjs`

To check reproducibility and configured rejection limits without rewriting the dataset:

`node data/synthetic/generate.mjs --check`

The existing seeder loads the regenerated JSON. Databases that already contain synthetic batches retain their existing demo data.

This change does not automatically replace it.

## Curing environment convention

Ambient temperature represents the shared curing/control-room environment rather than an independent measurement for each cylinder. Therefore, every specimen belonging to the same batch uses the **same `AmbientTempF` at the same `TakenAt` timestamp**.

`InternalTempF` remains specimen-specific and may vary between cylinders at the same timestamp. This allows individual specimen behavior to differ while maintaining a consistent shared ambient environment.

The synthetic dataset contains seven curing readings per specimen at 12-hour intervals through the first 72 hours.

## Timestamp and pH conventions

All timestamps use UTC ISO 8601 with a `Z` suffix. Synthetic casts occur in June 2026 so the 28-day breaks precede the September 2026 dataset preparation date. Specimen `TestAgeDays` uses the approved enum names (`Day3`, `Day7`, `Day28`). `BrokenAt` occurs at the target age plus a few minutes.

**Important schema distinction:** The approved schema has `Batch.MixWaterPh` but **does not contain** `CuringReading.Ph`. Consequently, the ticket's request for pH mostly 7.0–7.5 is represented by batch-level mix-water pH rather than invented per-reading pH fields.

Most synthetic batches use mix-water pH values within the normal 7.0–7.5 range. VM-SYN-009 intentionally uses a `MixWaterPh` of **7.6**, which remains within the accepted 1–9 range but is slightly outside the normal range. This provides a demonstration case for the application's pH warning behavior.

If the Project Lead requires pH at every curing interval, a schema change to M1-02 must be approved first; per-reading pH fields should not be silently added to the dataset.

## File encoding

`data/synthetic/batches.json` is stored as **UTF-8 without a byte order mark (BOM)**. Removing the UTF-8 BOM avoids unnecessary compatibility issues with command-line JSON tooling such as `jq` while remaining fully compatible with .NET.

## Review and import notes

The JSON is a nested interchange fixture, not an EF Core database export. An importer must create each batch and its composition and oxide record in the same transaction, assign generated IDs and foreign keys, and allow the audit interceptor to populate audit fields.


**How it loads.** With `SeedSyntheticData` on (`--SeedSyntheticData=true` on the command line, or the environment variable), the app loads this file once at startup, next to any real batches. It does nothing if synthetic batches already exist, and skips any batch number that's already taken.

Warnings represented by the synthetic scenarios, including chemistry or mix-water pH warnings, should be produced by the application's validation mechanism rather than hard-coded into the fixture as `RecordWarning` records.

Synthetic data must remain clearly labeled in the UI and reports. This fixture must never be presented as real laboratory evidence.

