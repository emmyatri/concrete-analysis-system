
# Synthetic concrete batch dataset

**Status:** M1 dataset approved — Amelia Black, 2026-09-2. M2 revision (SCRUM-166) — Amelia Black, 2026-10-29
**Requirements:** FR-20, A-2
**Source of truth:** Approved M1-02 `docs/schema.md` (2026-09-28)
**Data file:** `data/synthetic/batches.json`

## Purpose and structure

Nine **fabricated** batches support demonstrations and tests; they are not actual Vulcan production data or engineering predictions. Each batch has one `Composition`, one `CementOxideAnalysis`, and nine `Specimens` (three each at `Day3`, `Day7`, and `Day28`). Every specimen has one `BreakResult` and curing readings taken every 12 hours through 72 hours after casting, then once a day until the cylinder is broken (7 readings for 3-day cylinders, 11 for 7-day, 32 for 28-day). This produces 81 specimens, 81 break results, and 1,350 curing readings.

The nested JSON uses the exact approved schema field names for data fields, with entity names used as grouping keys. Generated database IDs, foreign keys, and audit fields are intentionally omitted because the importer and EF Core persistence layer will assign them.

`IsSynthetic: true` is set on every `Batch` and `Specimen`. Per the approved schema, `BreakResult` and `CuringReading` inherit synthetic status from their specimen and therefore do not contain their own `IsSynthetic` field. Audit fields must be populated by the save interceptor rather than hand-entered.

## Approved limits and their provenance

The following ranges originate from the approved M1-02 `docs/schema.md`, section 4 (`ValidationLimit`), with the moisture convention updated under SCRUM-201. The `Source` column identifies the client's documents, functional requirements, cited standards, or team-defined plausibility limits. These are **project validation settings**, not a claim that all are universal DOT limits.

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
| `Break.StrengthPsi` | 1–10,000 PSI; warn below 3,000 on 28-day breaks | Client doc / FR-5, Decision 3 (28-day scope is a team proposal pending client confirmation) |
| `Curing.AmbientTempF` | Reject outside 0–150°F; warn outside 60–80°F | Team plausibility / ASTM C31 as recorded in schema |
| `Curing.InternalTempF` | Reject outside 0–200°F | Team plausibility |
| `Curing.MoisturePct` | Reject outside 0–15% of sample weight; normal 2–4%, warn outside | FR-6 / SCRUM-201 |
| `Curing.RelativeHumidityPct` | 0–100% relative humidity | FR-6 |
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

The synthetic compositions were varied so data entry, validation, and review can be demonstrated across different mixes. Since M2, every break strength is calculated from the batch's curing readings and composition (see [M2 synthetic strength data](#m2-synthetic-strength-data)).

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
| VM-SYN-002 | TypeII | FlyAsh (2.1%) | 41.0 | 25.0 | 16.0 | 94.1 | 5.9 | 0.439 | **Cold-cured**; lower fly-ash scenario |
| VM-SYN-003 | TypeIII | Slag (2.72%) | 40.5 | 25.0 | 16.5 | 94.72 | 5.28 | 0.419 | **Moisture loss**; lower slag scenario |
| VM-SYN-004 | TypeI | SilicaFume (0.9%) | 41.0 | 25.0 | 16.0 | 93.9 | 6.1 | 0.435 | Lower silica-fume scenario; near lower alumina normal limit |
| VM-SYN-005 | TypeII | None | 45.0 | 25.0 | 15.0 | 95.0 | 5.0 | 0.476 | High-coarse, no-SCM scenario; near upper magnesium oxide normal limit |
| VM-SYN-006 | TypeIII | None | 40.0 | 25.0 | 19.0 | 94.0 | 6.0 | 0.603 | **High w/c**: w/cm warning and intentionally misses expected 28-day strength |
| VM-SYN-007 | TypeI | Slag (3.4%) | 40.0 | 25.0 | 16.6 | 95.0 | 5.0 | 0.401 | Higher slag scenario |
| VM-SYN-008 | TypeII | SilicaFume (1.3%) | 42.0 | 25.0 | 15.5 | 94.3 | 5.7 | 0.429 | Higher silica-fume scenario |
| VM-SYN-009 | TypeIII | FlyAsh (3.2%) | 40.0 | 25.0 | 16.0 | 94.2 | 5.8 | 0.410 | Higher fly-ash scenario; **mix-water pH warning at 7.6** |

