# Milestone 2 QA Checklist (Internal)

| Document Info | Details |
|---|---|
| **File**                 | `docs/m2-qa-checklist.md`                                                                |
| **Ticket**               | SCRUM-190 (M2-85)                                                                      |
| **Target Build**         | `v0.1.0-m2`                                                                              |
| **Purpose**              | Full internal check for the bug bash. The client demo script is `docs/m2-acceptance.md`. |
| **Requirements Covered** | FR-11, FR-12, FR-14, FR-15, FR-16                                                      |

---

## Contents

- [Execution Record](#execution-record)
- [Test Execution Matrix](#test-execution-matrix)
  1. [No Curing Data to Date](#section-1-empty-curing-data)
  2. [Initial Prediction](#section-2-initial-prediction)
  3. [Prediction Adjustments](#section-3-prediction-adjustments)
  4. [Strength Chart](#section-4-strength-chart)
  5. [Curing Reading Entry (Ambient Temp Warning)](#section-5-curing-reading-entry)
  6. [Curing Strip](#section-6-curing-strip)
  7. [Day-1 / Day-3 Summary](#section-7-day-1-day-3-summary)
  8. [3/7/28-Day Strength Estimates](#section-8-strength-estimates)
  9. [Early Warnings](#section-9-early-warnings)
  10. [Accuracy Benchmark](#section-10-accuracy-benchmark)
- [Sign-Off & Acceptance Summary](#sign-off--acceptance-summary)

---

## Execution Record

| Field | Value |
|---|---|
| **Tester Name / Role**       |                                     |
| **Date & Time Executed (T)** |                                     |
| **Build / Commit Hash**      |                                     |
| **Execution Environment**    | Windows 11 (clean test environment) |
| **Final Result**             | ☐ PASS / ☐ FAIL                     |
| **Total Execution Time**     | _____ minutes                  |

---

## Test Execution Matrix

> **Execution flow:** Run the sections in order. Sections 1–3 use VM-2026-0142, while Sections 4–9 use VM-SYN-006 and VM-SYN-009, and steps 9.6–9.7 use VM-SYN-002 and VM-SYN-003. Section 10 verifies the documented accuracy-benchmark agreement separately from application testing.
>
> **Sections 1–3 (VM-2026-0142):** No curing data → Enter three curing readings for A1 → Verify initial 3-, 7-, and 28-day predictions → Record A1's 3-day break → Verify adjusted predictions.
>
> **Sections 4–9 (VM-SYN-006, VM-SYN-009):** Strength chart → Enter flagged ambient-temperature reading for D3-1 → Curing strip → Day-1/Day-3 summary → 3-, 7-, and 28-day strength estimates → Early warnings.
>
> **Section 10 (Accuracy Benchmark):** Verify documented client agreement on the accuracy benchmark and measurement method.
>
> **Dependencies:** Section 3 requires the readings saved in Section 2. Sections 6–9 require the ambient-temperature reading saved in Section 5. When using the in-memory mock, avoid refreshing between dependent steps.
>
> **Before you start (step 0, not counted):** Close the app, delete `%LOCALAPPDATA%\VulcanCBAS\vulcan.db`, then relaunch. The synthetic batches reload with their stored predictions. A database from before #83 shows "Not enough curing data yet" on every batch.
>
> **VM-2026-0142 setup (real app only, not counted):** This batch exists only in the mock data. With mock mode off, create it first from **New batch**:
> • Batch number `VM-2026-0142`, cast `09/29/2026 07:40`, final weight `25` lb, expected final break `6,000` PSI, cement type **Type III**
> • Mix: cement `14`%, water `15`%, coarse `42`%, fine `25`%, SCM **Fly ash** `2`%
> • pH: mix water `7.2`, cement surface `7.5`
> • Oxides: alumina `4.8`%, iron oxide `2.8`%, magnesium oxide `1.8`%, sulfur trioxide `2.2`%
> • Cylinders (12 × 6 in): A1–A2 **Day 3**, A3–A4 **Day 7**, A5–A6 **Day 28**
> Saving shows one w/c warning (0.31 is below 0.4). That warning is expected.
>
> **Real-app dependency:** Steps 4.1 and 9.1 come from the early-warning service (SCRUM-181/182). If that isn't merged, record them as **Blocked**, not **Fail**.

---

<a id="section-1-empty-curing-data"></a>
### Section 1: No Curing Data to Date

> *Navigate to Batch Detail for VM-2026-0142 and verify the main visualization when no curing readings exist for a batch.*
>
> **Starting point:** Home Dashboard for Vulcan CBAS.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | On the Home dashboard, under **Recent batches**, click row: `VM-2026-0142` | Opens full Batch Detail view for VM-2026-0142. Note: Insufficient data for prediction doesn't mean no warnings exist.<br>• **Strength chart** reads: “Not enough curing data yet; Estimates start once a cylinder has a reading at least 24 hours after cast. No readings yet.” and “No breaks recorded yet.”<br>• **Curing strip** reads: “No curing readings yet.”<br>• **Day 1 / Day 3 summary:** “No curing readings yet.”<br>• **3-, 7-, and 28-day estimate cards:** “Not enough curing data yet.” | FR-10, FR-14 | ☐ |

---

<a id="section-2-initial-prediction"></a>
### Section 2: Initial Prediction

> *Enter curing readings for VM-2026-0142 and verify the initial predictions before any break results are recorded.*
>
> **Starting point:** Home Dashboard for Vulcan CBAS.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Click **Curing readings** in the left navigation sidebar. | On opening:<br>• Affordances for batch selection and cylinder are visible as dropdowns. | FR-6 | ☐ |
> | 2.2 | In the batch-selection dropdown, select `VM-2026-0142`. | • **Batch:** VM-2026-0142 is selected<br>• **Cylinder:** defaults to A1<br>• **Cast line:** "Cast 09/29/26 07:40 · View batch"<br>• **Grid:** **A1 · readings** with one empty new row (Row 1)<br>• **Time taken:** defaults to now | FR-6 | ☐ |
> | 2.3 | Enter in Row 1:<br>• Time taken: `09/29/2026 8:00`<br>• Internal (°F): `72.0`<br>• Ambient (°F): `70.0`<br>• Moisture (%): `92.0`<br>• RH (%): `95.0`<br>• Resistivity (Ω/ft): `15.00`<br>• CO₂ (ppm): `420`<br>Click **Add row**. | • Row 1 locks and reads 72.0 °F, 70.0 °F, 92.0%, 95.0%, 15.00 Ω/ft, 420 ppm and a new empty Row 2 appears below. | FR-6 | ☐ |
> | 2.4 | Enter in Row 2:<br>• Time taken: `09/30/2026 8:00`<br>• Internal (°F): `74.0`<br>• Ambient (°F): `70.0`<br>• Moisture (%): `92.5`<br>• RH (%): `95.0`<br>• Resistivity (Ω/ft): `16.50`<br>• CO₂ (ppm): `419`<br>Click **Add row**. | • Row 2 locks and reads 74.0 °F, 70.0 °F, 92.5%, 95.0%, 16.50 Ω/ft, 419 ppm and a new empty Row 3 appears below. | FR-6 | ☐ |
> | 2.5 | Enter in Row 3:<br>• Time taken: `10/02/2026 8:00`<br>• Internal (°F): `75.0`<br>• Ambient (°F): `71.0`<br>• Moisture (%): `93.0`<br>• RH (%): `94.5`<br>• Resistivity (Ω/ft): `18.00`<br>• CO₂ (ppm): `418`<br>Click **Save all 3 rows**. | • Row 3 locks and reads 75.0 °F, 71.0 °F, 93.0%, 94.5%, 18.00 Ω/ft, 418 ppm with status **Saved**. A new empty Row 4 appears below. | FR-6 | ☐ |
> | 2.6 | Click **View Batch**. | Opens full Batch Detail view for VM-2026-0142.<br>• **Strength chart** displays:<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Estimate, Predicted, Expected Final, and Minimum lines.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Estimated break markers for days 3, 7, 28.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Caption:** “No breaks recorded yet.”<br>• **Curing strip** displays:<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Curves for internal temperature, ambient temperature, moisture, RH, resistivity and CO₂.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Caption:** Line = average of 1 cylinder · band = lowest to highest · readings so far: 0–72h<br>• **Day 1 / Day 3 summary:** values for Day 1 only:<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Internal temp (avg):** 72.0 °F<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Temp rise since cast (peak):** +0.0 °F<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Ambient temp (avg):** 70.0 °F<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Moisture change:** +0.0%<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Relative humidity (avg):** 95.0%<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Resistivity change:** +0.0 Ω/ft<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **CO₂ (avg):** 420 ppm<br>**Estimate cards** should include the following values (3-, 7-, and 28-day estimates):<br>• **Break estimate:** 3,573 PSI, 4,814 PSI, 5,990 PSI<br>• **Adjustment details:**<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Breaks due 10/02/26” \| “Before any breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Breaks due 10/06/26” \| “Before any breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Breaks due 10/27/26 · target 6,000 PSI” \| “Before any breaks · Calibration v1”<br>• **How this was calculated** control | FR-10, FR-11, FR-12, FR-14, FR-15; SLA M2 Acceptance | ☐ |

---

<a id="section-3-prediction-adjustments"></a>
### Section 3: Prediction Adjustments

> *Record one break result for VM-2026-0142 and verify how the previously generated estimates adjust.*
>
> **Starting point:** Batch Detail for VM-2026-0142, after completing Section 2.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Click **Enter break**. | The list of cylinders for batch VM-2026-0142 is displayed. | FR-5 | ☐ |
> | 3.2 | Click **Record break** for cylinder `A1`. Enter:<br>• Strength: `3,600`<br>• Break date: `10/2/2026 8:00`<br>Click **Save**. | The record-break modal closes, and a green confirmation banner appears: “Saved VM-2026-0142 · A1: 3,600 PSI.” | FR-5 | ☐ |
> | 3.3 | Click **View batch**. | **Estimate cards** should include the following values (3-, 7-, and 28-day estimates):<br>• **Break estimate:** 3,600 PSI, 4,835 PSI, 5,998 PSI<br>• **Actual break:** 3,600 PSI; No actual break results for the 7- and 28-day estimates<br>• **Adjustment details:**<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3-day breaks · was 3,573 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3-day breaks · was 4,814 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3-day breaks · was 5,990 before breaks · Calibration v1”<br>• **How this was calculated** control | FR-14, FR-15 | ☐ |

---

<a id="section-4-strength-chart"></a>
### Section 4: Strength Chart

> *Navigate to Batch Detail for VM-SYN-006 and verify the main visualization.*
>
> **Starting point:** Home Dashboard for Vulcan CBAS.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | On the Home dashboard, review the **Top Reason** column under **Batches at risk**. | Batches at risk show a “Top Reason” column that contains the projected 28-day strength PSI, and percentage of shortfall relative to its expected PSI. **Example for VM-SYN-006:** “Projected 28-day strength of 4,870 PSI is 21.45% below the expected 6,200 PSI.”<br>Rows are listed largest shortfall first: VM-SYN-003, VM-SYN-006, VM-SYN-002. | FR-16 | ☐ |
> | 4.2 | On the Home dashboard, under **Batches at risk**, click row: VM-SYN-006 | Opens full Batch Detail view, showing:<br>• High-level batch info (cast date, cement type, final weight, etc.)<br>• Composition and cement oxide percentages<br>• Projected strength over time<br>• Curing strip with Day 1 / Day 3 summary<br>• 3-, 7-, and 28-Day estimates<br>• Cylinder and break data<br>• Curing readings<br>• Flagged data (warnings) | FR-10 | ☐ |
> | 4.3 | Review the **strength chart**. | **Strength Chart — Visual Verification**<br>Verify the chart includes a clearly labeled legend and axes, with appropriate units:<br>• **X-axis:** Days since cast<br>• **Y-axis:** Break strength (PSI)<br>Verify the chart displays the following, where applicable:<br>• **Predicted strength curve:** Initial prediction (before breaks) and adjusted prediction (after breaks).<br>• **Strength estimate markers:** 3-, 7-, and 28-day estimates.<br>• **Recorded breaks:** Individual break results, including warning markers for flagged values.<br>• **Mean by age:** Average recorded break strength at each applicable test age.<br>• **Minimum break level:** A clearly identified horizontal reference line indicating the minimum acceptable break strength.<br>• **Expected final break:** A clearly identified reference line indicating the batch's expected final strength. | FR-11, FR-12, FR-14 | ☐ |
> | 4.4 | Hover over the **VM-SYN-006 predicted line**. | Tooltip appears displaying the matching batch name and predicted PSI *after breaks* at the indicated day on the x-axis. **Exact:** “VM-SYN-006 predicted: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 4.5 | Hover over the **VM-SYN-006 before breaks line**. | Tooltip appears displaying the matching batch name and predicted PSI *before breaks* at the indicated day on the x-axis. No crash or unhandled error. **Exact:** “VM-SYN-006 before breaks: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 4.6 | Hover over one of the **VM-SYN-006 estimate markers** (hollow circles). | Tooltip appears displaying the estimated PSI *for a batch* at the indicated day on the x-axis. **Exact:** “VM-SYN-006 estimate: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 4.7 | Hover over one of the **VM-SYN-006 break markers** (filled circles). | Tooltip appears displaying the actual PSI *for a given cylinder* at the indicated day on the x-axis. **Exact:** “D#-#: #,### PSI at day #” | FR-10, FR-11, FR-12 | ☐ |
> | 4.8 | Hover over any open space in the **VM-SYN-006 graph**. | Vertical line tracking along the x-axis appears in curing strip and in the strength chart with a tooltip displaying the predicted PSI *after breaks* at the indicated day on the x-axis. | FR-11, FR-12, FR-14 | ☐ |

---

<a id="section-5-curing-reading-entry"></a>
### Section 5: Curing Reading Entry (Ambient Temp Warning)

> *Establish the early readings needed for prediction and to indicate the warning marker, if not already seeded.*
>
> **Starting point:** Batch Detail for VM-SYN-006 under Curing Readings. Readings are typed into a grid, one row per reading. Each row can be saved individually using **Save** or **Enter**.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | Click **Curing readings** in the upper right corner of the Batch Detail screen for batch: `VM-SYN-006`. | On opening:<br>• Affordances for batch selection and cylinder are visible as dropdowns.<br>• **Batch:** VM-SYN-006 is selected<br>• **Cylinder:** defaults to D3-1<br>• **Cast line:** "Cast 06/21/26 08:00 · View batch"<br>• **Grid:** **D3-1 · readings** with 7 saved readings and one empty new row (Row 8)<br>• **Time taken:** defaults to now | FR-6 | ☐ |
> | 5.2 | Enter in Row 8:<br>• Time taken: `06/21/2026 14:00 (2:00 PM)`, 6 hours after the cast time shown (on an Eastern-time computer)<br>• Internal (°F): `79.5`<br>• Ambient (°F): `85`<br>• Moisture (%): `91.7`<br>• RH (%): `95.2`<br>• Resistivity (Ω/ft): `16.25`<br>• CO₂ (ppm): `417.7`<br>Click the row's **Save** (or press **Enter**). | • **Ambient** cell turns amber (orange border and background). No message shows yet; the grid only lists a warning once a flagged row is saved.<br>• Row 8 locks and reads 79.5 °F, 85.0 °F, 91.7%, 95.2%, 16.25 Ω/ft, 417.7 ppm with status **Saved**. A new empty Row 9 appears below. | FR-6; FR-16 (warning aspect) | ☐ |

---

<a id="section-6-curing-strip"></a>
### Section 6: Curing Strip

> *Explore curing measurements alongside the strength chart.*
>
> **Starting point:** Batch Detail for VM-SYN-006 via View Batch from curing readings entry.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 6.1 | Select **View Batch** in the upper right corner of the Curing readings screen. | Opens full Batch Detail view for VM-SYN-006, showing:<br>• High-level batch info (cast date, cement type, final weight, etc.)<br>• Composition and cement oxide percentages<br>• Projected strength over time<br>• Curing strip with Day 1 / Day 3 summary<br>• 3-, 7-, and 28-Day estimates<br>• Cylinder and break data<br>• Curing readings<br>• Flagged data (warnings) | FR-10 | ☐ |
> | 6.2 | Under the strength chart with the predictive curve, review the **curing strip**. | **Curing Strip — Visual Verification**<br>Verify the chart includes a clearly labeled measurement selector and dropdown affordance, with appropriate units:<br>• **X-axis:** Aligns with the strength chart to clearly indicate days since cast.<br>• **Y-axis:** Proper units of measure for selected measurement (default is “Internal temperature (°F)”).<br>Verify the chart displays the following, where applicable:<br>• **Average readings curve:** The average for the selected measurement across all batch cylinders.<br>• **Shaded band along the curve:** The minimum and maximum values for the selected measurement across all batch cylinders.<br>• Day 1 and Day 3 vertical markers.<br>• Caption based on cylinder count:<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **1 reading:** “1 reading so far (# h)”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **2 readings:** “2 readings so far (# h and # h)”.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **>2 readings:** “Line = average of 9 cylinders · band = lowest to highest · readings so far: 0–72h”. | FR-12 | ☐ |
> | 6.3 | Hover over the **curing strip line**. | Tooltip appears displaying the average value for the selected measurement across all cylinders at the indicated day on the x-axis. | FR-12 | ☐ |
> | 6.4 | Hover over any open space in the **curing strip**. | Vertical line tracking along the x-axis appears along both the strength chart and curing strip. | FR-12 | ☐ |
> | 6.5 | Select each measurement from the **Display Measurement** dropdown. | The y-axis should update to reflect measurement ranges and units relative to the selected measurement:<br>• **Internal Temperature** = °F<br>• **Ambient Temperature** = °F<br>• **Moisture** = %<br>• **Relative Humidity** = %<br>• **Resistivity** = Ω/ft<br>• **CO₂** = ppm<br>Confirm that each chart renders a band for maximum and minimum values. Ambient temperature may not display a band if all cylinders record the same ambient temperature. | FR-12 | ☐ |

---

<a id="section-7-day-1-day-3-summary"></a>
### Section 7: Day-1 / Day-3 Summary

> *Review the summarized curing conditions and changes.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 7.1 | On the Batch Detail screen, to the right of the curing strip should be a card for a Day 1 / Day 3 curing reading summary. Review the **Day 1 / Day 3 summary**. | The Day 1 / Day 3 summary should display the following values in the Day 1 and Day 3 columns, respectively. Values are expected to differ from the strip.<br>• **Internal temp (avg):** 80.9 °F, 77.6 °F<br>• **Temp rise since cast (peak):** +6.4 °F, +1.2 °F<br>• **Ambient temp (avg):** 69.1 °F, 70.0 °F<br>• **Moisture change:** +0.6%, +0.9%<br>• **Relative humidity (avg):** 95.2%, 95.0%<br>• **Resistivity change:** +3.4 Ω/ft, +8.6 Ω/ft<br>• **CO₂ (avg):** 422 ppm, 419 ppm | FR-10; SCRUM-190 | ☐ |

---

<a id="section-8-strength-estimates"></a>
### Section 8: 3/7/28-Day Strength Estimates

> *Examine 3-, 7-, and 28-day predictions, their basis, and adjustments.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 8.1 | On the Batch Detail screen, beneath the curing readings strip and Day 1 / Day 3 summary card, the following should appear:<br>• **3-day estimate card**<br>• **7-day estimate card**<br>• **28-day estimate card**<br>Review each estimate card. | **Estimate cards** should include the following values (3-, 7-, and 28-day estimates):<br>• **Break estimate:** 3,000 PSI, 3,975 PSI, 4,870 PSI<br>• **Actual break:** 3,000 PSI, 3,975 PSI, 5,149 PSI<br>• **Adjustment details:**<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 3,848 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 5,094 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 6,235 before breaks · Calibration v1”<br>• **How this was calculated** control | FR-14, FR-15 | ☐ |
> | 8.2 | Click **How this was calculated**. | Card expands to display calculation approach (based on cement type I, II, III): Type III: “ACI 209R-92 (TypeIII) using 9 qualifying specimens, with last curing readings at 72 hours after casting and reference strength 6200 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 4845 PSI).” Optional checks: VM-SYN-007 = Type I: “ACI 209R-92 (TypeI) using 9 qualifying specimens, with last curing readings at 72 hours after casting and reference strength 6100 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 6055 PSI).” VM-SYN-005 = Type II: “ACI 209R-92 (TypeII) using 9 qualifying specimens, with last curing readings at 72 hours after casting and reference strength 5700 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 5398 PSI).” | FR-15 | ☐ |

---

<a id="section-9-early-warnings"></a>
### Section 9: Early Warnings

> *Verify early warnings for a shortfall and for each kind of out-of-range input.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 9.1 | On the Batch Detail screen, review that batch for early warning indicators. | The screen should display the following warning indicators:<br>• A warning tag next to the batch number indicates the number of warnings (“2 warnings”)<br>• An early warning banner that the batch is at risk of missing its target:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:** “⚠ VM-SYN-006 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 4,870 PSI is 21.45% below the expected 6,200 PSI<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ The water to cement ratio is outside the normal range [0.4-0.6]<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Ambient temperature is outside the normal range [60-80]<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Expected final of 6,200 PSI is 67.6% above the 3,700 PSI typical for a w/cm of 0.603”<br>• Amber-colored text or markers for the values associated with the saved warnings.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ W/C = 0.603 ⚠<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **28-day estimate card** shows:<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ A projected 28-day strength shortfall: “⚠ 21% below target”<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ Estimate card is outlined in amber.<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ Estimated break font color is amber.<br>• The **Warnings** tab has a **2 warnings** badge, and its value column highlights warning values in amber alongside the warning messages.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Water/cementitious ratio (W/C):** “The water to cement ratio is outside the normal range [0.4-0.6]”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Ambient temperature:** “Ambient temperature is outside the normal range [60-80]” | FR-16 | ☐ |
> | 9.2 | Hover over the **D3-1** break marker (day 3) on the **VM-SYN-006 strength graph**. | Tooltip shows the break without a warning. The marker is not amber: the 3,000 PSI warning applies to 28-day breaks only (client-decisions §3, pending client confirmation).<br>**Exact:** “D3-1: 2,950 PSI at day 3” | FR-11; SCRUM-190 | ☐ |
> | 9.3 | In the curing strip, select **Ambient Temperature °F** from the **Display Measurement** dropdown. | The curing strip should display the curve for ambient temperature across all cylinders with a clear amber warning marker. | FR-12; SCRUM-190 | ☐ |
> | 9.4 | Hover over the orange marker on the **Ambient Temperature curve**. | Tooltip appears displaying the specific cylinder, the recorded ambient temperature, and the warning message.<br>**Exact:** “D3-1: 85 °F ⚠ Ambient temperature is outside the normal range [60-80]”. | FR-12; SCRUM-190 | ☐ |
> | 9.5 | Return to the Home Dashboard and select the `VM-SYN-009` row. | The screen should display the following warning indicators:<br>• A warning tag next to the batch number indicates the number of warnings (“1 warning”)<br>• An early warning banner identifying the pH condition:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ Early warning<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ pH is outside the normal range [7.0-7.5]”<br>• Amber-colored text or markers for the values associated with the saved warnings.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ pH · water / surface = 7.6 ⚠ / 7.1<br>• The **Warnings** tab has a **1 warning** badge, and its value column highlights the warning value in amber alongside the message.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “pH is outside the normal range [7.0-7.5]” | FR-16; SCRUM-190 | ☐ |
> | 9.6 | Return to the Home Dashboard and select the `VM-SYN-002` row (cold-cured). | The batch is at risk from its curing temperature alone, before any break is considered:<br>• A warning tag reading “63 warnings” (one per cold ambient reading)<br>• Early warning banner, with the ambient reason listed **once**:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ VM-SYN-002 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 5,169 PSI is 7.7% below the expected 5,600 PSI<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Ambient temperature is outside the normal range [60-80]”<br>• In the curing strip, **Ambient temperature** reads 37–41 °F, with every point amber. | FR-12, FR-16 | ☐ |
> | 9.7 | Return to the Home Dashboard and select the `VM-SYN-003` row (moisture loss). | The batch has no input warning, but its low 3- and 7-day breaks pull the projection down:<br>• No warning tag<br>• Early warning banner:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ VM-SYN-003 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 4,563 PSI is 23.95% below the expected 6,000 PSI”<br>• **Estimate cards:** 2,906 PSI, 3,784 PSI, 4,563 PSI, each “Adjusted using the 3- and 7-day breaks”, was 3,725 / 4,931 / 6,034 before breaks<br>• **28-day estimate card:** “⚠ 24% below target”<br>• In the curing strip, **Moisture** falls about 2 points between day 1 and day 3. | FR-14, FR-16 | ☐ |

---

<a id="section-10-accuracy-benchmark"></a>
### Section 10: Accuracy Benchmark

> *Verify the milestone's required agreement, separately from application testing.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 10.1 | Review the team's documented agreement with Vulcan on prediction accuracy. | Written confirmation exists identifying the agreed accuracy benchmark and how prediction error will be measured. | SLA M2, SC-4, SC-5 | ☐ |
>
> | Item | Current Status |
> |---|---|
> | Original 5% error objective | Documented in SLA     |
> | Guaranteed accuracy         | None                  |
> | Final benchmark             | Awaiting confirmation |
> | Agreed measurement method   | Awaiting confirmation |
> | Supporting client decision  | Not yet located       |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps**   | 36             |
| **Steps Passed**       | _____     |
| **Steps Failed**       | _____     |
| **Total Elapsed Time** | _____ min |

### Remediation / Notes on Failures

*Document any failed step numbers, error messages, and associated Jira issue keys here.*

| Step # | Error / Observation | Jira Key |
|---|---|---|
|     |     |     |
|     |     |     |
|     |     |     |

### Sign-off Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Test Operator   |     |     |     |
| Lead / Reviewer |     |     |     |
