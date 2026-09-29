
# ADR-003: Canonical Measurement Units

## 1. Context

The Vulcan Materials Concrete Batch Analysis and Strength Prediction System collects and processes measurements associated with concrete composition, specimen dimensions, curing conditions, environmental conditions, and compressive strength testing.

To ensure consistent calculations, reliable data validation, and accurate reporting, the application requires a standardized set of measurement units.

Storing measurements in different units without explicitly identifying them introduces the risk of incorrect calculations and inconsistent comparisons between concrete batches. Additionally, concrete mix composition percentages and water-to-cement ratios use different measurement bases and must be distinguished.

The team must establish canonical measurement units to ensure that all components of the application interpret and process stored data consistently.

## 2. Decision

The team has selected a standardized set of canonical measurement units for all numerical measurements stored in the application's database.

### 2.1 Canonical Units

| Measurement | Canonical Unit | Symbol |
|---|---|---|
| Weight | Pounds | lb |
| Length and specimen dimensions | Inches | in |
| Compressive strength | Pounds per square inch | PSI |
| Temperature | Degrees Fahrenheit | °F |
| Electrical resistance per unit length | Ohms per foot | ohms/ft |
| Concentration | Parts per million | ppm |
| Concrete mix composition | Percentage by volume | % |
| Water-to-cement ratio | Ratio by weight | Unitless |
| Timestamps | Coordinated Universal Time | UTC |

All measurements will be converted to their canonical units before being persisted in the database.

Concrete mix composition percentages will be stored by volume. The water-to-cement ratio will be calculated using the respective weights of water and cement.

Timestamps will be stored in UTC to ensure consistent representation of dates and times.

### 2.2 Input Validation and Unit Conversion

The application will distinguish between user-entered values and the canonical values stored in the database.

Where alternative input units are supported, measurements must be converted to their corresponding canonical units before persistence.

The application service layer will enforce the approved validation rules against canonical values before they are stored.

The WPF interface may perform preliminary validation to provide immediate feedback, but authoritative validation must occur before database persistence.

This ensures that measurements are interpreted consistently regardless of how they are entered or which application component initiates an operation.

### 2.3 Database Representation

Database fields representing physical measurements will use clearly defined names that identify their associated units.

For example:

- `FinalWeightLb`
- `CylinderLengthIn`
- `CylinderDiameterIn`
- `BreakStrengthPsi`
- `AmbientTempF`

Measurements will be stored using appropriate numeric data types and precision for their intended use.

The application will maintain a consistent distinction between percentages by volume and ratios by weight.

All date and time fields will use UTC.

## 3. Consequences

| Positive Consequences | Trade-offs |
|---|---|
| Consistent measurement storage across all concrete batches | Alternative input units require conversion before storage |
| Reduced risk of unit-related calculation errors | Conversion logic must be implemented and tested |
| Simplified validation and comparison of measurements | Validation limits must use the correct canonical units |
| Consistent reporting and graphical representation | Alternative display units require additional conversion logic |
| Clear database field definitions | Developers must consistently follow established naming conventions |
| Standardized timestamps | Local times must be converted to and from UTC |

### Data Integrity

Establishing canonical units reduces the risk of inconsistent measurements being stored or used in calculations.

All measurements must satisfy the approved validation rules before persistence. Unit conversion must not introduce unintended rounding or precision loss.

### Reporting and Calculations

Calculations and reports will operate on canonical values.

Where alternative display units are supported, conversions will occur when presenting data without changing the underlying stored measurements.

Graph labels and generated reports must clearly identify the units associated with displayed values.

### Future Changes

Changes to canonical units may require database migrations, updates to validation rules, and conversion of previously stored measurements.

Any such changes must preserve the integrity of existing records and maintain consistency across calculations and reports.

## 4. Status

**Proposed** — Pending Co-Lead approval.