## M2 synthetic strength data

These measurements and strength effects are **made up for testing** (SLA 1.3). They are not Vulcan data, validated relationships, or engineering predictions. The generator is `data/synthetic/generate.mjs`; the effects and their sizes are constants at the top of that file.

### How the breaks are generated

Each specimen's break starts from the ADR-005 estimate: the Nurse-Saul equivalent age from that specimen's own internal temperatures (14 °F datum, 73.4 °F reference), put through the ACI 209R-92 curve for the batch's cement type and expected final strength. Four made-up effects are then applied (seed 176 for the scatter):

| Effect | Size | Applies to |
|---|---|---|
| Water-to-cementitious ratio | −2% per 0.01 above 0.50 (minimum multiplier 0.50) | Every age |
| Moisture loss, day 1 to day 3 | −16% per percentage point of batch-average moisture lost; losses under 0.2 percentage points are treated as normal variation and ignored | Every age |
| SCM content | Day 3 reduced by 10% of the SCM share of cementitious mass, shrinking linearly to nothing at day 28 | 3- and 7-day breaks |
| Scatter | Deterministic ±5% per specimen (seed 176), then rounded to whole PSI | Every break |

Synthetic moisture readings represent a percentage of sample weight, not relative humidity. Normal cylinders remain around 2–4%, using a baseline of 3.0% with deterministic variation. VM-SYN-003 receives an additional 1.3-percentage-point moisture-loss ramp from 24 to 72 hours, producing readings below 2%.

The moisture-loss strength coefficient (`MOISTURE_SLOPE = 0.16`), variation threshold (`MOISTURE_NOISE = 0.2`), and loss ramp (`MOISTURE_DROP = 1.3`) are **made up for testing** (SLA 1.3). They are not experimentally calibrated concrete-strength relationships.

The seed was chosen so that, with ±5% scatter, only the named demo cases below are flagged. That choice is part of the made-up data.

### Demo cases

| Batch | Case | What changed from M1 | What the app shows |
|---|---|---|---|
| VM-SYN-001 | On track | Daily readings added; breaks recalculated | No warnings. Projected 6,242 PSI against 6,200 |
| VM-SYN-002 | Cold-cured | Internal and ambient temperatures 25 °F lower (ambient 42–46 °F); daily readings added | Ambient warning on every reading (150); at risk before any break and after (projected 5,028 PSI, 10.21% short) |
| VM-SYN-003 | Moisture loss | Moisture drops below 2% of sample weight after a 1.3-point loss ramp from 24–72 h; daily readings added | 94 low-moisture reading warnings, summarized as one moisture reason; at risk after the 3- and 7-day breaks (projected 4,535 PSI, approximately 24.42% short) |
| VM-SYN-006 | High w/c | Daily readings added; breaks recalculated (w/cm 0.603 unchanged) | w/cm warning, expected-final-vs-w/cm warning, at risk (projected 4,733 PSI, 23.66% short) |
| VM-SYN-009 | pH | Daily readings added; breaks recalculated (mix-water pH 7.6 unchanged) | pH warning; on track |

All other batches remain on track with no warnings. Under SCRUM-201, moisture readings now use the percentage-of-sample-weight scale, with warnings outside the normal 2–4% range. VM-SYN-003 produces 94 low-moisture reading warnings, while the early-warning evaluation summarizes the moisture condition as a single reason. Its at-risk classification remains driven by the projected strength shortfall. No other synthetic batch produces moisture warnings.

