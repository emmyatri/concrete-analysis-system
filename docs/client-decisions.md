# Client Decisions

**Last updated:** November 10, 2026 · **Status:** Approved (11/10) · **Contract reference:** SLA section 6.3 · **Requirements:** A-3, A-4, A-6, A-7, FR-5, FR-6, SC-4

When we started building, a handful of things in the client's requirements weren't spelled out: units, which ranges
block a save, how some numbers get calculated. Instead of waiting on email replies, we answered each question from
Vulcan's own document (*Standard DOT Concrete Mix Design Rev 1*, which we'll just call **Rev 1**) or from the industry
standards Rev 1 points to.

This file is where those answers live. If you're building something and aren't sure how it should behave, check here
first. If it's not here, ask Amelia or Tyler. Please don't contact the client directly.

The exact numbers for every rule are in [`schema.md`](schema.md), section 4. This file is the "why" behind them.

**Two words you'll see a lot:**

- **Reject:** the value can't be saved. The operator gets an error that says what the allowed range is.
- **Warn:** the value saves, but it's flagged in amber so someone looks at it.

---

## 1. Mix percentages are by volume, not weight

Rev 1 gives a percentage for each ingredient but never says whether it means weight or volume. We're treating them as **volume**: how much space each ingredient takes up in the finished concrete.

Rev 1's ranges (cement 10–15%, sand 25–30%, gravel 40–55%, water 15–20%) are almost word for word the volume proportions
NRMCA publishes: "Typically, a mixture is by volume about 10 to 15 percent cement, 60 to 75 percent aggregates and 15 to
20 percent water."

**"But plants mix by weight."** They do. Georgia DOT requires approved plants to weigh cement and aggregates on scales.
That's how the plant *measures* the ingredients. The recipe is still *described* by how much space each one fills. It's
like a baker who weighs flour on a scale while the recipe says "2 cups." Both describe the same flour.

**The check that settles it.** Rev 1 also says the ideal water-to-cement ratio is 0.40 to 0.60. If the percentages were
by weight, that ratio would just be water ÷ cement:

| If Rev 1 means... | Lowest possible ratio   | Highest possible ratio  | Ever lands in 0.40–0.60? |
|-------------------|-------------------------|-------------------------|--------------------------|
| Weight            | 15 ÷ 15 = 1.00          | 20 ÷ 10 = 2.00          | No, never                |
| Volume            | 15 ÷ (15 × 3.15) = 0.32 | 20 ÷ (10 × 3.15) = 0.63 | Yes                      |

Read by weight, Rev 1 contradicts itself. Read by volume, it works. Here's a typical cubic yard both ways (volume uses
specific gravities of 3.15 for cement, 2.65 for sand and 2.70 for stone):

| Ingredient | Pounds | By weight | By volume | Rev 1 says |
|------------|--------|-----------|-----------|------------|
| Cement     | 564    | 14%       | 11%       | 10–15%     |
| Water      | 280    | 7%        | 17%       | 15–20%     |
| Sand       | 1,300  | 33%       | 29%       | 25–30%     |
| Stone      | 1,800  | 46%       | 40%       | 40–55%     |

(The last ~4% by volume is air.)

If real Vulcan data ever shows up in pounds, we'll convert it to volume when we import it. That's a Milestone 2 problem.

*Sources:* Rev 1, "Core Ingredients" and "
Hydration" · [NRMCA, About Concrete](https://www.nrmca.org/about-nrmca/about-concrete/) · [Georgia DOT Lab SOP 10, Quality Assurance for Concrete Plants](https://www.dot.ga.gov/partnersmart/business/source/sop/sop10.pdf)
*Affects:* `Composition` table, batch entry form

## 1a. How we calculate water-to-cement ratio

Rev 1 defines the ratio by **mass** ("the mass of water to the mass of cement"), but we store volume. To convert, we
multiply each ingredient's volume by its **specific gravity**, which is how much heavier it is than the same amount of
water:

```
w/c = water% ÷ (cement% × 3.15  +  SCM% × SCM specific gravity)
```

We include SCM (fly ash, slag or silica fume) in the bottom half because Rev 1 says SCMs are "used to replace a portion
of cement." Strictly speaking, that makes it a water-to-*cementitious* ratio (w/cm), which is the industry term when
SCMs are counted. With no SCM, the two are the same number. The app **warns** outside 0.40–0.60.

