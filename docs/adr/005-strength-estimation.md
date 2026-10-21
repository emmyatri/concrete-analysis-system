
# ADR-005: Concrete Strength Estimation Method

- **Status:** Approved October 20, 2026 — Revision Pending Approval October 21, 2026
- **Decision:** Deterministic concrete maturity and compressive strength estimation
- **Requirements:** FR-13, FR-14, FR-15, FR-17, NFR-5, NFR-7, SLA 1.2
- **References:** ASTM C1074, ACI 209R-92

---

## 1. Context

Vulcan CBAS requires a deterministic mathematical method for estimating concrete compressive strength at 3, 7, and 28 days. These estimates will be used to generate a strength-development curve and compared against actual cylinder break results as they become available.

The initial implementation will combine two established engineering approaches:

- **ASTM C1074:** Nurse–Saul maturity calculations to account for curing temperature over time.
- **ACI 209R-92:** An empirical strength-development equation to estimate compressive strength as concrete ages.

The model will use the batch's `ExpectedFinalBreakPsi` as its reference 28-day compressive strength.

The estimation method must operate locally without machine learning, external services, or cloud connectivity.

---

## 2. Decision

The system will implement a two-stage deterministic strength estimation model:

1. Calculate the concrete's equivalent age using the Nurse–Saul maturity method.
2. Estimate compressive strength at that equivalent age using the ACI 209R-92 strength-development equation.

The calculations will use predefined constants and the curing temperature readings recorded for each specimen.

The initial implementation will follow the published ACI equation without composition-based adjustments. Once breaks are recorded, the equation is re-fit to them (4.4).

---

## 3. Concrete Maturity — ASTM C1074

### 3.1 Nurse–Saul Temperature-Time Factor

The system will calculate accumulated maturity using:

### Equation 1 — Accumulated Maturity

```text
M = Σ [max(0, T_avg,i - T_0) × Δt_i]
```

Where:

- `M`: Accumulated maturity in °F-hours.
- `T_avg,i`: Average internal concrete temperature during interval i, in °F.
- `T_0`: Datum temperature in °F.
- `Δt_i`: Duration of interval i, in hours.

**Initial default constants:**

| Parameter | Value |
|---|---|
| Datum temperature (T_0) | 14°F (-10°C) |
| Reference temperature (T_ref) | 73.4°F (23°C) |
| Temperature unit | °F |
| Time interval unit | Hours |
| Minimum maturity contribution | 0 |

The 14°F (−10°C) datum is the updated default based on the Nebraska DOT documentation supplied by Vulcan Materials. The appropriate datum temperature may vary by concrete mixture and can be reviewed during Milestone 3 calibration.

### 3.2 Average Temperature Between Readings

Curing readings will be ordered chronologically. Readings taken before `Batch.CastAt` are ignored when calculating maturity.

For each pair of consecutive applicable readings, the system will calculate the average internal temperature.

For the period between casting and the first applicable curing reading, the system will assume the internal temperature remained constant at the first recorded value.

### Equation 2 — Average Interval Temperature

```text
T_avg,i = (T_i + T_(i+1)) / 2
```

The duration of each interval will be calculated from the timestamps of the two readings. Each interval's maturity contribution will be calculated using Equation 1.

**Maturity calculator input validation (SCRUM-160):**

- Casting, target, and reading timestamps must be UTC or `DateTimeKind.Unspecified`. Unspecified timestamps are interpreted as UTC without changing their clock values, to support timestamps retrieved through SQLite/EF Core. Explicitly local timestamps are rejected.
- Internal temperatures and maturity settings must be finite numeric values.
- Duplicate reading timestamps are rejected, rather than silently merged or overwritten.
- The target timestamp must not precede casting.
- At least one valid reading must exist at or after casting. The first reading may occur after the target timestamp; in that case, its temperature is assumed constant from casting through the target.
- The reference temperature must be greater than the datum temperature.

Zero-duration intervals contribute no maturity. Readings are ordered before intervals are constructed, preventing negative-duration intervals.

These calculator-level validation rules are separate from the stricter batch-level specimen eligibility requirements in Section 5.1.

### 3.3 Equivalent Age

Accumulated maturity will be converted into equivalent age at the selected reference temperature.

### Equation 3 — Equivalent Age

```text
t_eq = M / [(T_ref - T_0) × 24]
```

Where:

- `t_eq`: Equivalent age in days.
- `T_ref`: Reference temperature, 73.4°F.
- `T_0`: Datum temperature, 14°F.
- `M`: Accumulated maturity in °F-hours.

Using the default constants:

