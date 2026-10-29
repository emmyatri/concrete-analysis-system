# Milestone 2 Acceptance Script (Client Demo)

| Document Info | Details |
|---|---|
| **File** | `docs/m2-acceptance.md` |
| **Ticket** | SCRUM-190 (M2-85) |
| **Target Build** | `v0.1.0-m2` |
| **Target Execution Time** | 20-minute client walkthrough (target; execution time to be recorded below) |
| **Purpose** | Client-facing milestone demonstration and acceptance walkthrough. The comprehensive internal tests are in `docs/m2-qa-checklist.md`. |
| **Requirements Covered** | FR-11, FR-12, FR-14, FR-15, FR-16 |

---

## Contents

- [Execution Record](#execution-record)
- [Test Execution Matrix](#test-execution-matrix)
  1. [Strength Chart](#section-1-strength-chart)
  2. [Curing Reading Entry (Ambient Temperature Warning)](#section-2-curing-reading-entry)
  3. [Curing Strip](#section-3-curing-strip)
  4. [Day-1 / Day-3 Summary](#section-4-day-1-day-3-summary)
  5. [3/7/28-Day Strength Estimates](#section-5-strength-estimates)
  6. [Early Warnings](#section-6-early-warnings)
  7. [Accuracy Benchmark](#section-7-accuracy-benchmark)
- [Sign-Off & Acceptance Summary](#sign-off--acceptance-summary)

---

## Execution Record

| Field | Value |
|---|---|
| **Tester Name / Role** |  |
| **Date & Time Executed (T)** |  |
| **Build / Commit Hash** |  |
| **Execution Environment** | Windows 11 (clean test environment) |
| **Final Result** | ☐ PASS / ☐ FAIL |
| **Total Execution Time** | _____ minutes |

---

## Test Execution Matrix

> **Execution flow:** Run Sections 1–6 in order using seeded batch `VM-SYN-006`. Section 7 verifies the documented accuracy-benchmark agreement separately from the application walkthrough.
>
> **Sections 1–6 (VM-SYN-006; steps 6.5–6.6 use VM-SYN-002 and VM-SYN-003):** Strength chart → Enter a flagged ambient-temperature reading for D3-1 → Curing strip → Day-1/Day-3 summary → 3-, 7-, and 28-day strength estimates → Early warnings.
>
> **Section 7 (Accuracy Benchmark):** Verify the documented client agreement on the benchmark and measurement method; this is not a software-screen test.
>
> **Dependencies:** Sections 3–6 depend on the ambient-temperature reading saved in Section 2. When using the in-memory mock, start from seeded data and avoid refreshing between dependent steps.
>
> **Before you start (step 0, not counted):** Close the app, delete `%LOCALAPPDATA%\VulcanCBAS\vulcan.db`, then relaunch. The synthetic batches reload with their stored predictions. A database from before #83 shows "Not enough curing data yet" on every batch.
>
> **Real-app dependency:** Step 1.1 and the banner in 6.1 come from the early-warning service (SCRUM-181/182). If that isn't merged, Home shows "Early warnings aren't available yet" with mock mode off. Record those steps as **Blocked**, not **Fail**.

---

<a id="section-1-strength-chart"></a>
### Section 1: Strength Chart

> *Navigate to Batch Detail for VM-SYN-006 and verify the main visualization.*
>
> **Starting point:** Home Dashboard for Vulcan CBAS.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | On the Home dashboard, under **Batches at risk**, confirm that a **Top Reason** column appears. | The **Batches at risk** list displays a **Top Reason** column with the projected 28-day strength (PSI) and percentage shortfall relative to the expected strength.<br>**Example for VM-SYN-006:** “Projected 28-day strength of 4,733 PSI is 23.66% below the expected 6,200 PSI.”<br>Rows are listed largest shortfall first: VM-SYN-003, VM-SYN-006, VM-SYN-002. | FR-16 | ☐ |
> | 1.2 | On the Home dashboard, under **Batches at risk**, click row: `VM-SYN-006` | Opens full Batch Detail view, showing:<br>• High-level batch info (cast date, cement type, final weight, etc.)<br>• Composition and cement oxide percentages<br>• Projected strength over time<br>• Curing strip with Day 1 / Day 3 summary<br>• 3-, 7-, and 28-day estimates<br>• Cylinder and break data<br>• Curing readings<br>• Flagged data (warnings) | FR-10 | ☐ |
> | 1.3 | Review the **strength chart**. | **Strength Chart — Visual Verification**<br>Verify the chart includes a clearly labeled legend and axes, with appropriate units:<br>• **X-axis:** Days since cast<br>• **Y-axis:** Break strength (PSI)<br>Verify the chart displays the following, where applicable:<br>• **Predicted strength curve:** Initial prediction (before breaks) and adjusted prediction (after breaks).<br>• **Strength estimate markers:** 3-, 7-, and 28-day estimates.<br>• **Recorded breaks:** Individual break results, including warning markers for flagged values.<br>• **Mean by age:** Average recorded break strength at each applicable test age, joined into a line.<br>• **End labels** to the right of the plot: Expected final, “28-day est. #,### (−##%)” and, once 28-day breaks exist, “28-day actual #,###”.<br>• **Minimum break level:** A clearly identified horizontal reference line indicating the minimum acceptable break strength.<br>• **Expected final break:** A clearly identified reference line indicating the batch's expected final strength. | FR-11, FR-12, FR-14 | ☐ |
> | 1.4 | Hover over the **VM-SYN-006 predicted line**. | Tooltip should appear displaying the matching batch name and predicted PSI *after breaks* at day on x-axis.<br>**Exact:** “Predicted: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 1.5 | Hover over the **VM-SYN-006 before breaks line**. | Tooltip should appear displaying the matching batch name and predicted PSI *before breaks* at day on x-axis. No crash or unhandled error.<br>**Exact:** “Before breaks: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 1.6 | Hover over one of the **VM-SYN-006 estimate markers** (hollow circles). | Tooltip should appear displaying the estimated PSI *for a batch* at day on x-axis.<br>**Exact:** “Estimate: #,### PSI at day #” | FR-11, FR-14 | ☐ |
> | 1.7 | Hover over one of the **VM-SYN-006 break markers** (filled circles). | Tooltip should appear displaying the actual PSI *for a given cylinder* at day on x-axis.<br>**Exact:** “D#-#: #,### PSI at day #” | FR-10, FR-11, FR-12 | ☐ |
> | 1.8 | Hover over any open space in the **VM-SYN-006 graph**. | Vertical line tracking along the x-axis appears in curing strip and in the strength chart with a tooltip displaying the predicted PSI *after breaks* at day on x-axis. | FR-11, FR-12, FR-14 | ☐ |
> | 1.9 | Click **Expand** in the strength chart's header. | A larger copy of the chart opens over the page, titled "VM-SYN-006 · Strength over time", with bigger text and the same lines, markers and end labels. Press **Escape** (or click outside it) to close. | FR-11, FR-14 | ☐ |
> | 1.10 | In the curing strip, choose **Ambient temperature**, then click its **Expand**. | A larger curing chart opens on **Ambient temperature**, with day numbers on the x-axis, "Days since cast", and a y-axis title. Press **Escape** to close. | FR-12 | ☐ |

---

<a id="section-2-curing-reading-entry"></a>
### Section 2: Curing Reading Entry (Ambient Temperature Warning)

> *Enter an early curing reading for D3-1 to demonstrate saving and flagging an out-of-normal-range ambient temperature.*
>
> **Starting point:** Batch Detail for VM-SYN-006, under Curing Readings. Readings are entered in a grid, one row per reading. Each row has its own Save action (or press Enter).
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Click **Curing readings** in the upper right corner of the Batch Detail screen for batch: `VM-SYN-006`. | On opening:<br>• Batch and cylinder selection dropdowns are visible.<br>• **Batch:** VM-SYN-006 is selected.<br>• **Cylinder:** defaults to D3-1.<br>• **Cast line:** “Cast 06/21/26 08:00 · View batch”<br>• **Grid:** **D3-1 · readings**, with 7 saved readings and an empty new row (Row 8).<br>• **Time taken:** defaults to the current time. | FR-6 | ☐ |
> | 2.2 | Enter in Row 8:<br>• Time taken: `06/21/2026 14:00 (2:00 PM)`, 6 hours after the cast time shown (on an Eastern-time computer)<br>• Internal (°F): `79.5`<br>• Ambient (°F): `85`<br>• Moisture (%): `91.7`<br>• RH (%): `95.2`<br>• Resistivity (Ω/ft): `16.25`<br>• CO₂ (ppm): `417.7`<br>Click the row's **Save** (or press **Enter**). | • **Ambient** cell turns amber (orange border and background). No message shows yet; the grid only lists a warning once a flagged row is saved.<br>• Row 8 locks and reads 79.5 °F, 85.0 °F, 91.7%, 95.2%, 16.25 Ω/ft, 417.7 ppm with status **Saved**. A new empty Row 9 appears below. | FR-6; FR-16 (warning aspect) | ☐ |

---

<a id="section-3-curing-strip"></a>
### Section 3: Curing Strip

> *Explore curing measurements alongside the strength chart.*
>
> **Starting point:** Batch Detail for VM-SYN-006 via View Batch from the curing-reading entry screen.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Select **View Batch** in the upper right corner of the Curing readings screen. | Opens full Batch Detail view for VM-SYN-006, showing:<br>• High-level batch info (cast date, cement type, final weight, etc.)<br>• Composition and cement oxide percentages<br>• Projected strength over time<br>• Curing strip with Day 1 / Day 3 summary<br>• 3-, 7-, and 28-day estimates<br>• Cylinder and break data<br>• Curing readings<br>• Flagged data (warnings) | FR-10 | ☐ |
> | 3.2 | Under the strength chart with the predictive curve, review the **curing strip**. | **Curing Strip — Visual Verification**<br>Verify the chart includes a clearly labeled measurement selector and dropdown affordance, with appropriate units:<br>• **X-axis:** Aligns with the strength chart to clearly indicate days since cast.<br>• **Y-axis:** Proper units of measure for selected measurement (default is “Internal temperature (°F)”).<br>Verify the chart displays the following, where applicable:<br>• **Average readings curve:** The average for the selected measurement across all batch cylinders.<br>• **Shaded band along the curve:** The minimum and maximum values for the selected measurement across all batch cylinders.<br>• Day 1 and Day 3 vertical markers.<br>• Caption based on cylinder count:<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **For the seeded batch:** “Line = average of 9 cylinders · band = lowest to highest · readings so far: 0–672h” (3-day cylinders stop at 72 h, 7-day at 168 h, 28-day at 672 h). | FR-12 | ☐ |
> | 3.3 | Hover over the **curing strip line**. | Tooltip should appear displaying the average value for the selected measurement across all cylinders at day on x-axis. | FR-12 | ☐ |
> | 3.4 | Hover over any open space in the **curing strip**. | Vertical line tracking along the x-axis appears along both the strength chart and curing strip. | FR-12 | ☐ |
> | 3.5 | Select each measurement from the **Display Measurement** dropdown. | The y-axis for each chart should update to reflect measurement ranges and units relative to the selected measurement:<br>• **Internal Temperature** = °F<br>• **Ambient Temperature** = °F<br>• **Moisture** = %<br>• **Relative Humidity** = %<br>• **Resistivity** = Ω/ft<br>• **CO₂** = ppm<br>Confirm that each chart renders a band for maximum and minimum values. Ambient temperature may not display a band if all cylinders record the same ambient temperature. | FR-12 | ☐ |

---

<a id="section-4-day-1-day-3-summary"></a>
### Section 4: Day-1 / Day-3 Summary

> *Review the summarized curing conditions and changes.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | On the Batch Detail screen, review the **Day 1 / Day 3 Summary** card to the right of the curing strip. | The Day 1/ Day 3 Summary should include the following values for Day 1 and Day 3 columns. Values are expected to differ from the strip.<br>• **Internal temp (avg):** 80.9 °F, 77.6 °F<br>• **Temp rise since cast (peak):** +6.4 °F, +1.2 °F<br>• **Ambient temp (avg):** 69.1 °F, 70.0 °F<br>• **Moisture change:** +0.6%, +0.9%<br>• **Relative humidity (avg):** 95.2%, 95.0%<br>• **Resistivity change:** +3.4 Ω/ft, +8.6 Ω/ft<br>• **CO₂ (avg):** 422 ppm, 419 ppm | FR-10; SCRUM-190 | ☐ |

---

<a id="section-5-strength-estimates"></a>
### Section 5: 3/7/28-Day Strength Estimates

> *Examine 3-, 7-, and 28-day estimates, their calculation basis, and adjustments.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | On the Batch Detail screen, review the **3-day**, **7-day**, and **28-day estimate cards** beneath the curing strip and Day 1 / Day 3 Summary. | **Estimate card** should include the following values (3-,7-,28-day estimates):<br>• **Break estimate:** 3,129 PSI, 3,969 PSI, 4,733 PSI<br>• **Actual break:** 3,129 PSI, 3,969 PSI, 4,994 PSI<br>• **Difference:** “Estimate matched the actual break” on the 3- and 7-day cards; “Estimate was 261 PSI (5.2%) below the actual break” on the 28-day card<br>• **Adjustment details:**<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 3,848 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 5,048 before breaks · Calibration v1”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ “Adjusted using the 3- and 7-day breaks · was 6,212 before breaks · Calibration v1”<br>• **How this was calculated** affordance | FR-14, FR-15; SLA M2 Acceptance | ☐ |
> | 5.2 | Click **How this was calculated**. | The card expands to display the calculation approach for VM-SYN-006 (**Type III**). Optional reference checks for Type I and Type II (outside the timed walkthrough) are also listed:<br>**Type III:**<br>“ACI 209R-92 (TypeIII) using 9 qualifying specimens, with last curing readings at 72–672 hours after casting and reference strength 6200 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 4659 PSI).”<br>**Optional checks:**<br>**VM-SYN-007 = Type I:**<br>“ACI 209R-92 (TypeI) using 9 qualifying specimens, with last curing readings at 72–672 hours after casting and reference strength 6100 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 5941 PSI).”<br>**VM-SYN-005 = Type II:**<br>“ACI 209R-92 (TypeII) using 9 qualifying specimens, with last curing readings at 72–672 hours after casting and reference strength 5700 PSI. Equivalent curing ages were averaged across qualifying specimens. Beyond each specimen's last recorded temperature, the average of its available curing temperatures was assumed. Re-fit through the average 3- and 7-day breaks (reference strength 6119 PSI).” | FR-15 | ☐ |

---

<a id="section-6-early-warnings"></a>
### Section 6: Early Warnings

> *Verify the projected shortfall warning and the documented out-of-normal-range conditions.*
>
> **Starting point:** Batch Detail for VM-SYN-006.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 6.1 | On the Batch Detail screen for VM-SYN-006, review the early-warning indicators. | The screen should show the following warning indicators:<br>• A warning tag next to the batch number indicating the number of warnings (“2 warnings”)<br>• An early warning banner that the batch is at risk of missing its target:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ VM-SYN-006 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 4,733 PSI is 23.66% below the expected 6,200 PSI<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ The water to cement ratio is outside the normal range [0.4-0.6]<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Ambient temperature is outside the normal range [60-80]<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Expected final of 6,200 PSI is 67.6% above the 3,700 PSI typical for a w/cm of 0.603”<br>• Amber text or markers for the values associated with the saved warnings.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ W/C = 0.603 ⚠<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **28-day estimate card** shows:<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ A projected 28-day strength shortfall: “⚠ 24% below target”<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ Estimate card is outlined in amber.<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▪ Estimated break font color is amber.<br>• The **Warnings** tab shows a **2 warnings** badge and highlights flagged values in amber alongside their warning messages.<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Water/cementitious ratio (W/C):** “The water to cement ratio is outside the normal range [0.4-0.6]”<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ **Ambient temperature:** “Ambient temperature is outside the normal range [60-80]” | FR-16 | ☐ |
> | 6.2 | Hover over the **D3-1** break marker (day 3) on the **VM-SYN-006 strength graph**. | Tooltip shows the break without a warning.<br>**Exact:** “D3-1: 3,148 PSI at day 3” | FR-11; SCRUM-190 | ☐ |
> | 6.3 | In the curing strip, select **Ambient Temperature °F** from the **Display Measurement** dropdown. | The curing strip should display the curve for ambient temperature across all cylinders with a clear amber warning marker. | FR-12; SCRUM-190 | ☐ |
> | 6.4 | Hover over the amber marker on the **Ambient Temperature** curve. | Tooltip should appear displaying the specific cylinder, the recorded ambient temperature, and the warning message.<br>**Exact:** “D3-1: 85 °F ⚠ Ambient temperature is outside the normal range [60-80]”. | FR-12; SCRUM-190 | ☐ |
> | 6.5 | Return to the Home Dashboard and select the `VM-SYN-002` row (cold-cured). | The batch is at risk from its curing temperature alone, before any break is considered:<br>• A warning tag reading “150 warnings” (one per cold ambient reading)<br>• Early warning banner, with the ambient reason listed **once**:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ VM-SYN-002 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 5,028 PSI is 10.21% below the expected 5,600 PSI<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Ambient temperature is outside the normal range [60-80]”<br>• In the curing strip, **Ambient temperature** reads 42–46 °F, with every point amber.<br>• The 3-day breaks (about 1,900–2,000 PSI) are **not** flagged: the 3,000 PSI warning applies to 28-day breaks only (client-decisions §3, pending client confirmation). | FR-12, FR-16 | ☐ |
> | 6.6 | Return to the Home Dashboard and select the `VM-SYN-003` row (moisture loss). | The batch has no input warning, but its low 3- and 7-day breaks pull the projection down:<br>• No warning tag<br>• Early warning banner:<br>&nbsp;&nbsp;&nbsp;&nbsp;**Exact:**<br>&nbsp;&nbsp;&nbsp;&nbsp;“⚠ VM-SYN-003 is at risk of missing its target<br>&nbsp;&nbsp;&nbsp;&nbsp;◦ Projected 28-day strength of 4,503 PSI is 24.95% below the expected 6,000 PSI”<br>• **Estimate cards:** 3,002 PSI, 3,786 PSI, 4,503 PSI, each “Adjusted using the 3- and 7-day breaks”, was 3,725 / 4,874 / 6,005 before breaks<br>• **28-day estimate card:** “⚠ 25% below target”<br>• In the curing strip, **Moisture** falls about 2 points between day 1 and day 3. | FR-14, FR-16 | ☐ |

---

<a id="section-7-accuracy-benchmark"></a>
### Section 7: Accuracy Benchmark

> *Verify the milestone's required agreement, separately from application testing.*
>
> **Starting point:** Documented Vulcan/client decision or written confirmation.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 7.1 | Review the team's documented agreement with Vulcan on prediction accuracy. | Written confirmation exists identifying the agreed accuracy benchmark and how prediction error will be measured. | SLA M2, SC-4, SC-5 | ☐ |
>
> **Accuracy agreement status (as documented at drafting time):**
>
> | Item | Current Status |
> |---|---|
> | Original 5% error objective | Documented in SLA |
> | Guaranteed accuracy | None |
> | Final benchmark | Awaiting confirmation |
> | Agreed measurement method | Awaiting confirmation |
> | Supporting client decision | Not yet located |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps** | 27 (26 application checks + 1 separate documentary verification) |
| **Steps Passed** | _____ |
| **Steps Failed** | _____ |
| **Total Elapsed Time** | _____ min |

### Remediation / Notes on Failures

*Document any failed step numbers, error messages, and associated Jira issue keys here.*

| Step # | Error / Observation | Jira Key |
|---|---|---|
| | | |
| | | |
| | | |

### Sign-off Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Test Operator | | | |
| Lead / Reviewer | | | |
