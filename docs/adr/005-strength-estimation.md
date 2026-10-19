
# ADR-005: Concrete Strength Estimation Method

**Status:** Proposed — Pending Lead Approval
**Decision:** Deterministic concrete maturity and compressive strength estimation
**Requirements:** SLA 1.2, FR-17, NFR-5
**References:** ASTM C1074, ACI 209R-92

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

The initial implementation will follow the published ACI equation without additional normalization or composition-based adjustments.

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
| Datum temperature (T_0) | 32°F (0°C) |
| Reference temperature (T_ref) | 73.4°F (23°C) |
| Temperature unit | °F |
| Time interval unit | Hours |
| Minimum maturity contribution | 0 |

The 32°F datum is a provisional default. The appropriate datum temperature may vary by concrete mixture and can be reviewed during Milestone 3 calibration.

### 3.2 Average Temperature Between Readings

Curing readings will be ordered chronologically.

For each pair of consecutive readings, the system will calculate the average internal temperature.

For the period between casting and the first recorded curing reading, the system will assume the internal temperature remained constant at the first recorded value.

### Equation 2 — Average Interval Temperature

```text
T_avg,i = (T_i + T_(i+1)) / 2
```

The duration of each interval will be calculated from the timestamps of the two readings.

Each interval's maturity contribution will be calculated using Equation 1.

Intervals with zero or negative elapsed time will not contribute to accumulated maturity.

### 3.3 Equivalent Age

Accumulated maturity will be converted into equivalent age at the selected reference temperature.

### Equation 3 — Equivalent Age

```text
t_eq = M / [(T_ref - T_0) × 24]
```

Where:

- `t_eq`: Equivalent age in days.
- `T_ref`: Reference temperature, 73.4°F.
- `T_0`: Datum temperature, 32°F.
- `M`: Accumulated maturity in °F-hours.

Using the default constants:

```text
t_eq = M / [(73.4 - 32) × 24]

t_eq = M / 993.6
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

Type I and Type III coefficients are based on commonly published ACI 209R-92 moist-curing values.

Type II will initially use Type I coefficients as an application-specific assumption.

These values will remain fixed for the initial implementation and may be reviewed during Milestone 3 calibration.

### 4.3 Reference Strength

The system will use the batch's `ExpectedFinalBreakPsi` as the reference strength for all calculations.

The original ACI equation does not necessarily return exactly the reference strength at 28 equivalent days.

This behavior will be retained to remain consistent with the selected empirical equation.

The initial implementation will not independently estimate ultimate compressive strength from mix composition or cement chemistry.

---

## 5. Curing Readings and Prediction Assumptions

### 5.1 Sufficient Curing Readings

A specimen must have sufficient curing data before a temperature-adjusted strength estimate can be calculated.

The minimum requirements are:

- At least two valid internal temperature readings.
- The readings must span at least 24 hours after casting.
- The specimen's casting timestamp must be available.

These thresholds are application-specific requirements rather than ASTM-mandated minimums.

If sufficient readings are unavailable, the system will report that the specimen has insufficient curing data for a strength estimate.

### 5.2 Future Temperature Assumption

Strength predictions may extend beyond the most recent recorded curing temperature.

For the period between the last recorded reading and the requested prediction horizon, the system will assume a constant internal temperature equal to the arithmetic average of all valid internal temperature readings recorded so far.

### Equation 5 — Average Recorded Temperature

```text
T_future = (T_1 + T_2 + ... + T_n) / n
```

Where:

- `T_future`: Assumed future internal temperature in °F.
- `T_1 ... T_n`: Valid internal temperature readings.
- `n`: Number of valid readings.

The assumed temperature will be used to calculate additional maturity through the requested prediction horizon.

This is a deterministic approximation and does not represent an actual forecast of future curing conditions.

### 5.3 Prediction Horizons

The system will calculate estimated compressive strength at the following chronological ages:

| Horizon | Age after casting |
|---|---|
| Early strength | 3 days |
| Intermediate strength | 7 days |
| Final strength | 28 days |

For each horizon, the system will:

1. Determine the target timestamp from the casting date and time.
2. Calculate maturity from available curing readings.
3. Apply the future temperature assumption where necessary.
4. Convert accumulated maturity to equivalent age.
5. Calculate estimated compressive strength using Equation 4.

Predictions will be expressed in PSI.

---

## 6. Deterministic Behavior and Units

### 6.1 Deterministic Calculations — SLA 1.2

The estimation method is deterministic because identical inputs, constants, and curing temperature histories will produce identical results.

The implementation will not use:

- Machine learning or AI models.
- Randomized calculations.
- External prediction services.
- Cloud-based processing.
- Automatically adjusted coefficients.

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
- Future curing temperatures are assumed from recorded temperature averages.
- The selected ACI coefficients may not accurately represent every concrete mixture.
- Nurse–Saul maturity uses a linear temperature-rate approximation.

These limitations will be considered when evaluating prediction accuracy.

### 7.2 Prediction Accuracy

As actual cylinder break results become available, predicted and measured compressive strengths may be compared.

### Equation 6 — Percentage Prediction Error

```text
Error (%) = [|PredictedPSI - ActualPSI| / ActualPSI] × 100
```

The calculation requires an actual break strength greater than zero.

The project's 8% accuracy objective will be evaluated against available laboratory results.

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

### 7.4 External Dataset Evaluation

If historical laboratory break results are unavailable from Vulcan Materials, the development team may use publicly available concrete compressive strength datasets, including datasets hosted on Kaggle, for preliminary model evaluation.

Datasets will be reviewed for compatibility with the model's required inputs, including cement type, curing age, temperature history, reference strength, and measured compressive strength.

Datasets without sufficient information may be used only for limited comparisons.

Public datasets may help evaluate the model but cannot establish accuracy for Vulcan Materials' specific concrete mixtures.

Dataset sources, limitations, and preprocessing will be documented when external data is used.

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
- Future temperatures are assumed rather than measured.
- The model does not independently account for every recorded mixture characteristic.
- Accuracy must be evaluated against actual laboratory results.

---

## 9. Approval

This ADR establishes the proposed mathematical method, default constants, and assumptions for the initial strength estimation engine.

Implementation may begin once the calculation method has been reviewed and approved by both project leads.

**Backend Lead:** Proposed
**Frontend Lead:** Pending
**Status:** Proposed
