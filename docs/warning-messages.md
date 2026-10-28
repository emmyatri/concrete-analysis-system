# Plain-language early-warning messages

SCRUM-182 · Requirement: FR-16

## Purpose

Explain each triggered early-warning factor using the batch’s actual measurements. SCRUM-181 decides which factors trigger; this formatter supplies their wording.

Messages must not display internal factor codes or invent missing measurements, thresholds, or causes.

## Message templates

Placeholders in braces are replaced with actual values.

| Factor | Proposed message |
| --- | --- |
| Projected shortfall | The projected 28-day strength is {projectedPsi} PSI, which is {shortfallPsi} PSI below the expected {expectedPsi} PSI—a shortfall of {shortfallPct}%. |
| Below minimum strength | The projected 28-day strength is {projectedPsi} PSI, below the configured minimum of {minimumPsi} PSI. |
| Expected strength versus water–cementitious ratio | The expected final strength of {expectedPsi} PSI is {excessPct}% above the approximate {typicalPsi} PSI from the ACI reference for a water–cementitious ratio of {ratio}. The target may be too high for this much water relative to cementitious material. |
| Water–cement ratio | The water–cement ratio is {ratio}, {direction} the configured warning {boundLabel} of {limit}. |
| SCM percentage | Supplementary cementitious material makes up {value}% of the mix, {direction} the configured warning {boundLabel} of {limit}%. |
| Coarse aggregate | Coarse aggregate makes up {value}% of the mix, {direction} the configured warning {boundLabel} of {limit}%. |
| Fine aggregate | Fine aggregate makes up {value}% of the mix, {direction} the configured warning {boundLabel} of {limit}%. |
| Alumina | The cement’s alumina content is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Iron oxide | The cement’s iron oxide content is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Magnesium oxide | The cement’s magnesium oxide content is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Sulfur trioxide | The cement’s sulfur trioxide content is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Mix water pH | The mix water pH is {value}, {direction} the configured warning {boundLabel} of {limit}. |
| Cement surface pH | The cement surface pH is {value}, {direction} the configured warning {boundLabel} of {limit}. |
| Internal temperature | The internal temperature reading is {value} °F, {direction} the configured warning {boundLabel} of {limit} °F. |
| Ambient temperature | The ambient temperature reading is {value} °F, {direction} the configured warning {boundLabel} of {limit} °F. |
| Moisture | The moisture reading is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Relative humidity | The relative humidity reading is {value}%, {direction} the configured warning {boundLabel} of {limit}%. |
| Resistivity | The resistivity reading is {value} ohms/ft, {direction} the configured warning {boundLabel} of {limit} ohms/ft. |
| Carbon dioxide | The carbon dioxide reading is {value} {unit}, {direction} the configured warning {boundLabel} of {limit} {unit}. |

For range messages, `{direction}` is “below” or “above”; `{boundLabel}` is “minimum” or “maximum”. Both come from the triggered factor’s comparison with its configured limit.

Water–cement ratio and water–cementitious ratio are distinct measurements. Their labels and values must match the factor being explained.

## Formatting

- Match the application’s established number formatting.
- Show PSI with thousands separators.
- Retain enough precision to explain why a value crossed a limit.
- Show percentages with `%`, temperatures with `°F`, and resistivity with `ohms/ft`.
- pH and ratios have no physical unit; identify them explicitly in the sentence.
- Confirm the carbon dioxide unit against the implemented contract before coding. Do not relabel ppm as percent without conversion.
- Where available, identify the specimen and reading time so operators can locate the measurement.

## Ordering

1. Projected strength shortfall.
2. Projected strength below the configured minimum.
3. Other factors in an agreed, deterministic order.

The remaining explanatory ranking must be agreed with SCRUM-181’s developer. Do not invent numerical importance scores.

## Test plan

- Every supported factor produces a sentence containing its actual value and applicable unit or measurement label.
- Messages include the correct limit and comparison direction.
- Different input values produce corresponding changes in the message.
- Projected shortfall messages include projected strength, expected strength, shortfall in PSI, and shortfall percentage.
- Expected-strength-versus-ratio messages include the actual ratio, expected strength, reference strength, and percentage difference.
- Output contains no internal factor codes or unresolved placeholders.
- Formatting does not conceal a threshold crossing.
- Ordering puts projected shortfall first and is deterministic.
- Missing supporting data is handled explicitly without fabricated numbers; agree on that behavior before implementation.
- Once connected to the warning service, every returned warning has at least one nonempty explanation.

## Questions I need to figure out though

- Factor codes and shared input/output types.
- Which supporting values SCRUM-181 supplies.
- Ordering among mix and curing factors.
- Missing-data and unsupported-factor behavior.
- Carbon dioxide units and reading identification.
- Which measurements currently have approved warning ranges.