```text
t_eq = M / [(73.4 - 14) × 24]

t_eq = M / 1425.6
```

This represents a Nurse–Saul-based linear equivalent-age approximation rather than an Arrhenius equivalent-age calculation.

---

## 4. Compressive Strength Development — ACI 209R-92

### 4.1 Strength Development Equation

The system will use the ACI 209R-92 strength-development relationship.

### Equation 4 — Predicted Compressive Strength

```text
PredictedPSI(t_eq) = [t_eq / (a + b × t_eq)] × f_c28
```

Where:

- `PredictedPSI(t_eq)`: Estimated compressive strength in PSI.
- `t_eq`: Equivalent age in days.
- `f_c28`: Reference 28-day compressive strength, obtained from `ExpectedFinalBreakPsi`.
- `a, b`: Empirical strength-development coefficients.

The system will substitute equivalent age for chronological age to account for the effects of curing temperature.

### 4.2 Initial Cement Coefficients

| Cement type | a | b |
|---|---:|---:|
| Type I | 4.0 | 0.85 |
| Type II | 4.0 | 0.85 |
| Type III | 2.3 | 0.92 |

Type I and Type III coefficients are based on commonly published ACI 209R-92 moist-curing values. To be verified against ACI 209R-92.

Type II will initially use Type I coefficients as an application-specific assumption.

These values will remain fixed for the initial implementation and may be reviewed during Milestone 3 calibration.

### 4.3 Reference Strength

The system will use the batch's `ExpectedFinalBreakPsi` as the reference strength for all calculations.

The original ACI equation does not necessarily return exactly the reference strength at 28 equivalent days.

This behavior will be retained to remain consistent with the selected empirical equation.

The initial implementation will not independently estimate ultimate compressive strength from mix composition or cement chemistry.

### 4.4 Adjusting With Recorded Breaks

Once break results are recorded, the strength equation is re-fit so the curve passes through them. `b` stays at its cement-type value.

- **3-day breaks only:** `a` is re-fit through the average 3-day break (B₃) at the batch's 3-day equivalent age (t₃), keeping `ExpectedFinalBreakPsi` as the reference strength.
- **3- and 7-day breaks:** `a` and the reference strength F are both re-fit through the average 3- and 7-day breaks.

### Equation 4a — 3-Day Re-fit

```text
a' = t_3 × (f_c28 / B_3 − b)
```

### Equation 4b — 3- and 7-Day Re-fit

```text
k  = (1/B_3 − 1/B_7) / (1/t_3 − 1/t_7)
c  = 1/B_3 − k/t_3
F  = b / c
a' = k × F
PredictedPSI(t_eq) = [t_eq / (a' + b × t_eq)] × F
```

The 3-day re-fit is skipped if B₃ differs from the 3-day estimate by more than 30%, or if a' ≤ 0. The 3- and 7-day re-fit falls back to the 3-day re-fit if F ≤ 0 or a' ≤ 0. Each estimate records which adjustment was used.

This adjustment is the team's own method, derived from Equation 4. It is not part of ACI 209R-92, and it will be checked against real breaks in Milestone 3.

Adjustments will also be skipped or reverted when inputs are invalid, denominators are zero or numerically unstable, or calculated parameters are non-finite or nonpositive.

### 4.5 Checking Expected Final Against Water-Cement Ratio

The batch's water-cementitious ratio (by weight, including SCM) gives an approximate expected 28-day strength from ACI 211.1-97, Table 6.3.4(a), for non-air-entrained concrete, interpolated linearly:

| w/cm | 28-day PSI |
|---:|---:|
| 0.33 | 7,000 |
| 0.41 | 6,000 |
| 0.48 | 5,000 |
| 0.57 | 4,000 |
| 0.68 | 3,000 |
| 0.82 | 2,000 |

If `ExpectedFinalBreakPsi` is more than 30% above the table value, the batch gets an early warning that its target may be too high for its water content. The estimate still uses `ExpectedFinalBreakPsi`.

Below 0.33, the check is skipped. Above 0.82, the 2,000 PSI value is used, so a very high water content still triggers the warning.

**Reference:** ACI 211.1-97, Table 6.3.4(a), *Relationship Between Water-Cementitious Ratio and Compressive Strength of Concrete*.

