# CBAS Calculation Method

**Concrete Batch Analysis & Strength Prediction (CBAS)**
**Purpose:** Engineering explanation of maturity, compressive-strength estimation, break-based re-fitting, warning thresholds, and calibration
**Requirements:** FR-13, FR-14, FR-15, FR-17, NFR-7
**Baseline:** Calibration `v1` | **Method:** Nurse–Saul maturity and ACI 209R-92 strength-development relationship
**Reference decision:** [ADR-005: Concrete Strength Estimation Method](adr/005-strength-estimation.md) (approved October 21, 2026)

> **Scope and interpretation.** CBAS produces deterministic engineering *estimates*, not certified cylinder-test results. Its initial model uses the batch's entered expected final compressive strength, measured specimen temperatures, and, when available, measured 3- and 7-day cylinder breaks. It does not train a machine-learning model or call an external prediction service. This document describes calibration v1 as implemented in CBAS. Source labels in configuration identify the team's documented rationale; they should not be read as claims that a referenced standard mandates each application-specific warning threshold.

## 1. Calculation overview

For each batch, CBAS:

1. Loads the current calibration version and selects the coefficients associated with the batch's cement type.
2. Checks which specimens qualify based on their internal-temperature reading coverage.
3. Integrates each qualifying specimen's internal curing temperatures to obtain Nurse–Saul maturity (°F-hours).
4. Converts each specimen's maturity to equivalent curing age (days) at the configured reference temperature.
5. Averages equivalent age **across qualifying specimens**, then applies the strength equation **once** to the batch average.
6. Calculates an original strength curve for chronological days 1–28 and original 3-, 7-, and 28-day estimates.
7. If recorded 3- and/or 7-day breaks are available, attempts a break-based re-fit, retaining original estimates for comparison.
8. Saves prediction values, calibration version, calculation basis, and adjustment stage when relevant entity changes are persisted; exposes calculated curves and stored estimates through the strength API.

### Principal inputs and units

| Input or intermediate value | Unit | Role |
|---|---|---|
| `Batch.CastAt` and reading timestamps | UTC | Define interval durations and prediction horizons |
| `CuringReading.InternalTempF` | °F | Used directly in maturity |
| Datum temperature, `T₀` | °F | Baseline for maturity accumulation |
| Reference temperature, `T_ref` | °F | Converts maturity to equivalent age |
| Maturity, `M` | °F-hours | Accumulated temperature-time factor |
| Equivalent age, `t_eq` | days | Strength-model time input |
| `Batch.CementType` | type | Selects coefficients `a`, `b` |
| `Batch.ExpectedFinalBreakPsi` | PSI | Original strength-reference input, `f_ref` |
| Measured 3-/7-day cylinder breaks | PSI | Re-fit data, averaged within break age |
| Estimated compressive strength | PSI | Result |

Ambient temperature, moisture, relative humidity, resistivity, CO₂, pH, oxides, aggregates, and SCM composition are **not direct inputs to the v1 strength formula**. They may be validated or evaluated as warnings. Water-to-cementitious ratio is used for a separate plausibility check of expected strength; it does not replace `ExpectedFinalBreakPsi` in the v1 formula.

## 2. Calibration v1

The seeded `calibration-v1.json` contains:

| Setting | Stored value |
|---|---|
| `Version` | `v1` |
| `Method` | `ACI 209R-92` |
| `DatumTemperatureF` | `14.0` (−10°C) |
| `ReferenceTemperatureF` | `73.4` (23°C) |
| `ParametersJson.Factors` | `{}` — no additional factors configured |

**Cement coefficients** (`ParametersJson.Base.CementTypeCalibrations`):

| Cement type | `a` | `b` | Status of source/assumption |
|---|---:|---:|---|
| Type I | 4.0 | 0.85 | Initial moist-curing coefficients documented in ADR-005; primary ACI reference verification remains noted in the ADR |
| Type II | 4.0 | 0.85 | Explicit v1 assumption: reuses Type I coefficients |
| Type III | 2.3 | 0.92 | Initial moist-curing coefficients documented in ADR-005; primary ACI reference verification remains noted in the ADR |

`StrengthSettings.FromCalibration(calibration, cementType)` reads these values and passes them into `StrengthEstimator`. `MaturitySettings` defaults to 14°F and 73.4°F but the actual strength-estimation path takes its temperature values from the selected stored calibration. Calibration parameters are not hard-coded inside `StrengthEstimator`.

