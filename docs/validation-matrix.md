## Range Validation Rules
The following rules apply to the respective numerical field. Unavailable ranges indicate no upper or lower bound depending on whether a minimum or maximum is set respectively.

| field                            | minimum | maximum | decimal places | severity | message                                                                                                                                              | requirement #   | source                         |
| -------------------------------- | ------- | ------- | -------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | --------------- | ------------------------------ |
| `Batch.FinalWeightLb`            | 0.01    | N/A     | 2              | Reject   | If Range Error:<br>Weight must be greater than 0<br><br>If Round Error:<br>Weight must be within two decimals                                        | FR-1            | schema.md                      |
| `Batch.ExpectedFinalBreakPsi`    | 3000    | 10000   | 0              | Reject   | PSI must be within the range \[3000-10,000\]                                                                                                         | FR-5            | schema.md, client-decisions.md |
| `Batch.MixWaterPh`               | 1       | 9       | N/A            | Reject   | pH must be within the range \[1-9\]                                                                                                                  | FR-7            | schema.md, client-decisions.md |
| `Batch.MixWaterPh`               | 7       | 7.5     | N/A            | Warn     | pH is outside the normal range \[7.0-7.5\]                                                                                                           | FR-7            | schema.md, client-decisions.md |
| `Composition.CementPct`          | 10      | 15      | N/A            | Reject   | Cement composition must be within the range \[10-15\]                                                                                                | FR-2.1          | schema.md, client-decisions.md |
| `Composition.ScmPct`             | 0       | 100     | N/A            | Reject   | SCM composition must be a valid percentage \[0-100\]                                                                                                 | FR-2            | schema.md, client-decisions.md |
| `Composition.FineAggregatePct`   | 25      | 30      | N/A            | Reject   | Fine aggregate composition must be within the range \[25-30\]                                                                                        | FR-2.1          | schema.md, client-decisions.md |
| `Composition.CoarseAggregatePct` | 40      | 55      | N/A            | Reject   | Coarse aggregate composition must be within the range \[40-55\]                                                                                      | FR-2.1          | schema.md, client-decisions.md |
| `Composition.WaterPct`           | 15      | 20      | N/A            | Reject   | Water composition must be within the range \[15-20\]                                                                                                 | FR-2.1          | schema.md, client-decisions.md |
| `Composition.TotalPct`           | N/A     | 100     | N/A            | Reject   | Total composition must not exceed 100%                                                                                                               | NA / Decision 2 | schema.md, client-decisions.md |
| `Composition.WaterCementRatio`   | 0.40    | 0.60    | N/A            | Warn     | The water to cement ratio is outside the normal range \[0.4-0.6\]                                                                                    | FR-8            | schema.md, client-decisions.md |
| `Oxide.AluminaPct`               | 3.0     | 7.5     | N/A            | Warn     | The aluminum oxide percent is outside the normal range \[3.0-7.5\]                                                                                   | FR-3            | schema.md, client-decisions.md |
| `Oxide.AluminaPct`               | 0       | 100     | N/A            | Reject   | The aluminum oxide percent must be a valid percentage \[0-100\]                                                                                      | FR-3            | schema.md, client-decisions.md |
| `Oxide.IronOxidePct`             | 0.5     | 6.0     | N/A            | Warn     | The iron oxide percent is outside the normal range \[0.5-6.0\]                                                                                       | FR-3            | schema.md, client-decisions.md |
| `Oxide.IronOxidePct`             | 0       | 100     | N/A            | Reject   | The iron oxide percent must be a valid percentage \[0-100\]                                                                                          | FR-3            | schema.md, client-decisions.md |
| `Oxide.MagnesiumOxidePct`        | 0.1     | 4.0     | N/A            | Warn     | The magnesium oxide percent is outside the normal range \[0.1-4.0\]                                                                                  | FR-3            | schema.md, client-decisions.md |
| `Oxide.MagnesiumOxidePct`        | 0       | 6.0     | N/A            | Reject   | The magnesium oxide percent must be within the range \[0.0-6.0\]                                                                                     | FR-3            | schema.md, client-decisions.md |
| `Oxide.SulfurTrioxidePct`        | 1.3     | 3.0     | N/A            | Warn     | The sulfur trioxide percent is outside the normal range \[1.3-3.0\]                                                                                  | FR-3            | schma.md, client-decisions.md  |
| `Oxide.SulfurTrioxidePct`        | 0       | 4.5     | N/A            | Reject   | The sulfur trioxide percent must be within the range \[0.0-4.5\]                                                                                     | FR-3            | schema.md, client-decisions.md |
| `Specimen.LengthIn`              | 0.01    | 12      | 2              | Reject   | If Range Error: Specimen length must be nonzero and in the range \[0-12]<br><br>If Round Error:<br>Specimen length must be within two decimals       | FR-4            | SLA                            |
| `Specimen.WidthIn`               | 0.01    | 6.00    | 2              | Reject   | If Range Error:<br>Specimen width must be nonzero and in the range \[0-6\]<br><br>If Round Error:<br>Specimen width must be within two decimals      | FR-4            | SLA                            |
| `Break.StrengthPsi`              | 1       | 10000   | 0              | Reject   | PSI must be within the range \[1-10000\]                                                                                                             | FR-5            | SLA, schema.md                 |
| `Break.StrengthPsi`              | 3000    | 10000   | 0              | Warn     | PSI is below expected value \[3000\]                                                                                                                 | FR-5            | schema.md                      |
| `Curing.InternalTempF`           | 0       | 200     | 1              | Reject   | If Range Error:<br>Internal temperature must be within the range \[0-200\]<br><br>If Round Error:<br>Internal temperature must be within one decimal | FR-6            | schema.md, client-decisions.md |
| `Curing.AmbientTempF`            | 60      | 80      | N/A            | Warn     | Ambient temperature is outside the normal range \[60-80\]                                                                                            | FR-6            | schema.md, client-decisions.md |
| `Curing.AmbientTempF`            | 0       | 150     | N/A            | Reject   | Ambient temperature must be within the range \[0-150\]                                                                                               | FR-6            | schema.md, client-decisions.md |
| `Curing.MoisturePct`             | 0       | 100     | N/A            | Reject   | Moisture percent must be a valid percentage \[0-100\]                                                                                                | FR-6            | schema.md                      |
| `Curing.RelativeHumidityPct`     | 0       | 100     | N/A            | Reject   | Relative humidity percent must be a valid percentage \[0-100\]                                                                                       | FR-6            | schema.md                      |
| `Curing.ResistivityOhmPerFt`     | 0.01    | N/A     | N/A            | Reject   | Resistivity must be a positive number                                                                                                                | FR-6            | schema.md                      |
| `Curing.Co2Ppm`                  | 0       | N/A     | N/A            | Reject   | CO2 ppm must be non-negative                                                                                                                         | FR-6            | schema.md                      |
## Test Cases
Each field has a test case covering:
- The lower bound of the range
- The upper bound of the range
- The value just within the lower bound of the range
- The value just within the upper bound of the range
- A value with a rounding error

