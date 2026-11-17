# Milestone 3 QA Checklist (Internal)

| Document Info | Details |
|---|---|
| **File**                 | `docs/m3-qa-checklist.md`                                                                |
| **Ticket**               | SCRUM-199 (M3-82)                                                                        |
| **Target Build**         | `v0.1.0-m3`                                                                              |
| **Purpose**              | Full internal check for the M3 chunk. The client demo script is `docs/m3-acceptance.md`. |
| **Requirements Covered** | FR-17, FR-19, SC-4, NFR-3                                                                |

---

## Contents

- [Execution Record](#execution-record)
- [Test Execution Matrix](#test-execution-matrix)
  1. [Moisture as % of Sample Weight](#section-1-moisture)
  2. [3-Day / 7-Day Break Bands and Recommendations](#section-2-break-bands)
  3. [5% Accuracy on Estimate Cards](#section-3-accuracy-cards)
  4. [Stored Re-Fit / Calibration](#section-4-calibration)
  5. [Batch Report Content](#section-5-report-content)
  6. [Print and Save as PDF](#section-6-print-pdf)
  7. [Export Spreadsheet](#section-7-export)
  8. [Cloud Folder](#section-8-cloud)
  9. [Offline PDF](#section-9-offline)
  10. [SLA Acceptance Criterion](#section-10-sla)
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
| **Total Execution Time**     | _____ minutes                       |

---

## Test Execution Matrix

> **Scope:** Only the M3 chunk. Do **not** re-run the M1 batch-entry / cylinder scripts or the M2 strength-chart / curing-strip / early-warning scripts unless a step below depends on them. Record a missing build as **Blocked**, not **Fail**.
>
> **Execution flow:** Sections 1–2 use a technician-created batch (or `VM-2026-0142` in mock). Sections 3–8 use seeded `VM-SYN-006`. Section 9 needs the Windows desktop host. Section 10 is documentation, not a screen test.
>
> **Host:** Print, Save as PDF, Export data, Save to cloud, folder picker, and Wi-Fi-off PDF only exist in the **WPF / WebView2 desktop host** (`isDesktopHost`). `npm start` in a browser shows **Print report** only. Record desktop-only steps as **Blocked** if you are on the browser build.
>
> **Before you start (step 0, not counted):** Close the app, delete `%LOCALAPPDATA%\VulcanCBAS\vulcan.db`, then relaunch so synthetic batches reload with stored predictions.
>
> **Known gap (break-band recommendations):** No seeded synthetic batch has a 3-day average below 1,200 PSI or a 7-day average below 2,100 PSI, so the Day-3 / Day-7 recommendation copy is **not** visible until you enter a low break in Section 2. That is a data fixture gap, not a missing UI.
>
> **Known gap (6% accuracy toggle):** There is no Settings or UI control to change the 5% accuracy target to 6%. SC-4 at 6% is covered by unit tests only. Do not Fail the product for a missing toggle.

---

<a id="section-1-moisture"></a>
### Section 1: Moisture as % of Sample Weight

> *Confirm moisture is labeled and validated as a percent of sample weight (0–15% accepted, 2–4% normal), not as a 0–100 humidity-style field.*
>
> **Starting point:** Home Dashboard. Use mock `VM-2026-0142`, or create that batch in the real app the same way as `docs/m2-qa-checklist.md` (New batch, cylinders A1–A6).
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | Open **Curing readings** for `VM-2026-0142`, cylinder **A1**. | Column header and field name read **Moisture (% of sample weight)**.<br>Hint includes **0–15% (normal 2–4)**. | FR-17 | ☐ |
> | 1.2 | In the new row enter a complete reading with Moisture `3.0` (inside 2–4). Save. | Row saves. Moisture is **not** amber. | FR-17 | ☐ |
> | 1.3 | Enter another reading with Moisture `5.0` (outside 2–4, still ≤ 15). Save. | Row saves. Moisture cell is amber (Warn).<br>Warning text: **Moisture content is outside the normal range of 2–4% of sample weight.** | FR-17 | ☐ |
> | 1.4 | Enter Moisture `16` (above 15). Try to save. | Save is **rejected**. Message: moisture must be within **[0-15]** (wording may say “accepted range” or “the range”). | FR-17 | ☐ |
> | 1.5 | Enter Moisture `-1`. Try to save. | Save is **rejected**. Same 0–15 reject. | FR-17 | ☐ |
> | 1.6 | Open Batch Detail → curing strip → choose **Moisture**. | Y-axis title: **Moisture (% of sample weight)**.<br>Scale is a few percentage points (about 2–4%), **not** 0–100. | FR-12, FR-17 | ☐ |
> | 1.7 | Open **View batch report** for this batch (or skip to Section 5 if you prefer SYN-006). | Warning / field labels use **Moisture (% of sample weight)**, not a generic “Moisture (%)” as the canonical name. | FR-17, FR-19 | ☐ |
> | 1.8 | **Observation only:** on Batch Detail, the compact curing-readings table header still says **Moisture (%)**. | Record whether leads want this aligned with the full label. Do **not** Fail M3-82 for the abbreviated column unless a lead says otherwise. | — | ☐ |

---

<a id="section-2-break-bands"></a>
### Section 2: 3-Day / 7-Day Break Bands and Recommendations

> *Client bands: average 3-day break below 40% of 3,000 PSI (1,200 PSI) → watch the 7-day; average 7-day below 70% of 3,000 PSI (2,100 PSI) → consider abandoning the mix. These are Warn-level early-warning reasons with a recommendation line.*
>
> **Starting point:** Same batch as Section 1. You need at least one Day-3 cylinder with enough curing that estimates exist (three 24h+ readings, as in M2 QA Sections 1–3), then a **low** 3-day break.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Confirm no seeded row on Home **Batches at risk** already shows a Day-3/Day-7 abandon recommendation (hover **Top reason**). | Seeded synthetics (VM-SYN-001…006) do **not** trigger these bands. If none appear, that is expected. | FR-16 | ☐ |
> | 2.2 | After the batch has an initial prediction, **Enter break results** for A1 (Day 3) at **800 PSI**. Save. | Break saves. Average 3-day is below 1,200 PSI. | FR-16 | ☐ |
> | 2.3 | Open Batch Detail for that batch. | Early-warning banner lists a 3-day reason. Under that reason, recommendation:<br>**Watch this batch at the 7-day break.**<br>`[data-testid=early-warning-recommendation]` | FR-16 | ☐ |
> | 2.4 | Home → **Batches at risk** → hover the **Top reason** cell for this batch. | Tooltip (`title`) shows the same recommendation when this reason is the top one. | FR-16 | ☐ |
> | 2.5 | Add a Day-7 break of **1,500 PSI** on a Day-7 cylinder of the same batch (after that cylinder has curing). | Banner recommendation for the 7-day reason:<br>**Consider abandoning this mix.** | FR-16 | ☐ |

---

<a id="section-3-accuracy-cards"></a>
### Section 3: 5% Accuracy on Estimate Cards

> *SC-4 shows on the 28-day estimate card once 28-day actuals exist. Seeded `VM-SYN-006` is already outside 5%.*
>
> **Starting point:** Home → **Batches at risk** → `VM-SYN-006`.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Open `VM-SYN-006`. Read the 3-day and 7-day estimate cards. | Neither card shows **Outside the 5% target** or **Within the 5% target**. The 5% verdict is **28-day only**. | SC-4 | ☐ |
> | 3.2 | Read the **28-day** estimate card. | **Exact (seeded):**<br>• Estimate about **4,733 PSI**<br>• Actual **4,994 PSI** — **261 PSI / 5.2% below** the estimate<br>• **Outside the 5% target**<br>Card also still shows the M2 shortfall vs expected 6,200 PSI. | SC-4, FR-14 | ☐ |
> | 3.3 | Confirm the 7-day card number. | 7-day estimate **3,969 PSI** (sanity check that you are on the post-#83 seed, not an old DB). | FR-14 | ☐ |
> | 3.4 | Look in **Settings** (and anywhere else) for a control to change 5% → 6%. | **No such control.** Record this step **Blocked / N/A** (unit-tested only). Do not Fail. | SC-4 | ☐ |
> | 3.5 | Open an on-track batch with 28-day breaks if one exists (`VM-SYN-001`). | 28-day card does **not** show **Outside the 5% target**. | SC-4 | ☐ |

---

<a id="section-4-calibration"></a>
### Section 4: Stored Re-Fit / Calibration

> *Cards and the report must show the stored basis and calibration version, not a live recompute of the method in the UI.*
>
> **Starting point:** Batch Detail for `VM-SYN-006`.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | Read the **basis** line on each estimate card. | **Adjusted using the 3- and 7-day breaks** (and **Calibration v1** appears on the cards where that copy is wired). | FR-14, FR-19 | ☐ |
> | 4.2 | Click **View batch report**. | Section **06 Strength Predictions and Calibration** lists Day 3 / 7 / 28 estimates, an **Adjustment** column, and **Calibration version: v1** (or the stored version string). | FR-19 | ☐ |
> | 4.3 | Open `VM-2026-0142` (or any batch with **no** breaks and **no** 24h curing). | Cards: **Not enough curing data yet** and/or **Before any breaks · Calibration v1** once a prediction exists without breaks. | FR-14 | ☐ |
> | 4.4 | Do **not** expect the UI to re-fit coefficients live. | Changing a break updates the stored prediction after save; there is no “recalculate method” button. | FR-14 | ☐ |

---

<a id="section-5-report-content"></a>
### Section 5: Batch Report Content

> *FR-19 printable report. Route: `/batches/VM-SYN-006/report` or **View batch report** from Batch Detail.*
>
> **Starting point:** Batch Detail for `VM-SYN-006`.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | Click **View batch report**. | Report loads for VM-SYN-006. **Back to batch** returns to detail. | FR-19 | ☐ |
> | 5.2 | Confirm the seven numbered sections exist. | **01** Batch Record<br>**02** Mix Composition<br>**03** Cement Oxide Analysis<br>**04** Specimens and Break Results<br>**05** Strength Development<br>**06** Strength Predictions and Calibration<br>**07** Warnings and Recommendations | FR-19 | ☐ |
> | 5.3 | Section 01. | Batch number, SYNTHETIC badge, cast, cement type, expected final **6,200 PSI**, created-by metadata. | FR-19 | ☐ |
> | 5.4 | Section 04. | Cylinders and recorded breaks present. If a batch had zero breaks it would say **No breaks recorded yet** — SYN-006 should **not** show that empty state. | FR-19 | ☐ |
> | 5.5 | Section 05. | Strength chart is on the page (print-mode, no Expand needed). | FR-19 | ☐ |
> | 5.6 | Section 06. | Expected 28-day **6,200 PSI**. Projected 28-day about **4,733 PSI**. Adjustment / calibration version filled in. | FR-19, SC-4 | ☐ |
> | 5.7 | Section 07. | Strength risk assessment plus input warnings (w/cm and any moisture / ambient flags). Recommendations appear when the batch has them. | FR-16, FR-19 | ☐ |
> | 5.8 | Open report for a batch with no breaks (e.g. `VM-2026-0142` before Section 2). | Section 04 shows **No breaks recorded yet**. Section 06 may show **No strength predictions** / insufficient curing. | FR-19 | ☐ |

---

<a id="section-6-print-pdf"></a>
### Section 6: Print and Save as PDF

> *Print is on every host. Save as PDF is desktop-only.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 6.1 | On the SYN-006 report, confirm **Print report** is enabled. | Button is present on browser **and** desktop. | FR-19 | ☐ |
> | 6.2 | Click **Print report**. | System print dialog opens. Cancel is fine; no crash. | FR-19 | ☐ |
> | 6.3 | **Desktop only:** click **Save as PDF**. Pick a folder. | A PDF is written. Open it: sections 01–07 still present; action buttons are **not** in the printed page. | FR-19 | ☐ |
> | 6.4 | **Browser (`npm start`):** confirm **Save as PDF**, **Export data**, and **Save to cloud** are **absent**. | Expected on the Angular-only host. Record **Blocked** for 6.3 / 7.x / 8.x if you cannot use Windows desktop. | FR-19 | ☐ |

---

<a id="section-7-export"></a>
### Section 7: Export Spreadsheet

> **Desktop only.**
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 7.1 | On the SYN-006 report click **Export data**. Save the file. | A spreadsheet / CSV downloads without error. | FR-19 | ☐ |
> | 7.2 | Open the file. | Batch identity, mix, oxides, specimens/breaks, curing, and prediction fields are present. Values match the on-screen report for this batch (spot-check batch number, expected 6,200 PSI, projected ~4,733 PSI). | FR-19 | ☐ |
> | 7.3 | Export a second batch (`VM-SYN-001`). | File is for **that** batch, not a leftover SYN-006 export. | FR-19 | ☐ |

---

<a id="section-8-cloud"></a>
### Section 8: Cloud Folder

> **Desktop only.** Settings → Cloud folder. Save to cloud writes PDF + export under `CBAS\<batch number>` in the chosen sync folder. Nothing uploads until you click **Save to cloud**.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 8.1 | Open **Settings**. | Card **Cloud folder**. Hint mentions OneDrive / Google Drive and `CBAS\<batch number>`. | FR-19, SC-6 | ☐ |
> | 8.2 | With no folder set. | Value **Not set**. Button **Choose folder**. | FR-19 | ☐ |
> | 8.3 | Choose a real folder (local is enough for this test). | Path is shown. Button becomes **Change folder**. **Clear** appears. | FR-19 | ☐ |
> | 8.4 | Return to the SYN-006 report → **Save to cloud**. | PDF and export appear under that folder in `CBAS\VM-SYN-006` (or the documented subfolder name). | FR-19, NFR-3 | ☐ |
> | 8.5 | **Save to cloud** with folder **Not set**, or with a path that no longer exists. | Error / warning, not a silent success. Settings can show **This folder can't be found.** | FR-19 | ☐ |

---

<a id="section-9-offline"></a>
### Section 9: Offline PDF

> **Desktop only. NFR-3.**
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 9.1 | With the report already loaded, turn **Wi-Fi / Ethernet off**. | App stays on the report (NFR-3 offline). | NFR-3 | ☐ |
> | 9.2 | Click **Save as PDF** (and **Export data** if time). | Files still write to disk. No “network required” error. Turn the network back on when done. | NFR-3, FR-19 | ☐ |

---

<a id="section-10-sla"></a>
### Section 10: SLA Acceptance Criterion

> *Verify the written SLA items this milestone claims. Not a UI click-path.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 10.1 | Confirm the product can show 3- / 7- / 28-day estimates with stored calibration (Sections 3–4). | Met in the build you just walked. | FR-17 (estimates) | ☐ |
> | 10.2 | Confirm a printable / exportable batch report exists (Sections 5–8). | Met on desktop; browser is print-only. | FR-19 | ☐ |
> | 10.3 | Confirm the 5% 28-day accuracy display (Section 3) and that **no guaranteed accuracy** is claimed in client-facing copy. | Card says **Outside the 5% target** on SYN-006. There is still **no** guaranteed-accuracy badge. Written client confirmation of SC-4 measurement method: record whatever `docs/` / the SLA currently say (M2 still had this **awaiting confirmation**). | SC-4 | ☐ |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps**   | 47            |
| **Steps Passed**       | _____         |
| **Steps Failed**       | _____         |
| **Steps Blocked**      | _____         |
| **Total Elapsed Time** | _____ min     |

### Remediation / Notes on Failures

*Document any failed or blocked step numbers, error messages, and associated Jira issue keys here. Use **Blocked** when the host or fixture is missing (Mac/browser, no low-break seed, no 6% toggle).*

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