### Introducing a future calibration version

1. Establish a new, uniquely identified calibration version (for example, `v2`) based on measured data and documented engineering review. Preserve the existing `v1` baseline for traceability.
2. Record the chosen maturity constants, cement-type coefficients, supporting dataset, method, limitations, and change rationale in the version's parameter record and accompanying documentation.
3. Run regression tests and backtest the proposed version against recorded breaks before activating it. Report unrounded accuracy values when assessing the 5% objective.
4. Add the new version as a `CalibrationVersion` row. It becomes current immediately: `ICalibrationProvider.Current()` returns the most recently created version, and both the strength API and the save interceptor use it. There is no separate activation step, so a new version must be backtested before it is added.
5. Confirm new persisted predictions are labeled with the selected calibration version, and compare displayed recalculated curves with stored estimates when switching versions.

Do not silently edit historical `v1` coefficients or imply that the empty `Factors` object currently adjusts v1 strength predictions.

## 3. Nurse–Saul maturity

### 3.1 Accumulated maturity

For each chronological interval between applicable internal-temperature readings:

$$
M=\sum_i\max(0,T_{\mathrm{avg},i}-T_0)\,\Delta t_i
$$

where `T_avg,i` is interval-average internal temperature in °F, `T₀ = 14°F`, and `Δtᵢ` is elapsed time in **hours**. Negative temperature contributions are clamped to zero.

When both endpoints are measured:

$$
T_{\mathrm{avg},i}=\frac{T_i+T_{i+1}}{2}
$$

**Illustration from VM-SYN-006, specimen D3-1:** the first two temperatures are 78.4°F at casting and 81.9°F 12 hours later. That interval contributes:

$$
T_{\mathrm{avg}}=(78.4+81.9)/2=80.15\ ^\circ\mathrm{F}
$$

$$
\Delta M=\max(0,80.15-14)\times12=793.8\ ^\circ\mathrm{F\!\cdot h}
$$

All intervals through the requested target are accumulated independently for each specimen.

### 3.2 Equivalent age

Equivalent age is the maturity expressed as an equivalent number of days at the reference temperature:

$$
t_{eq}=\frac{M}{(T_{ref}-T_0)\times24}
=\frac{M}{(73.4-14)\times24}
=\frac{M}{1425.6}
$$

This is a **linear Nurse–Saul equivalent-age approximation**, not Arrhenius equivalent-age kinetics. A chronological day can correspond to more or less than one equivalent day depending on curing temperatures.

### 3.3 Reading selection, interpolation, and extrapolation

- Readings before casting are excluded from the maturity integration. Readings are ordered by timestamp.
- If the first usable reading occurs after casting, its temperature is assumed constant from casting until that reading. If the target is even earlier than the first reading, that same first value is assumed through the target.
- Between two recorded temperatures, the interval uses their arithmetic mean.
- If a target falls **between** recorded readings, temperature at the target is linearly interpolated:

  $$
  T_{target}=T_{before}+(T_{after}-T_{before})\frac{t_{target}-t_{before}}{t_{after}-t_{before}}
  $$

  The final partial interval then uses the mean of `T_before` and `T_target`.
- If a target is **after the last available reading**, the unobserved interval uses the arithmetic mean of that specimen's readings at or before the target:

  $$
  T_{assumed}=\frac{1}{n}\sum_{j=1}^{n} T_j
  $$

  This is an assumption, **not a measured or forecast future curing temperature**. It applies separately to each specimen.
- Maturity through a target excludes elapsed time after that target, though a later reading may be used to interpolate a temperature at the target.

`MaturityCalculator` rejects explicit local `DateTime` kinds; UTC and unspecified kinds are accepted, with unspecified interpreted as UTC without shifting its clock value. It also rejects non-finite internal temperatures and settings, duplicate timestamps, invalid reference-versus-datum settings, targets earlier than casting, and input lacking any reading at or after casting. Zero-length intervals contribute zero maturity.

### 3.4 Specimen eligibility and batch aggregation

For **batch-level strength estimation**, a specimen qualifies if it has:

- At least **two** valid internal temperature readings at or after casting; and
- A latest reading timestamp at least **24 hours after casting**.