Example: 12% cement, 18% water, no SCM → 18 ÷ (12 × 3.15) = **0.48**. No warning.

| Material        | Specific gravity | Where it comes from                            |
|-----------------|------------------|------------------------------------------------|
| Portland cement | 3.15             | "generally around 3.15" (Pavement Interactive) |
| Water           | 1.00             | By definition                                  |
| Slag            | 2.90             | PCA gives 2.85–2.95; we use the middle         |
| Fly ash         | 2.35             | PCA gives 1.9–2.8; we use the middle           |
| Silica fume     | 2.35             | PCA gives 2.2–2.5; we use the middle           |

These live in the `SpecificGravity` table, so if Vulcan gives us their actual numbers, we change the data, not the code.

*Sources:* Rev 1, "
Hydration" · [Pavement Interactive, Portland Cement Specific Gravity](https://pavementinteractive.org/reference-desk/testing/cement-tests/portland-cement-specific-gravity/) · [PCA,
*Design and Control of Concrete
Mixtures*, Ch. 3](https://www.ce.memphis.edu/1101/notes/concrete/PCA_manual_14/Chap03.pdf)
*Affects:* `SpecificGravity` table, `Composition.ScmType`, the w/c calculation

## 2. Ingredients can add up to 100% or less

Rev 1's ranges don't have to add up to exactly 100%, and in real concrete they usually don't. The rest is air (NRMCA: "
another 5 to 8 percent"). So the app **rejects** a total over 100% and shows whatever's left as "Air / other." We don't
store that number. It's calculated each time.

*Source:* [NRMCA, About Concrete](https://www.nrmca.org/about-nrmca/about-concrete/)
*Affects:* composition validation, the running total on the batch form

## 3. Break strength: 3,000–10,000 PSI, but failing breaks still save

Rev 1 gives the same range for the 3-day, 7-day and 28-day breaks and for the expected final break. We apply it two
ways:

- **Expected final break** (the design target): **reject** outside 3,000–10,000 PSI. A target outside that range is a
  typo.
- **Actual break results:** **warn** below 3,000 PSI, **reject** above 10,000 PSI or at 0 or below.

Why actual breaks below 3,000 aren't rejected: Rev 1 asks for an "early warning system" for mixes that "could produce a
failing break test." A failed break is exactly the data that system needs. If the app refused to save a 2,400 PSI break,
the failure would never be recorded and the prediction model would never learn from it. Low early breaks are also
normal: a 3-day break is often well under the 28-day strength.

*Source:* Rev 1: "3-day break in PSI (3000 to 10000 range)," repeated for each age; "What the software needs to
accomplish"
*Affects:* `BreakResult.StrengthPsi`, `Batch.ExpectedFinalBreakPsi`

**M2 change — team proposal, pending client confirmation (2026-10-29).** The 3,000 PSI warning now applies to
**28-day breaks only**. The strength model (ADR-005) expects Type I and II concrete to reach about 45–50% of its
28-day strength at 3 days, so a healthy 6,000 PSI mix breaks around 2,500–3,000 PSI at day 3. Flagging every such
break would bury the real problems. The 1–10,000 PSI rejection limits still apply at every age, and low early breaks
still drive the early warning through the projected 28-day strength (§13).

This differs from Rev 1, which lists the 3,000–10,000 range for each age. We are asking the client at the M2 review
(2026-11-02) whether the warning should apply to 28-day breaks only, or at every age. If the answer is every age,
revert `BreakResultValidator` to warn regardless of `TestAgeDays` and re-run `data/synthetic/generate.mjs`.

*Affects (M2 change):* `BreakResultValidator`, `SpecimenService`, `SyntheticDataSeeder`, the break dialog, and
`data/synthetic/generate.mjs`

**Client answer (2026-11-02).** Don answered the M2 question: the 3,000 PSI warning stays a 28-day rule, and early
breaks get their own bands instead. See §14. The M2 change above stands.

## 4. pH is the mix water's pH

Rev 1 says "PH reading 1 to 9 (7.0 to 7.5 is normal)." That range can't describe the concrete itself, which runs around
pH 12–13 while it cures. It does fit the mix water: Rev 1 calls for potable water "free from harmful amounts of oils,
acids, alkalis," and near-neutral water is right in 7.0–7.5.

So pH is recorded **once per batch, for the mix water**, not on every curing reading. Two rules: **reject** outside 1–9,
**warn** outside 7.0–7.5.

*Source:* Rev 1, "PH reading" and "Potable Water"
*Affects:* `Batch.MixWaterPh`

## 5. Temperatures in °F, with a curing-range warning

Rev 1 asks for internal and ambient temperature but doesn't give units or a range. Everything else in Rev 1 is in US
units (pounds, inches, PSI), so we use **°F**.

For ambient temperature, the app **warns** outside 60–80°F. That's the initial-curing range ASTM C31 sets for test
cylinders, so a reading outside it could explain a weak break.

*Source:* ["Who Is Watching Out for the Cylinders?" *Concrete
International*, via NRMCA](https://www.nrmca.org/wp-content/uploads/2020/06/CI4008cylindercuring.pdf) (explains the ASTM
C31 curing requirements)
*Affects:* `CuringReading.InternalTempF`, `CuringReading.AmbientTempF`

## 6. Batch numbers: flexible for now

We don't know what Vulcan's batch numbers look like, and there's no industry-standard format. Georgia DOT requires each
delivery to come with a ticket (project number, truck number, batch time, proportions and so on) but doesn't say how a
ticket or batch number should be formatted.

So for now the app accepts **letters, numbers and dashes, up to 32 characters**. Each number must be unique, and
capitalization doesn't matter (`A-100` and `a-100` are the same batch). Once we see a real Vulcan batch number, we'll
tighten this.

*Source:* [Georgia DOT Lab SOP 10](https://www.dot.ga.gov/partnersmart/business/source/sop/sop10.pdf), batch ticket
requirements
*Affects:* `Batch.BatchNumber`, batch search

## 7. All four cement oxides are required

Rev 1 marks alumina, iron oxide, magnesium oxide and sulfur trioxide each as "needs this concentration in test
software," so all four are **required**.

There are two levels of limits:

- **Warn** when a value is outside the normal range for Portland cement, from the composition table in Rev 1: alumina
  3.0–7.5%, iron oxide 0.5–6.0%, magnesium oxide 0.1–4.0%, sulfur trioxide 1.3–3.0%.
- **Reject** when a value is over the maximum the national cement spec allows for any cement type we accept: magnesium
  oxide above 6.0%, sulfur trioxide above 4.5%.

Rev 1 says the cement must conform to "AASHTO/ASTM standards." That means AASHTO M 85 or ASTM C150, which set
essentially the same limits. Georgia DOT requires AASHTO M 85 for all cement on its projects.

**Why 4.5% for sulfur trioxide.** ASTM C150's limit depends on the cement type and its tricalcium aluminate (C₃A)
content:

| Type | C₃A 8% or less | C₃A over 8% |
|------|----------------|-------------|
| I    | 3.0%           | 3.5%        |
| II   | 3.0%           | n/a         |
| III  | 3.5%           | 4.5%        |

We don't collect C₃A, and decision 8 allows Type III, so 4.5% is the highest value a valid mill certificate can show. A
lower hard limit would block real Type III cement. Anything above 3.0% still gets the amber warning.

One thing to keep straight: these are percentages **of the cement**, not of the whole batch. They never count toward the
100% total in decision 2.

*Sources:* Rev 1, oxide sections and "Portland Cement Ingredient
Composition" · [ASTM C150, Standard Specification for Portland Cement](https://store.astm.org/standards/c150), Table
1 · [Georgia DOT Section 830, Portland Cement](https://www.dot.ga.gov/PartnerSmart/Business/Source/specs/ss830.pdf) · [NPCA, "A Closer Look: Cement Types I Through V"](https://precast.org/blog/a-closer-look-cement-types-i-through-v/) (
6% magnesium oxide max)
*Affects:* `CementOxideAnalysis` table

## 8. Cement type is recorded per batch

Rev 1 says the mix uses "Portland Cement (Type I, II, or III)," so every batch records which one. Georgia DOT allows
Types I and II for general work and Types I and III for high-early-strength concrete, so all three are realistic.

*Sources:* Rev 1 · [Georgia DOT Section 830](https://www.dot.ga.gov/PartnerSmart/Business/Source/specs/ss830.pdf)
*Affects:* `Batch.CementType` (A-4)

## 9. Units and field names

- Resistivity is in **ohms per foot**, as Rev 1 lists it.
- - **CO₂ is measured as a percentage of total batch weight.** This supersedes our original ppm assumption following client confirmation (Don Murphy, Oct 21, 2026). The existing schema, API contract, seed data, and mocks still use the original unit and require updates under a separate ticket. See decision 13.
- **"Sand" and "fine aggregate" are one field** (`FineAggregatePct`). Rev 1's entry list names them separately, but its
  ingredient section defines them as the same thing: "Fine Aggregate / Sand (25–30%)" and "Fine Aggregates: Clean washed
  concrete sand."

*Source:* Rev 1
*Affects:* `CuringReading`, `Composition`, entry forms

## 10. Foreign object detection is on hold

Rev 1: "Foreign object detection we can leave off at this time but if there is time near the end, we can discuss and
maybe integrate that feature." It's not part of Milestone 1.

## 11. Batch final weight field correction

It was brought up by the client in the Milestone 1 Meeting that each batch's final weight (in lbs) should be about 25
pounds per the average cylinder. We have adjusted the ranges to warn values outside of 20-30 lbs and reject values outside
of 10-40 lbs.

---

## 12. Cement surface pH is recorded separately from mix water pH

At the M1 demo, the client requested the cement's surface pH alongside the mix water pH.
`Batch.CementSurfacePh` is required when creating or editing a batch, with at most two
decimal places. Reject outside 0-14 and warn outside the normal 7.1-8.0 range.
The ranges and messages come from `ValidationLimit` data.

The database column is nullable so historical batches still load. Historical null values
are not backfilled; the operator supplies the measurement when editing a batch.

*Source:* Client, M1 demo (Oct 9)
*Affects:* `Batch.CementSurfacePh`, FR-7 (extended), NFR-6; SCRUM-149

---

## 13. Two-tier early warning system (M2-50)

The early warning system evaluates recorded batch data, curing conditions, and predicted 28-day compressive strength to identify potential problems before cylinder break testing.

Warnings are informational and do not prevent valid data from being saved. Existing rejection rules remain in effect.

### Tier 1 — Range verification

The system checks recorded values against their configured `ValidationLimit` ranges.

- Values outside a Reject range are blocked during data entry.
- Values outside a Warn range are saved and flagged.
- Fields without an established warning range will not receive an arbitrary threshold.

Existing warning ranges cover water-cementitious ratio, cement oxides, mix water pH, cement surface pH, ambient temperature, and other previously established limits.

Once a valid strength prediction is available, the system also checks whether the predicted 28-day strength falls below the client's existing 3,000 PSI minimum.

**Proposed limit:** `Prediction.MinimumPsi`
**Warning condition:** Predicted 28-day PSI < 3,000

The Batch Detail strength chart will use the configured `Prediction.MinimumPsi` value for its minimum-strength reference line, rather than hard-coding 3,000 PSI in the UI.

This proposed prediction rule is based on the client's existing 3,000–10,000 PSI strength range. Applying that minimum to predicted strength requires client confirmation.

### Tier 2 — Projected strength shortfall

The system compares the current 28-day strength estimate against the batch's `ExpectedFinalBreakPsi`. When recorded breaks are available, the estimate includes the adjustments defined in SCRUM-162. Otherwise, the original unadjusted estimate is used.

```text
ShortfallPct =
    (ExpectedFinalBreakPsi - Current28DayEstimatePsi)
    / ExpectedFinalBreakPsi * 100
```

A warning is generated when the predicted strength falls more than 5% below the batch's expected final strength.

**Proposed limit:** `Prediction.ShortfallPct`
**Warning condition:** ShortfallPct > 5%

This threshold is proposed for Milestone 2 and requires client confirmation.

### Independent prediction warnings

The minimum-strength and projected-shortfall warnings are evaluated independently.

If the predicted 28-day strength is below 3,000 PSI and more than 5% below `ExpectedFinalBreakPsi`, both warnings are generated.

Each warning identifies its own reason and limit key. This provides operators with more specific feedback and allows the development team to distinguish minimum-strength concerns from target-strength shortfalls.

### Water-cementitious ratio strength warning

The system also checks whether the batch's `ExpectedFinalBreakPsi` is reasonable for its calculated water-cementitious ratio (w/cm).

Using ACI 211.1-97, Table 6.3.4(a), the system linearly interpolates an approximate 28-day compressive strength for non-air-entrained concrete.

```text
ExpectedFinalVsWcmPct =
    (ExpectedFinalBreakPsi - AciTablePsi)
    / AciTablePsi * 100
```

A warning is generated when the expected final strength exceeds the interpolated strength by more than 30%.

**Proposed limit:** `Batch.ExpectedFinalVsWcmPct`
**Warning condition:** ExpectedFinalVsWcmPct > 30%

Below a w/cm of 0.33, this check is skipped. Above 0.82, the
2,000 PSI endpoint is used without further extrapolation.

This warning is independent of the predicted-strength warnings and can be evaluated when a batch is entered, without curing readings or a strength prediction.

The warning does not modify `ExpectedFinalBreakPsi` or the strength estimation equation.

The 30% threshold is an application-specific proposal, not a requirement established by ACI 211.1.

### Warning availability

Batch-entry warnings are evaluated when a batch is created or updated.

Curing warnings are evaluated as readings become available, including day-1 and day-3 measurements, using established `ValidationLimit` warning ranges.

Prediction-related warnings are evaluated only when sufficient curing readings are available to produce a valid 28-day strength estimate.

If insufficient curing data is available, prediction-related warnings are not generated. Existing batch-entry and curing warnings remain active.

### Changes over time

The system will display changes in temperature, moisture, and resistivity over time, but these trends will not independently trigger early warnings during Milestone 2.

Vulcan has not provided thresholds for evaluating these changes. Milestone 3 may introduce trend-based warnings after sufficient real testing data is available.

### Pending client decisions — Curing warning thresholds

SLA A-3 assigns responsibility for supplying early-warning thresholds to Vulcan.

Our existing `ValidationLimit` configuration includes warning ranges for several batch inputs and ambient temperature. However, some measurements currently have only rejection limits intended to prevent invalid data entry.

The following warning ranges require clarification from Vulcan:

| Measurement | Client-provided information | Remaining clarification |
|---|---|---|
| Internal temperature (°F) | Not provided | Normal curing range and hard limits |
| Moisture (%) | ~~3.5–5% of total batch weight~~ Replaced by 2–4% of sample weight (Nov 2, see §14) | Confirm rejection limits |
| Relative humidity (%) | Below 75% | Treat 75% or above as outside the normal range; confirm warning versus rejection threshold |
| Electrical resistance (Ω) | Below 7,000–9,000 Ω, measured end to end | Confirm exact upper threshold, testing method, and compatibility with stored resistivity (ohms/ft) |
| CO₂ (%) | 0.5–0.7% of total batch weight | Supersedes decision 9; API, schema, contract, seed data, and mock updates tracked separately |
| Ambient temperature (°F) | No new range provided | Confirm whether existing 60–80°F warning range applies and establish hard limits |

Vulcan should also confirm whether separate warning ranges are needed for SCM percentage and aggregate proportions. These fields currently have rejection limits but no warning limits.

Until the client provides these ranges, the system will continue enforcing existing rejection limits without generating additional early warnings for these measurements.

Once Vulcan confirms the thresholds, they will be added as `ValidationLimit` Warn rows. Changes to the thresholds will be data changes rather than changes to the warning evaluation logic.

**Status:** Partially confirmed by the client (Don Murphy, Oct 21, 2026). Normal ranges have been provided for moisture, relative humidity, electrical resistance, and CO₂. Remaining clarifications include rejection thresholds for these measurements, the exact electrical resistance upper limit, and normal and rejection ranges for internal and ambient temperatures. These outstanding questions will be included in the M2-81 client communication under SLA A-3.

### Warning limit configuration

The following prediction-related limits will be stored in `ValidationLimit`, subject to client approval:

| Limit key | Severity | Min | Max | Unit |
|---|---|---:|---:|---|
| `Prediction.MinimumPsi` | Warn | 3000 | — | PSI |
| `Prediction.ShortfallPct` | Warn | — | 5 | % |
| `Batch.ExpectedFinalVsWcmPct` | Warn | — | 30 | % |

The minimum-strength rule warns below its minimum. The percentage-based rules warn above their maximum.

All three thresholds are proposed and may be adjusted following client feedback.

*Sources:* Rev 1, strength ranges and early warning requirements; ACI 211.1-97, Table 6.3.4(a); ADR-005, strength estimation method; Client email, Don Murphy (Oct 21, 2026).

*Affects:* `ValidationLimit`, SCRUM-181 (early warning implementation)
*Requirements:* SLA A-3, FR-16

---

## 14. Client answers, Nov 2, 2026

At the Milestone 2 review, Don Murphy provided clarification on early
break warnings, prediction accuracy, moisture measurements, and the
recommended actions for batches showing insufficient strength.

These answers establish the client-directed behavior for Milestone 3
and supersede conflicting earlier assumptions in sections 3 and 13.

### 14.1 Early break warning thresholds

**Question:**

> Break warning: right now, the 3,000 PSI warning applies only to
> 28-day breaks, since 3- and 7-day breaks are normally lower.
> Should it apply at every age instead?

**Client's answer:**

> 3 day break should be 30 to 40% of 3000 PSI  Early warning
>
> 7 day break should be 60 to 70% of 3000 PSI   Warning the batch
> will probably fail.

**Decision:**

Early break warnings will use age-specific strength thresholds
rather than applying the full 3,000 PSI minimum at every test age.

| Test age | Client threshold | Equivalent PSI | Expected behavior |
|----------|------------------|----------------|-------------------|
| 3 days | 30–40% of 3,000 PSI | 900–1,200 PSI | Early warning; monitor at day 7 |
| 7 days | 60–70% of 3,000 PSI | 1,800–2,100 PSI | Warn that the batch will probably fail |
| 28 days | 100% of 3,000 PSI | 3,000 PSI | Retain the existing minimum-strength warning |

Actual break results below the warning thresholds must remain
recordable rather than being rejected solely because they are
below 3,000 PSI.

The percentage ranges reflect the client's specified warning
criteria. Exact warning boundaries will be established during
implementation using these confirmed ranges.

*Affects:* FR-5, FR-16; sections 3 and 13.

### 14.2 Prediction accuracy benchmark

**Question:**

> Accuracy benchmark: what accuracy should we hold the 28-day
> estimate to (for example, within 5% of the actual break), and
> should we measure that per batch or as an average across batches?

**Client's answer:**

> 28 day estimate. I think 5% of the actual break will work.
> After enough data is gathered, it could be adjusted to reflect
> actual data.

**Decision:**

The prediction engine will use an initial accuracy target of
**within 5% of the actual 28-day compressive strength**.

The benchmark compares the predicted 28-day strength against
the measured 28-day break result.

This target may be adjusted after sufficient real testing data
has been collected and evaluated.

This accuracy benchmark is separate from the proposed 5%
projected-strength shortfall warning in section 13.

Prediction accuracy measures the difference between estimated
and actual strength. Projected shortfall measures the difference
between estimated strength and the batch's expected final strength.

**Current implementation note (November 9, 2026):**

The existing prediction engine has been evaluated against the
synthetic dataset. One synthetic batch currently produces a 28-day
strength estimate with an accuracy slightly outside the newly
established 5% target.

This deviation is documented as a known limitation of the current
implementation and does not indicate that the prediction engine
requires immediate modification.

The 5% benchmark will serve as the accuracy target for future
validation and refinement. Performance should be reassessed as
additional testing data becomes available, particularly real-world
batch and break results.

*Affects:* SC-4, SC-5; prediction accuracy evaluation.

### 14.3 Moisture measurement and alarm range

**Question:**

> Moisture sensors: when your sensors report moisture as a
> percentage, what is it a percentage of? For example, would a
> reading of 92% mean the concrete is 92% saturated (relative),
> or that 92% of its weight is water? We want to make sure our
> normal ranges match what your sensors actually send.

**Client's answer:**

> Moisture content should be 2 to 4% of the total weight of the
> batch sample or an alarm is set.

**Decision:**

Moisture content is measured as a **percentage of the total weight
of the batch sample**, not as relative moisture content or saturation.

The confirmed normal range is **2–4% by sample weight**.

| Moisture reading | Expected behavior |
|------------------|-------------------|
| Below 2% | Generate a moisture alarm |
| 2–4%, inclusive | Within the normal range |
| Above 4% | Generate a moisture alarm |

Moisture readings outside the normal range will generate warnings
rather than being rejected solely for exceeding the normal range.

Existing data-entry rejection limits remain separate.

This decision supersedes the earlier 3.5–5% moisture range recorded
in section 13 and clarifies the measurement basis described in FR-6.

*Affects:* FR-6, FR-16; section 13, moisture validation and
warning configuration.

### 14.4 Recommended action after consecutive short breaks

**Question:**

> Next steps: when a batch is projected to come in short at day 3,
> what does your team actually do? This tells us what the warnings
> should recommend in Milestone 3.

**Client's answer:**

> If a break comes in short on day 3, it should be a warning and
> flagged to watch on day 7. If day 7 is still short, it is a bad
> batch and should be abandoned and a new batch started.

**Decision:**

The early warning system will provide recommendations based on
consecutive insufficient break results.

| Condition | System recommendation |
|-----------|-----------------------|
| Day-3 break is below its applicable threshold | Issue an early warning and flag the batch for review at day 7 |
| Day-7 break is also below its applicable threshold | Warn that the batch is likely to fail and recommend abandoning it and starting a new batch |

These recommendations are informational.

The system will not automatically abandon a batch, delete its
records, or create a replacement batch.

The abandonment recommendation applies when both day-3 and day-7
break results are short.

A short day-7 result without a preceding short day-3 result still
receives its applicable strength warning, but does not automatically
satisfy the consecutive-short-break condition.

*Affects:* FR-16; early warning recommendations.

### 14.5 SLA requirements affected

The November 2 client clarifications affect the following
requirements:

| Requirement | Effect of client clarification |
|-------------|--------------------------------|
| FR-5 | Establishes age-specific early break warning thresholds instead of applying 3,000 PSI at every test age |
| FR-6 | Clarifies moisture as a percentage of sample weight, with an alarm outside 2–4%, rather than relative moisture content |
| FR-16 | Establishes early warning behavior and recommended actions for short day-3 and day-7 breaks |
| SC-4 | Establishes an initial 5% accuracy target for the 28-day strength estimate |
| SC-5 | Aligns prediction accuracy evaluation with the confirmed benchmark |

These client clarifications refine existing application behavior
and acceptance criteria without changing milestone content,
milestone dates, or the final deliverable.

Under SLA section 6.3, changes that can be accommodated without
affecting milestone content or dates may be recorded in writing
and implemented without re-execution of the agreement.

The agreement also requires:

- Written submission of the requested change, copying the
  Faculty Supervisor.
- A written feasibility assessment from the development team
  within two business days, addressing scope, milestone content,
  milestone dates, and the final deliverable.
- Written confirmation of any verbal agreement before it is
  acted upon.

This section records the client's answers and their anticipated
effect on the requirements. It does not independently establish
that all SLA 6.3 communication requirements have been completed.

*Source:* Don Murphy, client responses, November 2, 2026.

*Contract reference:* SLA section 6.3, Amendment of this Agreement.

*Status:* Client answers received and documented for Milestone 3.
Implementation will follow the confirmed client requirements and
the written amendment process established in SLA section 6.3.

---

## Our own defaults (not client rules)

These fill gaps Rev 1 doesn't cover. They're our calls, and we'll change them if real data proves them wrong. The
numeric ones live in the `ValidationLimit` table, so changing them is a data change, not a code change.

- **Windows 11, 64-bit.** We build on .NET 10, which supports Windows 10 only in its Enterprise, LTSC and IoT
  editions ([supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)). If
  Vulcan's workstations are still on Windows 10, we need to know early. Affects the installer and build target (
  `win-x64`).
- **Temperature typo guards.** Ambient rejects outside 0–150°F and internal outside 0–200°F. These don't reflect how
  concrete behaves. They just catch someone typing 720 instead of 72.
- **Times.** Stored in UTC, shown in the computer's local time, so daylight saving never scrambles the order of
  readings. Vulcan sees Eastern time on screen.
- **Two-tier validation.** Rev 1 separates "allowed" from "normal" for pH, so we use the same reject/warn pattern
  everywhere it makes sense.
- **Technician name.** Entered once per session instead of on every batch. Every record still stores who created it (
  FR-22).

- **Demo data loads next to real batches.** The M1 demo hotfix (#58) is kept on purpose: `SeedSyntheticData` adds the 9 synthetic batches once, even if real batches exist, so demos work on a machine that's already in use. They stay marked synthetic and can be hidden with the Synthetic data switch.

## If something changes

If Vulcan tells us something different from what's here:

1. Amelia or Tyler updates this file in a pull request and quotes or links what the client said.
2. If a number changed, `schema.md` gets updated in the same pull request.
3. We post it in the team channel.
4. It goes in the next Friday status report, with Professor Murphy copied.