**Source:** Values as reproduced in [American Concrete Institute webinar material](https://www.concrete.org/portals/0/files/pdf/webinars/Folks.pdf).

---

## 5. Curing Readings and Prediction Assumptions

### 5.1 Sufficient Curing Readings

Maturity is calculated independently for each specimen using its own internal temperature readings.

A specimen qualifies for batch-level strength estimation when:

- At least two valid internal temperature readings are available.
- The latest valid reading is at least 24 hours after `Batch.CastAt`.

These thresholds are application-specific requirements rather than ASTM-mandated minimums.

Eligibility is checked once, using all readings available when the prediction is generated. If no specimen qualifies, the batch has insufficient curing data and no estimates are produced. A qualifying specimen contributes to every target age. The maturity calculator uses the available readings to determine temperatures at each target timestamp according to Section 5.2.

### 5.2 Future Temperature Assumption and Target Interpolation

Strength predictions may extend beyond the most recent recorded curing temperature. The maturity calculator uses one deterministic calculation method for all target timestamps.

**When a reading exists after the target timestamp:**

If the target falls between two recorded readings, the calculator linearly interpolates the internal temperature at the target using the last reading before the target and the first reading after it.

Only maturity accumulated from casting through the target is included. The reading after the target is used solely to determine the interpolated endpoint temperature.

### Equation 5 — Linear Temperature Interpolation

```text
T_target = T_before + [(T_after - T_before) × (t_target - t_before) / (t_after - t_before)]
```

Where:

- `T_target`: Interpolated internal temperature at the target timestamp, in °F.
- `T_before`: Last recorded internal temperature before the target, in °F.
- `T_after`: First recorded internal temperature after the target, in °F.
- `t_target`: Target timestamp.
- `t_before`: Timestamp of the last reading before the target.
- `t_after`: Timestamp of the first reading after the target.

The interpolated target temperature and the last recorded temperature are averaged to calculate the final interval's maturity contribution.

**When no reading exists after the target timestamp:**

If the target is later than the last available reading, the calculator uses the arithmetic average of all applicable recorded internal temperatures as the assumed temperature for the remaining interval.

### Equation 5a — Average Recorded Temperature

```text
T_future = (T_1 + T_2 + ... + T_n) / n
```

Where:

- `T_future`: Assumed internal temperature for the unobserved interval, in °F.
- `T_1 ... T_n`: Valid specimen readings from casting through the target timestamp, inclusive.
- `n`: Number of applicable readings.

**When the first reading occurs after the target timestamp:**

The calculator assumes the first recorded internal temperature remained constant from casting through the target timestamp.

This allows maturity to be calculated for an earlier target even when no readings were recorded within that period.

The future-temperature assumption is a deterministic approximation, not a physical forecast of future curing conditions.

The calculator does not expose separate calculation modes. Callers requiring a historical calculation that excludes future observations must filter those readings before invoking the calculator.

### 5.3 Prediction Horizons and Batch-Level Aggregation

The method can estimate compressive strength at any age after casting.

For each target age:

1. Determine the target timestamp relative to casting.
2. Identify specimens that meet the minimum curing reading requirements in Section 5.1.
3. Calculate maturity and equivalent age independently for each qualifying specimen.
4. Accumulate maturity only from casting through the target timestamp. Ignore readings before casting.
5. When the target falls between recorded readings, interpolate the target temperature using the surrounding readings.
6. When the target extends beyond the last available reading, apply the average-temperature assumption in Section 5.2.
7. When the target precedes the first valid reading, use the first recorded temperature from casting through the target.
8. Calculate the arithmetic average of the qualifying specimens' equivalent ages.
9. Apply the ACI strength-development equation (Equation 4) once using the batch's average equivalent age and `ExpectedFinalBreakPsi`.

### Equation 6 — Batch Equivalent Age

```text
BatchEquivalentAge = Sum(SpecimenEquivalentAge) / QualifyingSpecimenCount
```

Specimen temperature readings will not be combined before maturity calculations, since readings may occur at different times.

The system will report estimated compressive strength at the following chronological ages:

| Horizon | Age after casting |
|---|---|
| Early strength | 3 days |
| Intermediate strength | 7 days |
| Final strength | 28 days |

The strength-development curve will use the same calculation method to estimate compressive strength at each whole day from day 1 through day 28.

All predictions will be expressed in PSI.

### 5.4 Engine Interface

The estimator takes the batch's cast time, cement type, `ExpectedFinalBreakPsi`, its recorded breaks, and each specimen's internal temperature readings (time and °F). Constants come from the current calibration version.

It returns either:

- **Insufficient curing data**, with no estimates, or
- for each whole day from 1 to 28: the batch equivalent age (days) and estimated PSI, and for the 3-, 7- and 28-day horizons also the basis text, calibration version, adjustment used (4.4) and the unadjusted PSI.

---

## 6. Deterministic Behavior and Units

### 6.1 Deterministic Calculations — SLA 1.2

The estimation method is deterministic because identical inputs, constants, and curing temperature histories will produce identical results.

The implementation will not use:

- Machine learning or AI models.
- Randomized calculations.
- External prediction services.
- Cloud-based processing.
- Uncontrolled or nondeterministic coefficient adjustments.

All calculations will be performed locally within the existing application architecture.

### 6.2 Units — NFR-5

The application will use the following canonical units:

| Quantity | Unit |
|---|---|
| Internal temperature | °F |
| Datum temperature | °F |
| Reference temperature | °F |
| Time interval | Hours |
| Equivalent age | Days |
| Accumulated maturity | °F-hours |
| Compressive strength | PSI |

All temperature calculations will use Fahrenheit.

Intermediate calculations will retain sufficient numerical precision to avoid unnecessary rounding errors.

---

## 7. Limitations and Milestone 3 Calibration — FR-17

### 7.1 Model Limitations

The initial model has several known limitations:

- The model depends on the entered `ExpectedFinalBreakPsi` rather than independently calculating ultimate strength from mix composition.
- Cement chemistry, SCM composition, and aggregate characteristics do not directly modify the initial strength equation.
- Future curing temperatures are assumed from recorded temperature averages when no later reading is available.
- The selected ACI coefficients may not accurately represent every concrete mixture.
- Nurse–Saul maturity uses a linear temperature-rate approximation.
- When the first curing temperature is recorded after casting, the model assumes that temperature remained constant between casting and the first reading. This may not capture temperature changes during early hydration and can affect early-age strength estimates.

These limitations will be considered when evaluating prediction accuracy.

### 7.2 Prediction Accuracy

As actual cylinder break results become available, predicted and measured compressive strengths may be compared.

### Equation 7 — Percentage Prediction Error

```text
Error (%) = [|PredictedPSI - ActualPSI| / ActualPSI] × 100
```

The calculation requires an actual break strength greater than zero.

The project's 5% accuracy objective will be evaluated against available laboratory results.

The initial mathematical model does not guarantee this level of accuracy without calibration and validation.

### 7.3 Milestone 3 Calibration

Milestone 3 will provide an opportunity to evaluate and refine the initial mathematical model.

Potential activities include:

- Comparing predicted and actual 3-, 7-, and 28-day compressive strengths.
- Reviewing the selected datum temperature.
- Evaluating the accuracy of the ACI strength-development coefficients.
- Assessing the impact of curing temperature assumptions.
- Adjusting empirical constants where supported by experimental evidence.

Any changes to the mathematical method will remain deterministic and will be documented.

### 7.4 Other Datapoints

Water-cement ratio has a published relationship to strength: ACI 211.1 relates water-cementitious ratio to expected 28-day strength. The System uses it to check `ExpectedFinalBreakPsi` (4.5), not to replace it, because the batch's Expected final is closer to actual results when it is reasonable.

No published method converts the remaining datapoints into strength: SCM, aggregates, oxides, mix water and surface pH, ambient temperature, moisture, relative humidity, resistivity and CO₂. The System does not estimate their effect without data.

- **Milestone 2:** these datapoints feed the early warning (SCRUM-180), using the warning ranges in the validation limits.
- **Milestone 3:** each datapoint's effect on the break, including water-cement ratio, is fitted from real batches and stored as a calibration version (FR-17). The fit remains deterministic.

---

## 8. Consequences

### Benefits

- Provides a deterministic and reproducible estimation method.
- Uses established concrete engineering relationships.
- Supports strength predictions at 3, 7, and 28 days.
- Incorporates recorded curing temperatures.
- Operates offline within the existing application.
- Provides a mathematical foundation for the strength-development chart.
- Allows future calibration without introducing AI or machine learning.

### Tradeoffs

- Predictions depend on the entered reference strength.
- Curing data must meet the minimum reading requirements.
- Future temperatures are assumed rather than measured when extrapolation is necessary.
- The model does not independently account for every recorded mixture characteristic.
- Accuracy must be evaluated against actual laboratory results.

---

## 9. Approval

This ADR establishes the mathematical method, default constants, and assumptions for the initial deterministic strength estimation engine.

The underlying method was approved on October 20, 2026. SCRUM-160 implements the maturity calculation component using a single deterministic calculation method, including target-time interpolation and the documented future-temperature assumption.

- **Backend Lead:** Approved — Tyler Schaefer, Oct 20, 2026
- **Frontend Lead:** Approved — Amelia Black, Oct 20, 2026
- **Original Status:** Approved — Oct 20, 2026
- **M2-15 Revision:** Updated Nurse–Saul datum from 32°F to 14°F — Oct 21, 2026
- **Revision Status:** Pending Approval
