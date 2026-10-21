# ADR-003: Canonical Measurement Units

## 1. Context

The Vulcan Materials Concrete Batch Analysis and Strength Prediction System collects and processes measurements associated with concrete composition, specimen dimensions, curing conditions, environmental conditions, and compressive strength testing.

To ensure consistent calculations, reliable validation, and accurate reporting, the application requires a standardized set of measurement units.

Storing measurements in inconsistent units introduces the risk of incorrect calculations and unreliable comparisons between concrete batches. Additionally, concrete mix composition percentages and cement chemistry percentages represent different quantities and must remain distinct.

The team must therefore establish canonical units for all stored measurements, consistent with the approved data schema and client decisions.

## 2. Decision

The team has selected standardized canonical measurement units for all measurements stored in the application's database.

### 2.1 Canonical Units

| Measurement                  | Schema Field                                                 | Canonical Unit                    |
| ---------------------------- | ------------------------------------------------------------ | --------------------------------- |
| Batch weight                 | `FinalWeightLb`                                              | Pounds (lb)                       |
| Specimen dimensions          | `LengthIn`, `WidthIn`                                        | Inches (in)                       |
| Compressive strength         | `StrengthPsi`, `ExpectedFinalBreakPsi`                       | Pounds per square inch (PSI)      |
| Temperature                  | `InternalTempF`, `AmbientTempF`                              | Degrees Fahrenheit (°F)           |
| Electrical resistivity       | `ResistivityOhmPerFt`                                        | Ohms per foot (ohms/ft)           |
| Carbon dioxide concentration | `Co2Ppm`                                                     | Parts per million (ppm)           |
| Cement oxide composition     | `AluminaPct`, `IronOxidePct`, `MagnesiumOxidePct`, `SulfurTrioxidePct` | Percentage of cement (%)          |
| Concrete mix composition     | `CementPct`, `ScmPct`, `CoarseAggregatePct`, `FineAggregatePct`, `WaterPct` | Percentage by absolute volume (%) |
| Mix-water pH                 | `MixWaterPh`                                                 | Unitless                          |
| Moisture                     | `MoisturePct`                                                | Percentage (%)                    |
| Relative humidity            | `RelativeHumidityPct`                                        | Percentage (%)                    |
| Test age                     | `TestAgeDays`                                                | Days                              |
| Timestamps                   | `CastAt`, `BrokenAt`, `TakenAt` and audit timestamps         | Coordinated Universal Time (UTC)  |

All percentages are stored as values from 0 to 100. For example, 12.5 represents 12.5%, not 0.125.

Timestamps are stored in UTC and displayed in the workstation's local time.

### 2.2 Calculated Values

The water-to-cement ratio is calculated dynamically from the stored mix percentages and material-specific gravities. It is not stored in the database.

The calculation follows the approved schema:

WaterCementRatio =
(WaterPct × SG_water) /
(CementPct × SG_cement + ScmPct × SG_scm)

When SCM is present, the calculated value is technically a water-to-cementitious-materials ratio (w/cm).

The total composition percentage and remaining air/other percentage are also calculated rather than stored.

### 2.3 Input Validation and Unit Conversion

The UI performs preliminary validation to provide immediate feedback when an operator enters invalid or incomplete data.

All submitted data passes through the ASP.NET Core API, which delegates authoritative validation to the application service layer before persistence.

Unit conversion occurs exclusively within `Vulcan.Domain.Units`. Where alternative input units are supported, measurements are converted to their canonical units before storage.

Values exceeding the permitted precision of two decimal places are rejected rather than rounded. Fields with explicitly defined alternative precision, such as curing temperatures, follow their field-specific rules.

The application enforces the approved validation limits against canonical values to ensure consistent interpretation across the UI, API, and database.

### 2.4 Database Representation

Database fields use the names and units established in the approved data schema.

For example:

- `FinalWeightLb`
- `LengthIn`
- `WidthIn`
- `StrengthPsi`
- `AmbientTempF`
- `Co2Ppm`

The application maintains a distinction between mix composition percentages by absolute volume and cement oxide percentages by cement content.

All database operations must preserve the established canonical units.

## 3. Consequences

| Positive Consequences                                  | Trade-offs                                                 |
| ------------------------------------------------------ | ---------------------------------------------------------- |
| Consistent measurement storage across concrete batches | Alternative input units require conversion before storage  |
| Reduced risk of unit-related calculation errors        | Conversion logic must be implemented and tested            |
| Consistent validation in both the UI and API           | Both layers must follow the approved validation rules      |
| Simplified comparison of measurements                  | Developers must consistently use the established units     |
| Clear database field definitions                       | Changes to canonical units may require database migrations |
| Standardized timestamps                                | Local times must be converted to and from UTC              |

### Data Integrity

Establishing canonical units reduces the risk of inconsistent measurements being stored or used in calculations.

Values with more than two decimal places are rejected rather than rounded, except where the approved schema specifies a different precision.

Unit conversion is centralized in `Vulcan.Domain.Units` to prevent inconsistent conversion logic across application components.

### Reporting and Calculations

Calculations and reports operate on canonical values.

Where alternative display units are supported, conversions occur when presenting data without modifying the underlying stored measurements.

Graph labels and generated reports must clearly identify their associated units.

### Future Changes

Changes to canonical units may require database migrations, updates to validation rules, and conversion of previously stored measurements.

Any such changes must preserve the integrity of existing records and maintain consistency across calculations and reports.

## 4. Status

**Approved** —

## References

- [Approved Data Schema](../schema.md)
- [Client Decisions](../client-decisions.md)
