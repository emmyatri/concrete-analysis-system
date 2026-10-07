# Milestone 1 Acceptance Script (Client Demo)

| Document Info | Details |
|---|---|
| **File** | `docs/m1-acceptance.md` |
| **Ticket** | SCRUM-126 (M1-83) |
| **Target Build** | `v0.1.0-m1` |
| **Target Execution Time** | < 20 minutes |
| **Purpose** | Client-facing walkthrough for the live demo and final install test. The full internal check is `docs/m1-qa-checklist.md`. |
| **Requirements Covered** | FR-1–FR-10, FR-20–FR-22, NFR-1, NFR-2, NFR-4, NFR-6 |

---

## Contents

- [Execution Record](#execution-record)
- [Test Execution Matrix](#test-execution-matrix)
  1. [Sign-In & Home](#section-1-sign-in--home)
  2. [New Batch](#section-2-new-batch)
  3. [Cylinders](#section-3-cylinders)
  4. [Break Results](#section-4-break-results)
  5. [Curing Readings](#section-5-curing-readings)
  6. [Find the Batch](#section-6-find-the-batch)
  7. [Batch Detail & Audit Trail](#section-7-batch-detail--audit-trail)
  8. [Synthetic Data](#section-8-synthetic-data)
  9. [Local Database & Application Logging](#section-9-local-database--application-logging)
- [Sign-Off & Acceptance Summary](#sign-off--acceptance-summary)

---

## Execution Record

| Field | Value |
|---|---|
| **Tester Name / Role** | |
| **Date & Time Executed (T)** | |
| **Build / Commit Hash** | |
| **Execution Environment** | Windows 11 (clean test environment) |
| **Final Result** | ☐ PASS / ☐ FAIL |
| **Total Execution Time** | _____ minutes |

---

## Test Execution Matrix

> **Flow:** Run the sections in order. This is the happy path: each screen's entry is what the next screen builds on. Rejections and edge cases are in `docs/m1-qa-checklist.md`.
>
> **New Batch** (creates `VM-2026-M1-001`) → **Cylinders** (C1, C2) → **Break Results** (C1, plus C2 saved with a warning) → **Curing Readings** (C1) → Find, Detail, Synthetic, Database.
>
> **Dates:** All dates are relative to the day the test is run (**T**). Timeline: cast **T−3 8:00 AM** → curing reading on C1 **T−2 10:00 AM** → C1 broken **T** (its 3-day age; the app records the time you click Save) → C2 broken early on **T** (before its 7-day age) at a low strength, so it saves with a warning. Batch Detail shows dates as `MM/dd/yy HH:mm` (24-hour). Example if run on 10/06/2026: T−4 = 10/02, T−3 = 10/03, T−2 = 10/04, T = 10/06. Write the actual dates used in the Execution Record.

### Section 1: Sign-In & Home

> *Launch the app, sign in, and see who is working.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | Launch the Vulcan CBAS application from the desktop shortcut or Start Menu and verify Technician login screen appears. | Application window opens cleanly to the Technician login screen for technician name entry. | NFR-1 | ☐ |
> | 1.2 | Enter the technician name and continue (e.g., "*Don Murphy*"). | Login is successful and screen displays Home dashboard. | FR-21 | ☐ |
> | 1.3 | Verify the operator lands on Home and the top header displays the logged-in operator session. | Current operator is clearly shown for record attribution:<br>• **Upper right:** avatar with the technician's initials (`DM`) next to their full name as entered (`Don Murphy`)<br>• **Home dashboard:** greeting with the full name, based on time of day (e.g., "*Good morning, Don Murphy*"; afternoon and evening also apply) | FR-21 | ☐ |

---

### Section 2: New Batch

> *Create batch `VM-2026-M1-001`.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Navigate to **New batch** via the side navigation menu. Under **Batch**, enter valid batch metadata:<br>• Batch number: `VM-2026-M1-001`<br>• Cast date/time: **T−3** at `8:00 AM`<br>• Final weight (lb): `4,050`<br>• Expected final break (PSI): `6,200`<br>• Cement type: `Type I`<br>• Mix water pH: `7.2` | New batch screen appears and New batch tab is active in the side navigation menu. All metadata fields accept input with neutral valid styling. | FR-1, FR-2, FR-7 | ☐ |
> | 2.2 | Under **Cement oxides**, enter:<br>• Alumina Al₂O₃ (%): `5.1`<br>• Iron oxide Fe₂O₃ (%): `3.2`<br>• Magnesium oxide MgO (%): `2.1`<br>• Sulfur trioxide SO₃ (%): `2.4` | No warning or error banners appear; chemistry validation card reflects valid composition. | FR-3 | ☐ |
> | 2.3 | Under **Composition**, enter:<br>• Cement (%): `10`<br>• Water (%): `20`<br>• Coarse aggregate (%): `40`<br>• Fine aggregate / sand (%): `25`<br>• SCM type: `Fly Ash`<br>• SCM (%): `5` | • **Total volume:** 100.0%<br>• **W/c ratio:** **0.46** = 20 ÷ (10 × 3.15 + 5 × 2.35), updates automatically<br>• **Warning:** none | FR-2, FR-2.1, FR-8 | ☐ |
> | 2.4 | Click **Save & add cylinder**. | Batch record saves to database. Add cylinder modal opens automatically. **Continue directly to Section 3.** | FR-1, FR-4 | ☐ |

---

### Section 3: Cylinders

> *Add cylinders C1 and C2.*
>
> **Starting point:** Add cylinder modal for `VM-2026-M1-001`, opened by the last step of Section 2.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Enter Cylinder 1:<br>• Label: `C1`<br>• Test age: `3 days`<br>• Length (in): `12.00`<br>• Width (in): `6.00`<br>Click **Save & add another**. | • **C1** saves to `VM-2026-M1-001`<br>• **Modal:** stays open with confirmation "Added C1"<br>• **Label:** clears for the next entry<br>• **Length, width, test age:** stay filled | FR-4, FR-5 | ☐ |
> | 3.2 | Enter Cylinder 2:<br>• Label: `C2`<br>• Test age: `7 days`<br>• Length (in): `12.00`<br>• Width (in): `6.00`<br>Click **Save**. | Cylinder C2 saves. Modal closes, returning to the Batch Detail view. **Cylinders & breaks** count shows 2. | FR-4, FR-5 | ☐ |

---

### Section 4: Break Results

> *Save a passing break for C1, then a low break for C2 that saves with a warning.*
>
> **Starting point:** Batch Detail for `VM-2026-M1-001`.
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | Click **Enter break**. | **Break results** screen opens with the filter pre-filled with `VM-2026-M1-001`. **Waiting to be broken** lists:<br>• **C1** — Test age `3 days` · Age `3 days` · **Due** tag<br>• **C2** — Test age `7 days` · Age `3 days` · no Due tag | FR-5 | ☐ |
> | 4.2 | Click **Record break** on the C1 row. | **Record break** dialog opens for Batch `VM-2026-M1-001`, Cylinder `C1 · 3-day`. **Break date** defaults to today. | FR-5 | ☐ |
> | 4.3 | Enter valid break result values:<br>• Strength (PSI): `3,710`<br>• Break date: **T** (today, the default) *(3 days after cast, matching C1's 3-day test age)*<br>Click **Save**. | • **Dialog:** closes<br>• **Confirmation:** "Saved VM-2026-M1-001 · C1: 3,710 PSI." with a **View batch** link<br>• **Waiting to be broken:** C1 removed; C2 remains | FR-5 | ☐ |
> | 4.4 | On the C2 row in **Waiting to be broken**, click **Record break** *(C2 is not yet due; the app still allows an early break)*. Enter:<br>• Strength (PSI): `2,500`<br>• Break date: **T** (today, the default)<br>Click **Save**. | **Before saving:**<br>• **Strength:** amber with warning icon (⚠) and "Break strength is below 3,000 PSI. Saved and flagged for review."<br>• **Save:** stays enabled<br>**After saving:**<br>• **Dialog:** closes<br>• **Confirmation** (with warning icon): "Saved VM-2026-M1-001 · C2: 2,500 PSI. Break strength is below 3,000 PSI. Saved and flagged for review."<br>• **Waiting to be broken:** C2 removed; no cylinders left for this batch | FR-5 | ☐ |

---

### Section 5: Curing Readings

> *Save a curing reading for C1.*
>
> **Starting point:** Any screen. Readings are typed into a grid, one row per reading; each row has its own **Save** (or press **Enter**).
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | Click **Curing readings** in the side navigation menu, then pick `VM-2026-M1-001` in the **Batch** dropdown. | **On opening:**<br>• **Curing readings** is the active tab in the side navigation<br>• Message reads "Pick a batch to enter readings."<br>**After picking the batch:**<br>• **Batch:** `VM-2026-M1-001` selected<br>• **Cylinder:** defaults to `C1`<br>• **Cast line:** "Cast **T−3** 08:00 · View batch"<br>• **Grid:** **C1 · readings** with one empty new row (Row 1)<br>• **Time taken:** defaults to now | FR-6 | ☐ |
> | 5.2 | In Row 1, enter:<br>• Time taken: **T−2** at `10:00 AM`<br>• Internal (°F): `72.5`<br>• Ambient (°F): `72`<br>• Moisture (%): `65`<br>• RH (%): `55`<br>• Resistivity (Ω/ft): `12.50`<br>• CO₂ (ppm): `450`<br>Click the row's **Save** (or press **Enter**). | Row 1 locks and reads `72.5 °F`, `72.0 °F`, `65.0%`, `55.0%`, `12.50 Ω/ft`, `450.0 ppm` with status **Saved** (no warnings). A new empty Row 2 appears below. | FR-6 | ☐ |

---

### Section 6: Find the Batch

> *Find the batch by its number.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 6.1 | Click **Home** in the side navigation, then click **Find a batch**. | Search batches screen opens with search input focused. Side navigation shows Search batches as the active tab. | FR-9 | ☐ |
> | 6.2 | Enter partial / case-insensitive query: `vm-2026`. | Search results filter to batches whose number contains `VM-2026`, including `VM-2026-M1-001`. | FR-9 | ☐ |
> | 6.3 | Click row `VM-2026-M1-001`. | Opens full Batch Detail view, showing:<br>• High-level batch info (cast date, cement type, final weight, etc.)<br>• Composition and cement oxide percentages<br>• Cylinder and break data<br>• Curing readings<br>• Projected strength over time | FR-10 | ☐ |

---

### Section 7: Batch Detail & Audit Trail

> *Confirm everything entered was saved, and who created it and when.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 7.1 | Inspect the Batch Detail summary strip. | Displays correct:<br>• Batch number: `VM-2026-M1-001`<br>• Cast date/time: **T−3** `08:00`<br>• Cement type: `Type I`<br>• Final weight (lb): `4,050`<br>• Mix water pH: `7.2`<br>• Expected final break (PSI): `6,200`<br>• Water/Cementitious ratio: `0.46` *(no warning)*<br>• Air/Other (%): `0.0` | FR-10 | ☐ |
> | 7.2 | Verify audit trail metadata on the summary strip. | Clearly shows creator audit information: "Created by [Operator Name] on [Date/Time]". | FR-22 | ☐ |
> | 7.3 | Inspect the **Composition & Oxides** cards. | All saved composition percentages and cement oxide values match original inputs exactly, displayed to two decimal places:<br>• Cement (%): `10.00`<br>• Water (%): `20.00`<br>• Coarse aggregate (%): `40.00`<br>• Fine aggregate / sand (%): `25.00`<br>• SCM type: `Fly Ash`<br>• SCM (%): `5.00`<br>• Alumina Al₂O₃ (%): `5.10`<br>• Iron oxide Fe₂O₃ (%): `3.20`<br>• Magnesium oxide MgO (%): `2.10`<br>• Sulfur trioxide SO₃ (%): `2.40` | FR-22 | ☐ |
> | 7.4 | Select the **Cylinders & breaks** tab. | Table lists both cylinders:<br>**C1**<br>• Age: `Day 3`<br>• Length: `12.00 in` · Width: `6.00 in`<br>• Broken at: **T** `@` time it was saved (`MM/dd @ HH:mm`)<br>• Strength: `3,710 PSI` (no warning)<br>• Readings: `1`<br>**C2**<br>• Age: `Day 7`<br>• Length: `12.00 in` · Width: `6.00 in`<br>• Broken at: **T** `@` time it was saved<br>• Strength: `2,500 PSI ⚠` (hover shows "Break strength is below 3,000 PSI. Saved and flagged for review.")<br>• Readings: `0` | FR-5, FR-22 | ☐ |
> | 7.5 | Select the **Curing readings** tab. | Readings are grouped under **Cylinder C1 · 1 reading**, with correct date and values:<br>• Taken at: **T−2** `10:00`<br>• Internal (°F): `72.5 °F`<br>• Ambient (°F): `72.0 °F`<br>• Moisture (%): `65.0%`<br>• RH (%): `55.0%`<br>• Resistivity (Ω/ft): `12.50 Ω/ft`<br>• CO₂ (ppm): `450.0 ppm` | FR-6, FR-10 | ☐ |

---

### Section 8: Synthetic Data

> *Show that test data is clearly marked SYNTHETIC.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 8.1 | In **Search**, enter seeded test batch `VM-SYN-001` (or any test batch tagged synthetic). | Search row prominently displays the **SYNTHETIC** badge. | FR-10, FR-20 | ☐ |
> | 8.2 | Click `VM-SYN-001` to view Batch Detail. | Header prominently displays the **SYNTHETIC** badge. Associated cylinders are marked synthetic. | FR-10, FR-20 | ☐ |

---

### Section 9: Local Database & Application Logging

> *Show data stays on this computer and the app keeps a log. *(Windows only.)**
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 9.1 | After saving the happy-path batch, open `%LOCALAPPDATA%\VulcanCBAS\` and verify `vulcan.db` exists. Close and reopen Vulcan CBAS, then retrieve `VM-2026-M1-001`. | • **Database:** a single SQLite file `vulcan.db` exists in the application data folder<br>• **After restart:** `VM-2026-M1-001` is still retrievable | NFR-2 | ☐ |
> | 9.2 | With Vulcan CBAS running, open `%LOCALAPPDATA%\VulcanCBAS\logs\` and open the latest `app-*.log` file. | A current application log exists and contains startup diagnostics:<br>• Application startup / version<br>• Log location<br>• Local API startup details | NFR-4 | ☐ |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps** | 27 |
| **Steps Passed** | _____ |
| **Steps Failed** | _____ |
| **Total Elapsed Time** | _____ min *(pass criterion: ≤ 20 min)* |

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