### Running the generator

From the repository root:

- `node data/synthetic/generate.mjs` rewrites `batches.json`.
- `node data/synthetic/generate.mjs --check` fails if `batches.json` differs from the generator, breaks a rejection limit, or raises any warning other than the demo cases.
- `node --test data/synthetic/generate.test.mjs` runs the generator tests (also run in CI).

The cold, moisture and daily-reading changes are applied only once, so running the generator again leaves the data unchanged.

## Curing environment convention

Ambient temperature represents the shared curing/control-room environment rather than an independent measurement for each cylinder. Therefore, every specimen belonging to the same batch uses the **same `AmbientTempF` at the same `TakenAt` timestamp**.

`InternalTempF` remains specimen-specific and may vary between cylinders at the same timestamp. This allows individual specimen behavior to differ while maintaining a consistent shared ambient environment.

Each specimen has readings every 12 hours through the first 72 hours (from the M1 dataset), then one a day until it is broken, so 3-day cylinders stop at 72 h, 7-day at 168 h, and 28-day at 672 h. After 72 h, internal temperature cools toward the batch's ambient as hydration slows, resistivity keeps rising, and moisture, humidity and CO₂ hold near their earlier values. VM-SYN-002's shared ambient readings are 42–46 °F (the cold-cured case); every other batch stays within 60–80 °F.

## Timestamp and pH conventions

All timestamps use UTC ISO 8601 with a `Z` suffix. Synthetic casts occur in June 2026 so the 28-day breaks precede the September 2026 dataset preparation date. Specimen `TestAgeDays` uses the approved enum names (`Day3`, `Day7`, `Day28`). `BrokenAt` occurs at the target age plus a few minutes.

**Important schema distinction:** The approved schema has `Batch.MixWaterPh` but **does not contain** `CuringReading.Ph`. Consequently, the ticket's request for pH mostly 7.0–7.5 is represented by batch-level mix-water pH rather than invented per-reading pH fields.

Most synthetic batches use mix-water pH values within the normal 7.0–7.5 range. VM-SYN-009 intentionally uses a `MixWaterPh` of **7.6**, which remains within the accepted 1–9 range but is slightly outside the normal range. This provides a demonstration case for the application's pH warning behavior.

Intentional input warnings in this dataset are VM-SYN-002 ambient temperature (150 readings), VM-SYN-003 low moisture (94 readings), VM-SYN-006 w/cm (0.603), and VM-SYN-009 mix-water pH (7.6). This produces 246 persisted input warnings in total. Several 3-day breaks are below 3,000 PSI, but the 3,000 PSI warning applies to 28-day breaks only (client-decisions §3). Any other input warning raised on import indicates a validation or data error; `generate.mjs --check` enforces the expected warning cases.

If the Project Lead requires pH at every curing interval, a schema change to M1-02 must be approved first; per-reading pH fields should not be silently added to the dataset.

## File encoding

`data/synthetic/batches.json` is stored as **UTF-8 without a byte order mark (BOM)**. Removing the UTF-8 BOM avoids unnecessary compatibility issues with command-line JSON tooling such as `jq` while remaining fully compatible with .NET.

## Review and import notes

The JSON is a nested interchange fixture, not an EF Core database export. An importer must create each batch and its composition and oxide record in the same transaction, assign generated IDs and foreign keys, and allow the audit interceptor to populate audit fields.

**How it loads.** With `SeedSyntheticData` on (`--SeedSyntheticData=true` on the command line, or the environment variable), the app loads this file once at startup, next to any real batches. It does nothing if synthetic batches already exist, and skips any batch number that's already taken.

Warnings represented by the synthetic scenarios, including chemistry or mix-water pH warnings, are produced by the application's validators when the data loads (`SyntheticDataSeeder`), not hard-coded into the fixture as `RecordWarning` records.

Synthetic data must remain clearly labeled in the UI and reports. This fixture must never be presented as real laboratory evidence.