These are CBAS application rules, not an assertion of ASTM-mandated reading counts. Eligibility is evaluated using the readings supplied to the estimator at calculation time, then each qualifying specimen contributes to **every** prediction horizon. If none qualifies, the estimator returns `Not enough curing data yet` and no strength curve.

At each chronological day `d`:

$$
\overline{t}_{eq,d}=\frac{1}{N}\sum_{s=1}^{N}t_{eq,s,d}
$$

Strength is evaluated **once at the averaged equivalent age**. CBAS does **not** average specimen temperatures first, and it does **not** calculate separate strengths and then average them.

## 4. Original strength-development curve

The v1 equation, based on the ACI 209R-92 strength-development relationship, is:

$$
f_{est}(t)=\frac{t}{a+bt}\,f_{ref}
$$

where `t` is the batch's average equivalent age in days, `a` and `b` are coefficients for the selected cement type, and `f_ref = Batch.ExpectedFinalBreakPsi`.

CBAS evaluates this equation for **chronological days 1 through 28**, using the maturity-calculated equivalent age at each day. Its 3-, 7-, and 28-day horizons come from that same curve.

**Interpretation:** `ExpectedFinalBreakPsi` is a reference parameter, not a guaranteed result. The formula does not necessarily equal that reference value at exactly 28 equivalent days. In v1, mix composition does not independently predict ultimate strength.

## 5. Re-fitting after cylinder breaks

Measured break results are grouped by test age; within each age the arithmetic mean is used. Only 3- and 7-day results are used in the re-fitting methods below. **28-day breaks provide an observed comparison, not a third re-fit stage.** A specimen ID may appear only once in the break measurement input; supported ages are 3, 7, and 28 days and strengths must be positive finite values.

### 5.1 Three-day-only adjustment

Let `B₃` be mean measured 3-day strength, `P₃` be the original 3-day prediction, and `t₃` be batch equivalent age at chronological day 3. Holding `b` and `f_ref` fixed:

$$
a'=t_3\left(\frac{f_{ref}}{B_3}-b\right)
$$

Then:

