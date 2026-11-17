# Milestone 3 Acceptance Script (Client Demo)

| Document Info | Details |
|---|---|
| **File** | `docs/m3-acceptance.md` |
| **Ticket** | SCRUM-199 (M3-82) |
| **Target Build** | `v0.1.0-m3` |
| **Target Execution Time** | 20-minute client walkthrough (target; execution time to be recorded below) |
| **Purpose** | Client-facing milestone demonstration. The comprehensive internal tests are in `docs/m3-qa-checklist.md`. |
| **Requirements Covered** | FR-17, FR-19, SC-4, NFR-3 |

---

## Contents

- [Execution Record](#execution-record)
- [Test Execution Matrix](#test-execution-matrix)
  1. [Moisture as % of Sample Weight](#section-1-moisture)
  2. [5% Accuracy and Calibration on Cards](#section-2-accuracy-cards)
  3. [Batch Report, Print, and PDF](#section-3-report)
  4. [Export and Cloud (Desktop)](#section-4-export-cloud)
  5. [SLA Acceptance Criterion](#section-5-sla)
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

> **Execution flow:** Sections 1–3 use seeded `VM-SYN-006` (plus one moisture save). Section 4 is desktop-only. Section 5 is the SLA check, not extra clicks.
>
> **Happy path only.** Edge cases (reject 16% moisture, enter an 800 PSI 3-day break, Wi-Fi off) live in `docs/m3-qa-checklist.md`.
>
> **Before you start (step 0, not counted):** Close the app, delete `%LOCALAPPDATA%\VulcanCBAS\vulcan.db`, then relaunch.
>
> **Desktop vs browser:** Save as PDF / Export / Cloud need the Windows desktop host. On `npm start`, demo Print report and skip Section 4 as **Blocked**, not Fail.
>
> **Do not demo Day-3 / Day-7 abandon recommendations on a synthetic.** No seed batch is below 1,200 PSI (3-day) or 2,100 PSI (7-day). If a lead prepared a low-break fixture, show the recommendation under the banner; otherwise say the copy is in the build and skip.

---

<a id="section-1-moisture"></a>
### Section 1: Moisture as % of Sample Weight

> *Show that moisture is a percent of sample weight with a 2–4% normal band.*
>
> **Starting point:** Home Dashboard → `VM-SYN-006` → **Curing readings**.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | Open **Curing readings** for `VM-SYN-006`. | Column **Moisture (% of sample weight)**. Hint includes **0–15% (normal 2–4)**. | FR-17 | ☐ |
> | 1.2 | On a new row, enter a complete reading with Moisture `5.0`. Save. | Row saves. Moisture is amber (outside 2–4%, still allowed). | FR-17 | ☐ |
> | 1.3 | **View batch** → curing strip → **Moisture**. | Axis title **Moisture (% of sample weight)**. Scale is a few percent, not 0–100. | FR-17 | ☐ |

---

<a id="section-2-accuracy-cards"></a>
### Section 2: 5% Accuracy and Calibration on Cards

> *SC-4 on the 28-day card; stored re-fit on the basis line.*
>
> **Starting point:** Batch Detail for `VM-SYN-006`.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Point at the **3-day** and **7-day** estimate cards. | No **5% target** line on those cards. 7-day estimate **3,969 PSI**. | SC-4, FR-14 | ☐ |
> | 2.2 | Point at the **28-day** card. | Estimate ~**4,733 PSI**. Actual **4,994 PSI** — **261 PSI / 5.2% below**. Copy: **Outside the 5% target**. | SC-4 | ☐ |
> | 2.3 | Point at the basis / calibration line. | **Adjusted using the 3- and 7-day breaks** (Calibration v1). | FR-14 | ☐ |

---

<a id="section-3-report"></a>
### Section 3: Batch Report, Print, and PDF

> **Starting point:** Same batch → **View batch report**.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Open the report. | Seven sections: Batch Record, Mix Composition, Cement Oxide Analysis, Specimens and Break Results, Strength Development, Strength Predictions and Calibration, Warnings and Recommendations. | FR-19 | ☐ |
> | 3.2 | Section 06. | Expected **6,200 PSI**. Projected ~**4,733 PSI**. Calibration version shown. | FR-19 | ☐ |
> | 3.3 | Click **Print report**. | Print dialog. Cancel is fine. | FR-19 | ☐ |
> | 3.4 | **Desktop:** **Save as PDF** and open the file. | PDF has the same sections; toolbar buttons are not on the page. **Browser:** button is absent — skip. | FR-19 | ☐ |

---

<a id="section-4-export-cloud"></a>
### Section 4: Export and Cloud (Desktop)

> *Skip entirely on `npm start` / Mac browser.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | **Export data** for VM-SYN-006. | Spreadsheet / CSV opens; batch number and ~4,733 PSI projection match the report. | FR-19 | ☐ |
> | 4.2 | **Settings** → **Cloud folder** → **Choose folder**. | Path is stored. | FR-19 | ☐ |
> | 4.3 | Back on the report, **Save to cloud**. | PDF + export land under `CBAS\VM-SYN-006` in that folder. | FR-19, NFR-3 | ☐ |

---

<a id="section-5-sla"></a>
### Section 5: SLA Acceptance Criterion

> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | Confirm with the client: estimates + calibration (Section 2), printable/exportable report (Sections 3–4), and the 5% 28-day display (SYN-006 outside target). No guaranteed-accuracy claim. | Verbal / written acceptance of FR-17, FR-19, SC-4. | FR-17, FR-19, SC-4 | ☐ |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps**   | 14            |
| **Steps Passed**       | _____         |
| **Steps Failed**       | _____         |
| **Steps Blocked**      | _____         |
| **Total Elapsed Time** | _____ min     |

### Remediation / Notes on Failures

*Document any failed or blocked step numbers, error messages, and associated Jira issue keys here.*

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
