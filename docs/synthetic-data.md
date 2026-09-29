# Synthetic concrete batch dataset

**Status:** Draft — Project Lead review required  
**Requirements:** FR-20, A-2  
**Source of truth:** Approved M1-02 `docs/schema.md` (2026-09-28)  
**Data file:** `data/synthetic/batches.json`

## Purpose and structure

Nine **fabricated** batches support demonstrations and tests; they are not actual Vulcan production data or engineering predictions. Each batch has one `Composition`, one `CementOxideAnalysis`, and nine `Specimens` (three each at `Day3`, `Day7`, and `Day28`). Every specimen has one `BreakResult` and seven `CuringReadings`, taken at 0, 12, 24, 36, 48, 60, and 72 hours after casting. This produces 81 specimens, 81 break results, and 567 readings. The nested JSON uses the exact approved schema field names for data fields, with entity names as grouping keys; generated database IDs, foreign keys, and audit fields are intentionally omitted because the importer/EF layer must assign them.

`IsSynthetic: true` is set on every `Batch` and `Specimen`. Per the approved schema, `BreakResult` and `CuringReading` **inherit** synthetic status from their specimen; they do not have an `IsSynthetic` field. Audit fields must be populated by the save interceptor rather than hand-entered.

## Approved limits and their provenance

The following ranges are transcribed from the approved M1-02 `docs/schema.md`, section 4 (`ValidationLimit`); its `Source` column identifies the client's documents, functional requirements, cited standards, or team-defined plausibility limits. They are **project validation settings**, not a claim that all are universal DOT limits.

| Field or derived value | Approved range or rule | Source in approved schema |
|---|---|---|
| `Composition.CementPct` | 10–15% by absolute volume | Client doc / FR-2.1 |
| `Composition.FineAggregatePct` | 25–30% by absolute volume | Client doc / FR-2.1 |
| `Composition.CoarseAggregatePct` | 40–55% by absolute volume | Client doc / FR-2.1 |
| `Composition.WaterPct` | 15–20% by absolute volume | Client doc / FR-2.1 |
| `Composition.ScmPct` | 0–100%; must match `ScmType` | FR-2 / Decision 1a |
| Calculated `TotalPct` | At most 100%; this dataset leaves 6–7% for air/other | Decision 2 (NRMCA); 5–8% residual is this ticket's constraint |
| Calculated `WaterCementRatio` | Warning outside 0.40–0.60 | Client doc / FR-8 |
| `Oxide.AluminaPct` | Normal 3.0–7.5%; warn outside | Client doc table |
| `Oxide.IronOxidePct` | Normal 0.5–6.0%; warn outside | Client doc table |
| `Oxide.MagnesiumOxidePct` | Normal 0.1–4.0%; warn outside; reject above 6.0% | Client doc table / ASTM C150 as recorded in schema |
| `Oxide.SulfurTrioxidePct` | Normal 1.3–3.0%; warn outside; reject above 4.5% | Client doc table / ASTM C150 Table 1 as recorded in schema |
| `Batch.MixWaterPh` | Normal 7.0–7.5; warn outside; reject outside 1–9 | Client doc / FR-7 |
| `Batch.ExpectedFinalBreakPsi` | 3,000–10,000 PSI | Client doc / FR-5 |
| `BreakResult.StrengthPsi` | 1–10,000 PSI; warn below 3,000 | Client doc / FR-5, Decision 3 |
| `CuringReading.AmbientTempF` | Reject outside 0–150°F; warn outside 60–80°F | Team plausibility / ASTM C31 as recorded in schema |
| `CuringReading.InternalTempF` | Reject outside 0–200°F | Team plausibility |
| `CuringReading.MoisturePct`, `RelativeHumidityPct` | 0–100% | FR-6 |
| `CuringReading.ResistivityOhmPerFt` | At least 0.01 ohms/ft | FR-6 |
| `CuringReading.Co2Ppm` | Nonnegative ppm | FR-6 |

Cement chemistry values are percentages **of the cement**, not the concrete mix. The synthetic measurements were chosen for demonstration and are not measurements from supplier certificates. The temperatures, moisture, humidity, resistivity, CO2 and strength trajectories are illustrative synthetic values, not validated physical simulations.

## Ratio calculation

Composition percentages are by **absolute volume**. Per the approved schema, the calculated field named `WaterCementRatio` uses specific gravity and includes SCM in its denominator (technically **water-to-cementitious-material ratio, w/cm**, when SCM is present):

`WaterPct × 1.00 / (CementPct × 3.15 + ScmPct × SG(ScmType))`

Specific gravities from the approved schema: cement 3.15; water 1.00; fly ash 2.35; slag 2.90; silica fume 2.35. `TotalPct`, `AirOtherPct`, and `WaterCementRatio` are **derived**, so they are not stored as JSON fields. All nine batches yield ratios between 0.40 and 0.60. The chosen mixes have 6–7% unassigned volume for air/other. Do not interpret the remainder as a measured air-content result.

## Designed scenarios

| Batch | Cement | SCM | Composition total | Air/other remainder | Calculated w/c(m) | Intended chemistry scenario |
|---|---|---|---:|---:|---:|---|
| VM-SYN-001 | TypeI | None | 93% | 7% | 0.423 | Ordinary |
| VM-SYN-002 | TypeII | FlyAsh | 94% | 6% | 0.407 | Ordinary |
| VM-SYN-003 | TypeIII | Slag | 93% | 7% | 0.429 | Ordinary |
| VM-SYN-004 | TypeI | SilicaFume | 94% | 6% | 0.407 | Near lower alumina normal limit (3.1%) |
| VM-SYN-005 | TypeII | None | 93% | 7% | 0.423 | Near upper magnesium oxide normal limit (3.9%) |
| VM-SYN-006 | TypeIII | FlyAsh | 93% | 7% | 0.442 | **Expected warning:** sulfur trioxide 3.3% exceeds normal 3.0%, below rejection limit 4.5% |
| VM-SYN-007 | TypeI | Slag | 93% | 7% | 0.429 | Ordinary |
| VM-SYN-008 | TypeII | SilicaFume | 94% | 6% | 0.407 | Ordinary |
| VM-SYN-009 | TypeIII | None | 93% | 7% | 0.423 | Ordinary |

## Timestamp and pH conventions

All timestamps use UTC ISO 8601 with a `Z` suffix. Synthetic casts occur in June 2026 so the 28-day breaks precede the September 2026 dataset preparation date. Specimen `TestAgeDays` uses the approved enum names (`Day3`, `Day7`, `Day28`). `BrokenAt` is at the target age plus a few minutes.

**Important schema distinction:** The approved schema has `Batch.MixWaterPh` but **does not contain** `CuringReading.Ph`. Consequently, the ticket's request for pH mostly 7.0–7.5 is represented by batch-level mix-water pH, not invented per-reading pH fields. If the Project Lead requires pH at every curing interval, first approve a schema change to M1-02; do not silently add fields to the dataset.

## Review and import notes

The JSON is a nested interchange fixture, not an EF database export. An importer must create each batch and its composition and oxide record in the same transaction, assign IDs and foreign keys, and let the audit interceptor populate audit fields. The expected sulfur-trioxide warning should be created by the application's validation mechanism rather than hard-coded as a `RecordWarning` record. Synthetic data should remain clearly labeled in the UI and reports. This fixture should never be presented as real laboratory evidence.

Before merging, confirm the Project Lead accepts the synthetic strength trajectories and the schema-compatible interpretation of the ticket's pH requirement. Verify automated checks, PR review, and the FR-20 / A-2 references in the pull request.