$$
f_{adjusted}(t)=\frac{t}{a'+bt}f_{ref}
$$

The adjustment is accepted only when required values are positive and finite, `a' > 0`, and:

$$
0.70\le \frac{B_3}{P_3}\le1.30
$$

Thus a 3-day measured break more than 30% above or below its original estimate prevents this **single-break** adjustment. This is a CBAS safeguard, **not an ACI-mandated tolerance**.

### 5.2 Combined three- and seven-day adjustment

When both mean breaks are available, CBAS first attempts a two-point fit. Define measured means `B₃`, `B₇` and corresponding equivalent ages `t₃`, `t₇`:

$$
k=\frac{1/B_3-1/B_7}{1/t_3-1/t_7}
$$

$$
c=\frac{1}{B_3}-\frac{k}{t_3},\qquad F=\frac{b}{c},\qquad a'=kF
$$

Then:

$$
f_{adjusted}(t)=\frac{t}{a'+bt}\,F
$$

This method recalibrates **both** `a` and reference strength `F`; coefficient `b` stays fixed. It is a **team-derived algebraic re-fit of the strength equation**, not a re-fitting procedure specified by ACI 209R-92.

The two-point fit requires `t₇ > t₃ > 0`, positive measured breaks and `b`, and finite positive fitted `a'` and `F` (with `c > 0`). If it cannot be fitted, CBAS attempts the valid 3-day-only adjustment; if that also fails or is unavailable, CBAS retains the original equation. The 30% day-3 ratio rule is checked for the **single-break fallback**, not for an otherwise valid two-point fit.

### 5.3 Preserving the original prediction

The estimator returns its original daily curve and horizon estimates separately from an adjusted daily curve and adjustment records. Persisted predictions include `EstimatedPsi` (adjusted if applicable), `UnadjustedPsi`, `AdjustmentStage`, `EquivalentAgeDays`, `Basis`, and `CalibrationVersion`. Consumers should label which value is shown; the endpoint's `CurvePoints` uses an adjusted curve when available, while `UnadjustedCurvePoints` supplies the original for comparison.

## 6. Worked example: VM-SYN-006

**Dataset:** `data/synthetic/batches.json`; all results below were recalculated from its nine specimens using the `MaturityCalculator`, `MaturityIntervalBuilder`, `StrengthEstimator`, and `BreakAdjustmentCalculator` formulas. Values are shown with extra precision for auditability; the API rounds displayed curve PSI to the nearest whole PSI, with midpoint rounding away from zero.

### 6.1 Batch and input assumptions

| Field | Value |
|---|---|
| Batch | `VM-SYN-006` (synthetic) |
| Cast time | 2026-06-21 12:00:00 UTC |
| Cement | Type III |
| `ExpectedFinalBreakPsi` | 6,200 PSI |
| Calibration | `v1`, `a = 2.3`, `b = 0.92` |
| Maturity settings | `T₀ = 14°F`, `T_ref = 73.4°F` |
| Eligible specimens | 9: three day-3, three day-7, and three day-28 cylinders |
| Temperature records | 7 per day-3 specimen, 11 per day-7 specimen, 32 per day-28 specimen (150 total) |

**Important:** All nine qualifying specimens contribute to the batch equivalent age at all three horizons. For a 28-day target, the day-3 and day-7 specimens' missing later temperature intervals use the mean-temperature assumption; the day-28 specimens provide measurements through day 28. Consequently, these numbers describe a calculation made with the **complete synthetic record**, not a cast-day forecast or a historical day-3 snapshot.

### 6.2 Average maturity and original predictions

| Chronological age | Mean maturity (°F-hours) | Mean equivalent age (days) | Original estimated PSI |
|---|---:|---:|---:|
| Day 3 | 4,744.066667 | 3.327768 | 3,848.174 |
| Day 7 | 10,641.952381 | 7.464894 | 5,048.412 |
| Day 28 | 42,024.025108 | 29.478132 | 6,212.276 |

For day 28:

$$
f_{original,28}=\frac{29.478132}{2.3+0.92(29.478132)}\times6200
\approx\boxed{6212.276\ \mathrm{PSI}}
$$

**API display: 6,212 PSI.**

### 6.3 Recorded breaks and recalibration

| Break age | Specimen results (PSI) | Mean measured break (PSI) |
|---|---|---:|
| Day 3 | 3,148; 3,160; 3,078 | 3,128.666667 |
| Day 7 | 3,900; 4,070; 3,938 | 3,969.333333 |
| Day 28 | 5,150; 5,004; 4,828 | 4,994.000000 |

The 3-day measured-to-original ratio is approximately `3128.666667 / 3848.173750 = 0.813026`, inside the allowed 0.70–1.30 interval. With both 3- and 7-day results available, the application uses the **combined 3+7-day fit**:

$$
\begin{aligned}
k&=\frac{1/3128.666667-1/3969.333333}{1/3.327768-1/7.464894}
\approx0.000406466742\\
c&=\frac{1}{3128.666667}-\frac{k}{3.327768}
\approx0.000197481035\\
F&=\frac{0.92}{c}\approx4658.675202\ \mathrm{PSI}\\
a'&=kF\approx1.893596531
\end{aligned}
$$

The day-28 adjusted prediction is:

$$
f_{adjusted,28}=
\frac{29.478132}{1.893596531+0.92(29.478132)}\times4658.675202
\approx\boxed{4733.284\ \mathrm{PSI}}
$$

**API display: 4,733 PSI.** The fit adjusts both the curve shape and its reference strength. The 28-day measured break was **not** used in the fitting step.

### 6.4 Accuracy assessment

Accuracy is measured as the absolute percentage error against the measured 28-day break average. The estimate card shows the signed difference (for example, "261 PSI (5.2%) below the actual break"), and SCRUM-205 adds a pass or miss against the 5% target on the 28-day card:

$$
\mathrm{Error\ (\%)}=
\frac{|\mathrm{PredictedPSI}-\mathrm{ActualPSI}|}
{\mathrm{ActualPSI}}\times100
$$

| Comparison | Calculation | Error |
|---|---|---:|
| Original vs. actual | `abs(6212.275832 − 4994) / 4994 × 100` | **24.3948%** |
| Adjusted vs. actual | `abs(4733.284397 − 4994) / 4994 × 100` | **5.2206%** |

The combined re-fit substantially reduces the prediction error. **However, 5.2206% is outside the project's 5% accuracy objective.** A value must not be labeled as meeting the benchmark merely because of display rounding. The 5% prediction-accuracy objective is also separate from the **configured 5% projected-strength shortfall warning**, which compares predicted strength against the batch's expected final strength instead of a measured break.

## 7. Validation and warning thresholds

The following values are from `src/Vulcan.Infrastructure/Seed/validation-limits.json`. `Warn` marks a value for review; `Reject` is an input-acceptance constraint. Bounds are reproduced as configured. An absent bound means no limit on that side in this entry.

> **How these become early warnings:** `EarlyWarningService` emits the three prediction warnings in 7.1 (`Prediction.MinimumPsi`, `Prediction.ShortfallPct`, `Batch.ExpectedFinalVsWcmPct`), the two early break warnings in 7.1 (`Break.Day3WarnPct`, `Break.Day7FailPct`, from the average break at that age), plus each input warning saved at entry (7.2), grouped as one reason per limit key (client-decisions §13). The `Source` text records where each value came from; it does not claim the cited standard prescribes that exact threshold.

### 7.1 Prediction and mix-related warnings

| Configuration key | Warning condition / normal range | Configured source |
|---|---|---|
| `Prediction.MinimumPsi` | Projected strength **below 3,000 PSI** | ACI 211.1-97 Table 6.3.4(a); ADR-005 |
| `Prediction.ShortfallPct` | Projected 28-day strength **more than 5% below `ExpectedFinalBreakPsi`** | ACI 211.1-97 Table 6.3.4(a); ADR-005 |
| `Batch.ExpectedFinalVsWcmPct` | Expected final strength **more than 30% above** the w/cm table estimate | ACI 211.1-97 Table 6.3.4(a); ADR-005 |
| `Break.Day3WarnPct` | Average 3-day break **below 40% of 3,000 PSI** (1,200 PSI) | Client answers, Nov 2, 2026 (client-decisions §14.1) |
| `Break.Day7FailPct` | Average 7-day break **below 70% of 3,000 PSI** (2,100 PSI); the batch will probably fail | Client answers, Nov 2, 2026 (client-decisions §14.1) |
| `Composition.WaterCementRatio` | Outside 0.40–0.60 | Client hydration document |

For the w/cm check, ADR-005 specifies this **non-air-entrained** ACI 211.1-97 Table 6.3.4(a) lookup with linear interpolation:

| Water-to-cementitious ratio (by weight) | Approximate 28-day PSI |
|---:|---:|
| 0.33 | 7,000 |
| 0.41 | 6,000 |
| 0.48 | 5,000 |
| 0.57 | 4,000 |
| 0.68 | 3,000 |
| 0.82 | 2,000 |

ADR-005 specifies that ratios below 0.33 skip this plausibility check, and ratios above 0.82 use 2,000 PSI. The 30% comparison produces a warning **without changing the strength formula's entered reference PSI**. The configured threshold is a CBAS decision; do not present 30% as a universal ACI acceptance rule. **Source in ADR-005:** [ACI webinar material reproducing the table](https://www.concrete.org/portals/0/files/pdf/webinars/Folks.pdf).

### 7.2 Additional configured warnings

| Configuration key | Normal range / warning trigger | Configured source |
|---|---|---|
| `Batch.CementSurfacePh` | pH outside 7.1–8.0 | Client, M1 demo (Oct. 9) |
| `Batch.FinalWeightLb` | Weight outside 20–30 lb for 12 × 6 in. cylinder | Client, M1 demo (Oct. 9) |
| `Batch.MixWaterPh` | pH outside 7.0–7.5 | Client document (“normal”) |
| `Oxide.AluminaPct` | Outside 3.0–7.5% | Client document table |
| `Oxide.IronOxidePct` | Outside 0.5–6.0% | Client document table |
| `Oxide.MagnesiumOxidePct` | Outside 0.1–4.0% | Client document table |
| `Oxide.SulfurTrioxidePct` | Outside 1.3–3.0% | Client document table |
| `Break.StrengthPsi` | Below 3,000 PSI (warn, save for review) | Client document / FR-5 |
| `Curing.AmbientTempF` | Outside 60–80°F | ASTM C31 initial curing (source label in configuration) |
| `Curing.MoisturePct` | Outside **2–4% of sample weight** | **Don, Nov. 2, 2026** |

No separate `Warn` range is configured for internal temperature, relative humidity, resistivity, or CO₂. Those fields do have input-rejection constraints below. The moisture acceptance bound is explicitly **provisional** pending client confirmation, whereas the 2–4% normal range is attributed to Don.

### 7.3 Input-rejection limits (not strength-model coefficients)

| Key | Configured accepted bound | Source / qualification |
|---|---|---|
| `Batch.CementSurfacePh` | 0–14 | Client, M1 demo |
| `Batch.FinalWeightLb` | 10–40 lb | Client, M1 demo |
| `Batch.ExpectedFinalBreakPsi` | 3,000–10,000 PSI | Client document / FR-5 |
| `Batch.MixWaterPh` | 1–9 | Client document / FR-7 |
| `Composition.CementPct` | 10–15% | Client document / FR-2.1 |
| `Composition.ScmPct` | 0–100% | FR-2 |
| `Composition.FineAggregatePct` | 25–30% | Client document / FR-2.1 |
| `Composition.CoarseAggregatePct` | 40–55% | Client document / FR-2.1 |
| `Composition.WaterPct` | 15–20% | Client document / FR-2.1 |
| `Composition.TotalPct` | Maximum 100%; no minimum configured | Decision 2 (NRMCA) |
| `Oxide.AluminaPct` | 0–100% | FR-3 |
| `Oxide.IronOxidePct` | 0–100% | FR-3 |
| `Oxide.MagnesiumOxidePct` | 0–6.0% | ASTM C150 (configuration source) |
| `Oxide.SulfurTrioxidePct` | 0–4.5% | ASTM C150 Table 1 (configuration source) |
| `Specimen.LengthIn` | 0.01–12 in. | FR-4 |
| `Specimen.WidthIn` | 0.01–6 in. | FR-4 |
| `Break.StrengthPsi` | 1–10,000 PSI | Client document / FR-5 |
| `Curing.InternalTempF` | 0–200°F | Plausibility (heat of hydration) |
| `Curing.AmbientTempF` | 0–150°F | Plausibility |
| `Curing.MoisturePct` | 0–15% | **M3-02 provisional range; client confirmation pending** |
| `Curing.RelativeHumidityPct` | 0–100% | FR-6 |
| `Curing.ResistivityOhmPerFt` | Minimum 0.01 Ω/ft; no maximum configured | FR-6 |
| `Curing.Co2Ppm` | Minimum 0 ppm; no maximum configured | FR-6 |

Input-rejection and normal-range warnings have different purposes. For example, an ambient temperature may fall within the plausible 0–150°F acceptance range while still being outside the 60–80°F warning range. Configured mix composition percentages use the application's composition units and **must not be mistaken for weights** when calculating water-to-cementitious ratio.

## 8. Prediction persistence, API outputs, and traceability

### 8.1 Saved predictions

`PredictionSaveChangesInterceptor` runs for synchronous and asynchronous EF Core save operations. On **added or modified** curing readings or breaks, and newly added batches or specimens, it identifies affected batches, merges pending and existing inputs, obtains the current calibration, and calls the estimator. It persists or updates three prediction rows (horizons 3, 7, 28), including nullable results if there is insufficient data.

Each saved prediction records horizon, adjusted/current estimate, original estimate, equivalent age, basis, adjustment stage, calibration version, and synthetic flag. Existing rows for each batch/horizon are **updated**, not appended to a time-series history. Deleting curing readings and break results is explicitly outside the interceptor's documented change-handling scope. If an estimation attempt fails, the interceptor logs a warning and does not refresh the stored predictions.

### 8.2 Strength API response

`StrengthService.GetByBatchNumberAsync()` loads all batch specimens and their internal readings, all available breaks, current calibration, and stored predictions. It recalculates the currently displayed day-1–28 curve and returns, among other values:

| Field | Source / interpretation |
|---|---|
| `CurvePoints` | Freshly calculated curve, adjusted if a valid break fit applies |
| `UnadjustedCurvePoints` | Original freshly calculated curve when adjustments apply |
| `Estimates` | Stored 3-, 7-, and 28-day prediction records |
| `MeasuredBreaks` | Recorded cylinder break measurements |
| `CuringPoints` | Specimen-level sensor readings and maturity at reading times |
| `EarlyWarnings` / risk fields | Early warning evaluator when configured; otherwise limited fallback values |

The **fresh curve** and **stored estimate cards** need not originate from the same computation event. Both currently request `ICalibrationProvider.Current()` when recalculating, but stored rows retain the version that was used at their last successful update. After switching versions or on failed recalculation, the two views can differ. The strength API call supplies all available breaks to its live curve, not a historical as-of-date break filter; a historical forecast should not be described as temporally isolated unless its inputs have been explicitly filtered elsewhere.

### 8.3 Calculation explanation shown to users

A “How this was calculated” view or link should communicate, at minimum:

- Formula family and selected calibration version (`v1`, Nurse–Saul, ACI 209R-92).
- Batch cement type and coefficient values `a` and `b`.
- Number of qualifying specimens and span of their last recorded curing readings.
- That equivalent ages are averaged across specimens, and future temperature averages are assumed when readings end before the prediction target.
- Expected final reference PSI, unadjusted estimate, adjusted estimate if applicable, and stage of break re-fit.
- Whether presented values came from the latest **stored prediction** or a freshly **recalculated curve**.
- The distinction between predicted strength, early-warning flags, and measured laboratory breaks.

## 9. Assumptions and limitations

1. **Empirical model:** ACI 209R-92 coefficients are an initial engineering approximation. Type II borrows Type I coefficients; the ADR records that the primary ACI source values for Type I/III still warrant verification.
2. **Reference dependence:** The initial predicted curve is anchored to user-entered expected final PSI, not an independently computed ultimate strength.
3. **Temperature assumption:** Future temperature defaults to an average of prior recorded values, and temperature at casting may be inferred from the earliest later reading; neither assumption guarantees actual curing behavior.
4. **Coverage:** Specimens with fewer than two readings or less than 24 hours of coverage are excluded; a qualifying specimen contributes across horizons using recorded or assumed temperatures.
5. **Re-fit:** The 3-day 30% safeguard and two-point re-fit are CBAS-specific modeling choices. A valid combined fit can depart substantially from the original reference strength.
6. **Not a historical snapshot:** Inputs supplied to the estimator determine the information available. The current strength API loads all available break measurements and curing records.
7. **Validation versus prediction:** Warning ranges do not by themselves alter the v1 strength equation; early warning factor implementation should be verified separately before claiming each is emitted in the UI.
8. **Accuracy:** The 5% objective is a validation target, not an assurance. For VM-SYN-006, the verified adjusted error is 5.2206%, above target.
9. **Persistence:** Relevant additions and modifications trigger re-estimation on save; deletion behavior and estimation-failure recovery are limited as described above.
10. **Offline/deterministic:** The documented calculation does not depend on cloud services, ML, randomness, or an external inference API.

## 10. Source and implementation references

**Implementation references**

- [ADR-005: Concrete Strength Estimation Method](adr/005-strength-estimation.md) — approved October 21, 2026.
- `calibration-v1.json` — Stored calibration version, coefficient values, and maturity settings.
- `validation-limits.json` — Warning/rejection limits and their source labels.
- Synthetic batch dataset containing `VM-SYN-006` — Per-specimen readings and 3-, 7-, 28-day breaks used in Section 6.
- `Vulcan.Prediction.Maturity.MaturityCalculator`, `MaturityIntervalBuilder`, and `MaturitySettings` — Maturity and equivalent-age behavior.
- `Vulcan.Prediction.Strength.StrengthEstimator` and `StrengthSettings` — Eligibility, aggregation, strength curve, versioned parameter loading.
- `Vulcan.Prediction.Adjustments.BreakAdjustmentCalculator`, `BreakMeasurementAggregator`, and `BreakAdjustmentStage` — Re-fit and fallback logic.
- `Vulcan.Application.Services.StrengthEstimateService` — Estimator adapter.
- `Vulcan.Application.Services.StrengthService` — Batch-data assembly and strength API response.
- `Vulcan.Infrastructure.Interceptors.PredictionSaveChangesInterceptor` — Automatic stored-prediction recomputation and updates.

**Engineering references documented in ADR-005**

- ASTM C1074 — maturity concept and Nurse–Saul temperature-time factor.
- ACI 209R-92 — empirical strength-development equation and initial coefficient family (see ADR-005's primary-source verification caveat).
- NDOT Materials & Research Division, *Maturity Curve Method of Development-Certification*, §5.5.2, Equation 1 — client-supplied rationale for −10°C (14°F) maturity datum.
- ACI 211.1-97 Table 6.3.4(a) — approximate strength versus water-to-cementitious ratio, reproduced in the [ACI webinar material](https://www.concrete.org/portals/0/files/pdf/webinars/Folks.pdf).
- Client documentation, M1 demo decisions, FR references, and Don's Nov. 2, 2026 moisture guidance — configuration provenance as recorded in `validation-limits.json`.

