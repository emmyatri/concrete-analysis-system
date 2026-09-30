## Range Validation Rules
The following rules apply to the respective numerical field. Empty ranges indicate no upper or lower bound depending on whether a minimum or maximum is set respectively.

| field                         | minimum | maximum | decimal places | severity | message                                                                                                                                            | requirement # | source                         |
| ----------------------------- | ------- | ------- | -------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- | ------------------------------ |
| `Batch.FinalWeightLb`         | 0.01    |         | 2              | Reject   | If Range Error:<br>Weight must be greater than 0.<br><br>If Round Error:<br>Weight must be within two decimals.                                    | FR-1          | schema.md                      |
| `Batch.ExpectedFinalBreakPsi` | 3000    | 10000   | 0              | Reject   | PSI must be within the range \[0-10000\]                                                                                                           | FR-5          | schema.md, client-decisions.md |
| `Batch.MixWaterPh`            | 1       | 9       |                | Reject   | pH must be within the range \[1-9\]                                                                                                                | FR-7          | schema.md, client-decisions.md |
| `Batch.MixWaterPh`            | 7       | 7.5     |                | Warn     | pH is outside the normal range \[7.0-7.5\]                                                                                                         | FR-7          | schema.md, client-decisions.md |
| `Composition.CementPct`       |         |         |                |          |                                                                                                                                                    |               |                                |
| `Specimen.LengthIn`           | 0.01    | 12      | 2              | Reject   | If Range Error: Specimen length must be nonzero and in the range \[0-12].<br><br>If Round Error:<br>Specimen length must be within two decimals.   | FR-4          | SLA                            |
| `Specimen.WidthIn`            | 0.01    | 6.00    | 2              | Reject   | If Range Error:<br>Specimen width must be nonzero and in the range \[0-12\].<br><br>If Round Error:<br>Specimen width must be within two decimals. | FR-4          | SLA                            |
| `BreakResult.StrengthPsi`     | 1       | 10000   | 0              | Reject   | PSI must be within the range \[0-10000\]                                                                                                           | FR-5          | SLA, schema.md                 |
| `BreakResult.StrengthPsi`     | 3000    | 10000   | 0              | Warn     | PSI is below expected value \[3000\]                                                                                                               | FR-5          | schema.md                      |
| `CuringReading.InternalTempF` |         |         |                |          |                                                                                                                                                    |               |                                |

## Test Cases