This holds true for both warning and error (Reject) thresholds.
### Boundary Tests
| field                            | input | output |
| -------------------------------- | ----- | ------ |
| `Batch.FinalWeightLb`            | 0.00  | Reject |
| `Batch.FinalWeightLb`            | 0.01  | Valid  |
| `Batch.FinalWeightLb`            | 0.02  | Valid  |
| `Batch.ExpectedFinalBreakPsi`    | 2999  | Reject |
| `Batch.ExpectedFinalBreakPsi`    | 3000  | Valid  |
| `Batch.ExpectedFinalBreakPsi`    | 10000 | Valid  |
| `Batch.ExpectedFinalBreakPsi`    | 10001 | Reject |
| `Batch.MixWaterPh`               | 0.9   | Reject |
| `Batch.MixWaterPh`               | 1.0   | Valid  |
| `Batch.MixWaterPh`               | 9.0   | Valid  |
| `Batch.MixWaterPh`               | 9.1   | Reject |
| `Batch.MixWaterPh`               | 6.9   | Warn   |
| `Batch.MixWaterPh`               | 7.0   | Valid  |
| `Batch.MixWaterPh`               | 7.5   | Valid  |
| `Batch.MixWaterPh`               | 7.6   | Warn   |
| `Composition.CementPct`          | 9     | Reject |
| `Composition.CementPct`          | 10    | Valid  |
| `Composition.CementPct`          | 15    | Valid  |
| `Composition.CementPct`          | 16    | Reject |
| `Composition.ScmPct`             | -1    | Reject |
| `Composition.ScmPct`             | 0     | Valid  |
| `Composition.ScmPct`             | 100   | Valid  |
| `Composition.ScmPct`             | 101   | Reject |
| `Composition.FineAggregatePct`   | 24    | Reject |
| `Composition.FineAggregatePct`   | 25    | Valid  |
| `Composition.FineAggregatePct`   | 30    | Valid  |
| `Composition.FineAggregatePct`   | 31    | Reject |
| `Composition.CoarseAggregatePct` | 39    | Reject |
| `Composition.CoarseAggregatePct` | 40    | Valid  |
| `Composition.CoarseAggregatePct` | 55    | Valid  |
| `Composition.CoarseAggregatePct` | 56    | Reject |
| `Composition.WaterPct`           | 14    | Reject |
| `Composition.WaterPct`           | 15    | Valid  |
| `Composition.WaterPct`           | 20    | Valid  |
| `Composition.WaterPct`           | 21    | Reject |
| `Composition.TotalPct`           | 99    | Valid  |
| `Composition.TotalPct`           | 100   | Valid  |
| `Composition.TotalPct`           | 101   | Reject |
| `Composition.WaterCementRatio`   | 0.39  | Warn   |
| `Composition.WaterCementRatio`   | 0.40  | Valid  |
| `Composition.WaterCementRatio`   | 0.60  | Valid  |
| `Composition.WaterCementRatio`   | 0.61  | Warn   |
| `Oxide.AluminaPct`               | -0.1  | Reject |
| `Oxide.AluminaPct`               | 0     | Warn   |
| `Oxide.AluminaPct`               | 3.0   | Valid  |
| `Oxide.AluminaPct`               | 7.5   | Valid  |
| `Oxide.AluminaPct`               | 7.6   | Warn   |
| `Oxide.AluminaPct`               | 100   | Warn   |
| `Oxide.AluminaPct`               | 100.1 | Reject |
| `Oxide.IronOxidePct`             | -0.1  | Reject |
| `Oxide.IronOxidePct`             | 0     | Warn   |
| `Oxide.IronOxidePct`             | 0.4   | Warn   |
| `Oxide.IronOxidePct`             | 0.5   | Valid  |
| `Oxide.IronOxidePct`             | 6.0   | Valid  |
| `Oxide.IronOxidePct`             | 6.1   | Warn   |
| `Oxide.IronOxidePct`             | 100   | Warn   |
| `Oxide.IronOxidePct`             | 100.1 | Reject |
| `Oxide.MagnesiumOxidePct`        | -0.1  | Reject |
| `Oxide.MagnesiumOxidePct`        | 0     | Warn   |
| `Oxide.MagnesiumOxidePct`        | 0.1   | Valid  |
| `Oxide.MagnesiumOxidePct`        | 4.0   | Valid  |
| `Oxide.MagnesiumOxidePct`        | 4.1   | Warn   |
| `Oxide.MagnesiumOxidePct`        | 6.0   | Warn   |
| `Oxide.MagnesiumOxidePct`        | 6.1   | Reject |
| `Oxide.SulfurTrioxidePct`        | -0.1  | Reject |
| `Oxide.SulfurTrioxidePct`        | 0     | Warn   |
| `Oxide.SulfurTrioxidePct`        | 1.2   | Warn   |
| `Oxide.SulfurTrioxidePct`        | 1.3   | Valid  |
| `Oxide.SulfurTrioxidePct`        | 3.0   | Valid  |
| `Oxide.SulfurTrioxidePct`        | 3.1   | Warn   |
| `Oxide.SulfurTrioxidePct`        | 4.5   | Warn   |
| `Oxide.SulfurTrioxidePct`        | 4.6   | Reject |
| `Specimen.LengthIn`              | 0.00  | Reject |
| `Specimen.LengthIn`              | 0.01  | Valid  |
| `Specimen.LengthIn`              | 12.00 | Valid  |
| `Specimen.LengthIn`              | 12.01 | Reject |
| `Specimen.WidthIn`               | 0.00  | Reject |
| `Specimen.WidthIn`               | 0.01  | Valid  |
| `Specimen.WidthIn`               | 6.00  | Valid  |
| `Specimen.WidthIn`               | 6.01  | Reject |
| `Break.StrengthPsi`              | 0     | Reject |
| `Break.StrengthPsi`              | 1     | Warn   |
| `Break.StrengthPsi`              | 10000 | Valid  |
| `Break.StrengthPsi`              | 10001 | Reject |
| `Break.StrengthPsi`              | 2999  | Warn   |
| `Break.StrengthPsi`              | 3000  | Valid  |
| `Curing.AmbientTempF`            | -1    | Reject |
| `Curing.AmbientTempF`            | 0     | Warn   |
| `Curing.AmbientTempF`            | 150   | Warn   |
| `Curing.AmbientTempF`            | 151   | Reject |
| `Curing.AmbientTempF`            | 59    | Warn   |
| `Curing.AmbientTempF`            | 60    | Valid  |
| `Curing.AmbientTempF`            | 80    | Valid  |
| `Curing.AmbientTempF`            | 81    | Warn   |
| `Curing.InternalTempF`           | -1    | Reject |
| `Curing.InternalTempF`           | 0     | Valid  |
| `Curing.InternalTempF`           | 200   | Valid  |
| `Curing.InternalTempF`           | 201   | Reject |
| `Curing.MoisturePct`             | -1    | Reject |
| `Curing.MoisturePct`             | 0     | Valid  |
| `Curing.MoisturePct`             | 100   | Valid  |
| `Curing.MoisturePct`             | 101   | Reject |
| `Curing.RelativeHumidityPct`     | -1    | Reject |
| `Curing.RelativeHumidityPct`     | 0     | Valid  |
| `Curing.RelativeHumidityPct`     | 100   | Valid  |
| `Curing.RelativeHumidityPct`     | 101   | Reject |
| `Curing.ResistivityOhmPerFt`     | 0     | Reject |
| `Curing.ResistivityOhmPerFt`     | 0.01  | Valid  |
| `Curing.ResistivityOhmPerFt`     | 0.02  | Valid  |
| `Curing.Co2Ppm`                  | -1    | Reject |
| `Curing.Co2Ppm`                  | 0     | Valid  |
| `Curing.Co2Ppm`                  | 1     | Valid  |
### Rounding Tests
| field                  | input  | output |
| ---------------------- | ------ | ------ |
| `Batch.FinalWeightLb`  | 0.01   | Valid  |
| `Batch.FinalWeightLb`  | 1.23   | Valid  |
| `Batch.FinalWeightLb`  | 1.234  | Reject |
| `Batch.FinalWeightLb`  | 12.345 | Reject |
| `Specimen.LengthIn`    | 0.01   | Valid  |
| `Specimen.LengthIn`    | 1.23   | Valid  |
| `Specimen.LengthIn`    | 12.00  | Valid  |
| `Specimen.LengthIn`    | 1.234  | Reject |
| `Specimen.LengthIn`    | 12.001 | Reject |
| `Specimen.WidthIn`     | 0.01   | Valid  |
| `Specimen.WidthIn`     | 1.23   | Valid  |
| `Specimen.WidthIn`     | 6.00   | Valid  |
| `Specimen.WidthIn`     | 1.234  | Reject |
| `Specimen.WidthIn`     | 6.001  | Reject |
| `Curing.InternalTempF` | 0.0    | Valid  |
| `Curing.InternalTempF` | 72.0   | Valid  |
| `Curing.InternalTempF` | 72.1   | Valid  |
| `Curing.InternalTempF` | 72.12  | Reject |
| `Curing.InternalTempF` | -10.01 | Reject |
