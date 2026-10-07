# Milestone 1 Acceptance Test Script

| Document Info | Details |
|---|---|
| **File** | `docs/m1-acceptance.md` |
| **Ticket** | SCRUM-126 (M1-83) |
| **Target Build** | `v0.1.0-m1` |
| **Target Execution Time** | < 20 minutes |
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
  6. [Search & Retrieval by Batch Number](#section-6-search--retrieval-by-batch-number)
  7. [Batch Detail & Audit Trail Verification](#section-7-batch-detail--audit-trail-verification)
  8. [Synthetic Data Tagging & Filtering](#section-8-synthetic-data-tagging--filtering)
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

> **Execution flow:** Run the sections in order. Sections 2–5 each follow the same pattern: helper text → blank fields → invalid values (red) → warnings (amber) → valid save. Red and amber checks are never saved. Each section's final save creates the data the next section needs:
>
> **New Batch** (creates `VM-2026-M1-001`) → **Cylinders** (creates C1, C2) → **Break Results** (breaks for C1 and C2) → **Curing Readings** (reading for C1) → Search, Detail, Synthetic, Database.
>
> **Dates:** All dates are relative to the day the test is run (**T**). Timeline: cast **T−3 8:00 AM** → curing reading on C1 **T−2 10:00 AM** → C1 broken **T** (its 3-day age; the app records the time you click Save) → C2 broken early on **T** (before its 7-day age) at a low strength, so it saves with a warning. Batch Detail shows dates as `MM/dd/yy HH:mm` (24-hour). Example if run on 10/06/2026: T−4 = 10/02, T−3 = 10/03, T−2 = 10/04, T = 10/06. Write the actual dates used in the Execution Record.

### Section 1: Sign-In & Home

> *Verify application launches, technician is able to log in, and home dashboard appears successfully.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 1.1 | Launch the Vulcan CBAS application from the desktop shortcut or Start Menu and verify Technician login screen appears. | Application window opens cleanly to the Technician login screen for technician name entry. | NFR-1 | ☐ |
> | 1.2 | Enter the technician name and continue (e.g., "*Don Murphy*"). | Login is successful and screen displays Home dashboard. | FR-21 | ☐ |
> | 1.3 | Verify the operator lands on Home and the top header displays the logged-in operator session. | Current active operator profile is clearly indicated for record attribution in two forms:<br>1. Upper right of screen displays logged-in technician's initials + first initial and last name.<br>2. Home dashboard displays friendly message recognizing logged-in technician (e.g., "*Good morning, Don Murphy*"). | FR-21 | ☐ |
> | 1.4 | Check available Home dashboard quick-action buttons. | Primary actions **New batch**, **Find a batch**, and **Enter break results** are visible and responsive. Sidebar navigation displays additional deep links: **Home, New batch, Search batches, Break results, Curing Readings**. | FR-9 | ☐ |

---

### Section 2: New Batch

> *Verify New batch guidance, required fields, rejections and warnings, then create batch `VM-2026-M1-001`.*
>
> #### Helper text & affordances
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.1 | Select **New batch**. | Application window opens cleanly to the New batch entry screen. Side navigation menu shows New batch tab as active. | FR-1, FR-2, FR-2.1, FR-3 | ☐ |
> | 2.2 | Verify each relevant field shows its label, unit (if any), and allowed range/helper text before entry. | Helper text appears beneath each field in soft gray before any input (*e.g., Batch Number displays guidance such as "Letters, digits, dashes · max 32."*). | FR-2.1, FR-4, FR-5, FR-7; Supporting UI behavior (SCRUM-123) | ☐ |
>
> #### Blank, whitespace, tab away (red errors)
>
> **Expected for every step in this table:** Field displays inline red error: "This field is required". **Save batch** button is disabled.
>
> | Step | Field (leave blank or enter whitespace, then tab away) | Req ID | Pass / Fail |
> |---|---|---|:---:|
> | 2.3 | **Batch number** | FR-1 | ☐ |
> | 2.4 | **Final weight (lb)** | FR-1 | ☐ |
> | 2.5 | **Expected final break (PSI)** | FR-5 | ☐ |
> | 2.6 | **Mix water pH** | FR-7 | ☐ |
> | 2.7 | **Alumina Al₂O₃ (%)** | FR-3 | ☐ |
> | 2.8 | **Iron oxide Fe₂O₃ (%)** | FR-3 | ☐ |
> | 2.9 | **Magnesium oxide MgO (%)** | FR-3 | ☐ |
> | 2.10 | **Sulfur trioxide SO₃ (%)** | FR-3 | ☐ |
> | 2.11 | **Cement (%)** | FR-1 | ☐ |
> | 2.12 | **Water (%)** | FR-1 | ☐ |
> | 2.13 | **Coarse aggregate (%)** | FR-1 | ☐ |
> | 2.14 | **Fine aggregate / sand (%)** | FR-1 | ☐ |
> | 2.15 | **SCM (%)** | FR-1 | ☐ |
>
> #### Invalid values (red errors)
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.16 | Under **Batch**, enter<br>Batch number: `54%6!34578)2` | Inline red error: "Letters, digits and dashes only". Save batch button is disabled. | FR-1, NFR-6 | ☐ |
> | 2.17 | Under **Batch**, enter<br>Batch number: `123456789123456123456789123456789` | Field stops accepting input at 32 characters; the extra character is not entered (field shows `12345678912345612345678912345678`). ⚠️ **TO CONFIRM:** whether an inline red error ("Limit to 32 characters only") should also display. | FR-1, NFR-6 | ☐ |
> | 2.18 | Under **Batch**, enter<br>Batch number: a preregistered batch in the system *(e.g., existing batch `VM-2026-000`)* | Inline error: "Batch number already exists". Re-entry required. Save batch button is disabled. | FR-1 | ☐ |
> | 2.19 | Under **Batch**, open the **Cast date/time** date picker and attempt to select a future date. | Future dates are disabled on the date picker and cannot be selected. | FR-1, NFR-6 | ☐ |
> | 2.20 | Under **Batch**, enter<br>Final batch weight (lb): `-500` | Red error: "Weight must be greater than 0". Value is not saved. | FR-1, NFR-6 | ☐ |
> | 2.21 | Under **Batch**, enter<br>Final batch weight (lb): `1.234` | Red error: "Weight must be within two decimals." Value is not saved. | FR-1, NFR-6 | ☐ |
> | 2.22 | Under **Batch**, enter<br>Expected final break (PSI): `2999` | Red error: "PSI must be within the range [3000-10,000]". Value is not saved. | FR-5, NFR-6 | ☐ |
> | 2.23 | Under **Batch**, enter<br>Mix water pH: `15.0` | Red inline message: "pH must be within the range [1-9]". Save batch button is disabled. | FR-7, NFR-6 | ☐ |
> | 2.24 | Under **Cement oxides**, enter<br>Alumina Al₂O₃ (%): `101` | Red boundary error: "The aluminum oxide percent must be a valid percentage [0-100]". Save batch button is disabled. | FR-3, NFR-6 | ☐ |
> | 2.25 | Under **Cement oxides**, enter<br>Iron oxide Fe₂O₃ (%): `101` | Red boundary error: "The iron oxide percent must be a valid percentage [0-100]". Save remains disabled. | FR-3, NFR-6 | ☐ |
> | 2.26 | Under **Cement oxides**, enter<br>Magnesium oxide MgO (%): `6.01` | Red boundary error: "The magnesium oxide percent must be within the range [0.0-6.0]". Save batch button is disabled. | FR-3, NFR-6 | ☐ |
> | 2.27 | Under **Cement oxides**, enter<br>Sulfur trioxide SO₃ (%): `4.59` | Red boundary error: "The sulfur trioxide percent must be within the range [0.0-4.5]". Save remains disabled. | FR-3, NFR-6 | ☐ |
> | 2.28 | Under **Mix Composition**, enter<br>Cement (%): `9` | Red boundary error: "Cement composition must be within the range [10-15]". Save remains disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.29 | Under **Mix Composition**, enter<br>Water (%): `14` | Red boundary error: "Water composition must be within the range [15-20]". Save remains disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.30 | Under **Mix Composition**, enter<br>Coarse aggregate (%): `39` | Red boundary error: "Coarse aggregate composition must be within the range [40-55]". Save remains disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.31 | Under **Mix Composition**, enter<br>Fine aggregate / sand (%): `24` | Red boundary error: "Fine aggregate composition must be within the range [25-30]". Save remains disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.32 | Under **Mix Composition**, enter<br>SCM (%): `101` | Red boundary error: "SCM composition must be a valid percentage [0-100]". Save remains disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.33 | Under **Mix Composition**, clear prior values and enter:<br>• Cement (%): `15`<br>• Water (%): `20`<br>• Coarse agg (%): `40`<br>• Fine agg (%): `25`<br>• SCM (%): `1` *(sum = 101%)* | Composition card highlights in red: "Total composition must not exceed 100%". Save batch button is disabled. | FR-2, FR-2.1, NFR-6 | ☐ |
> | 2.34 | Under **Supplementary Cementitious Materials (SCM)**, set:<br>• SCM type: `None`<br>• SCM (%): `10` | **SCM type** field displays inline red error: "Choose the SCM type when SCM is more than 0%". Save batch button is disabled. | FR-2.1, NFR-6 | ☐ |
>
> #### Warnings on unusual values (amber)
>
> **Expected for every step in this table:** Input turns amber with a warning icon (⚠) and the helper text shown below. **Save remains enabled.**
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.35 | In **Mix water pH**, enter `6.9`. | "pH is outside the normal range [7.0-7.5]" | FR-7 | ☐ |
> | 2.36 | Under **Mix Composition**, enter:<br>• Cement (%): `15`<br>• Water (%): `15`<br>• Coarse agg (%): `40`<br>• Fine agg (%): `25`<br>• SCM type: `Fly Ash`<br>• SCM (%): `5` *(sum = 100%)* | Inputs result in a water/cementitious ratio of **0.25** (15 ÷ (15 × 3.15 + 5 × 2.35)). Field turns amber: "The water to cement ratio is outside the normal range [0.4-0.6]" | FR-8 | ☐ |
> | 2.37 | Under **Cement oxides**, enter<br>Alumina Al₂O₃ (%): `2.9` | "The aluminum oxide percent is outside the normal range [3.0-7.5]" | FR-3 | ☐ |
> | 2.38 | Under **Cement oxides**, enter<br>Iron oxide Fe₂O₃ (%): `0.4` | "The iron oxide percent is outside the normal range [0.5-6.0]" | FR-3 | ☐ |
> | 2.39 | Under **Cement oxides**, enter<br>Magnesium oxide MgO (%): `4.1` | "The magnesium oxide percent is outside the normal range [0.1-4.0]" | FR-3 | ☐ |
> | 2.40 | Under **Cement oxides**, enter<br>Sulfur trioxide SO₃ (%): `1.2` | "The sulfur trioxide percent is outside the normal range [1.3-3.0]" | FR-3 | ☐ |
>
> #### Valid save – create `VM-2026-M1-001`
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 2.41 | Navigate to **New batch** via the side navigation menu. Under **Batch**, enter valid batch metadata:<br>• Batch number: `VM-2026-M1-001`<br>• Cast date/time: **T−3** at `8:00 AM`<br>• Final weight (lb): `4,050`<br>• Expected final break (PSI): `6,200`<br>• Cement type: `Type I`<br>• Mix water pH: `7.2` | New batch screen appears and New batch tab is active in the side navigation menu. All metadata fields accept input with neutral valid styling. | FR-1, FR-2 | ☐ |
> | 2.42 | Under **Cement oxides**, enter:<br>• Alumina Al₂O₃ (%): `5.1`<br>• Iron oxide Fe₂O₃ (%): `3.2`<br>• Magnesium oxide MgO (%): `2.1`<br>• Sulfur trioxide SO₃ (%): `2.4` | No warning or error banners appear; chemistry validation card reflects valid composition. | FR-3 | ☐ |
> | 2.43 | Under **Composition**, enter:<br>• Cement (%): `10`<br>• Water (%): `20`<br>• Coarse aggregate (%): `40`<br>• Fine aggregate / sand (%): `25`<br>• SCM type: `Fly Ash`<br>• SCM (%): `5` | Total volume computes to 100.0%; live w/c ratio calculates (**0.46** = 20 ÷ (10 × 3.15 + 5 × 2.35)) and updates automatically, with no warning. | FR-2, FR-2.1 | ☐ |
> | 2.44 | Click **Save & add cylinder**. | Batch record saves to database. Add cylinder modal opens automatically. **Continue directly to Section 3.** | FR-1, FR-4 | ☐ |

---

### Section 3: Cylinders

> *Verify Add cylinder guidance, required fields and rejections, then create cylinders C1 and C2 and confirm Cancel discards an unsaved cylinder.*
>
> **Starting point:** Add cylinder modal for `VM-2026-M1-001`, opened by the last step of Section 2. (If it was closed, open Batch Detail for `VM-2026-M1-001` and click **Add cylinder**.)
>
> #### Helper text & affordances
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.1 | Verify each field shows its label, unit (if any), and helper text before entry, then open the **Test age** dropdown. | Helper text appears beneath each field in soft gray:<br>• **Label:** "Up to 20 characters, e.g. A1"<br>• **Test age:** "When it will be broken"<br>• **Length (in)** and **Width (in):** allowed range<br>**Test age** dropdown lists `3 days`, `7 days`, `28 days`, with no value selected by default. | FR-4, FR-5; Supporting UI behavior (SCRUM-123) | ☐ |
>
> #### Blank, whitespace, tab away (red errors)
>
> **Expected for every step in this table:** Field displays inline red error: "This field is required". **Save** and **Save & add another** are disabled.
>
> | Step | Field (leave blank or enter whitespace, then tab away) | Req ID | Pass / Fail |
> |---|---|---|:---:|
> | 3.2 | **Label** | FR-4 | ☐ |
> | 3.3 | **Test age** *(leave unselected)* | FR-4, FR-5 | ☐ |
> | 3.4 | **Length (in)** | FR-4 | ☐ |
> | 3.5 | **Width (in)** | FR-4 | ☐ |
>
> #### Invalid values (red errors)
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.6 | In **Label**, enter `333$22A%BCAB`. | ⚠️ **FAIL / TO CONFIRM:** Inline red error: "Letters, digits and dashes only". Save buttons are disabled. | FR-4, NFR-6 | ☐ |
> | 3.7 | In **Label**, enter `333-22A-BCAB-CABC1-2312-34879`. | Field stops accepting input at 20 characters; extra characters are not entered (field shows `333-22A-BCAB-CABC1-2`). ⚠️ **TO CONFIRM:** whether an inline red error ("Limit to 20 characters only") should also display. | FR-4, NFR-6 | ☐ |
> | 3.8 | In **Length (in)**, enter `-1`. | Red boundary error: "Specimen length must be nonzero and in the range [0-12]". Save buttons are disabled. | FR-4, NFR-6 | ☐ |
> | 3.9 | In **Length (in)**, enter `1.234`. | Red boundary error: "Specimen length must be within two decimals". Save buttons are disabled. | FR-4, NFR-6 | ☐ |
> | 3.10 | In **Width (in)**, enter `-1`. | Red boundary error: "Specimen width must be nonzero and in the range [0-6]". Save buttons are disabled. | FR-4, NFR-6 | ☐ |
> | 3.11 | In **Width (in)**, enter `1.234`. | Red boundary error: "Specimen width must be within two decimals". Save buttons are disabled. | FR-4, NFR-6 | ☐ |
>
> #### Valid save – create C1 and C2
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.12 | Clear the previous values and enter Cylinder 1:<br>• Label: `C1`<br>• Test age: `3 days`<br>• Length (in): `12.00`<br>• Width (in): `6.00`<br>Click **Save & add another**. | Cylinder C1 saves and associates to `VM-2026-M1-001`. Modal stays open with confirmation "Added C1". Label clears for the next entry; length, width and test age stay filled. | FR-4, FR-5 | ☐ |
> | 3.13 | In **Label**, enter `C1` again (keep the other values) and click **Save**. | ⚠️ **TO CONFIRM:** Error: "C1 already exists in this batch". Cylinder is not saved and the modal stays open. | FR-4, NFR-6 | ☐ |
> | 3.14 | Enter Cylinder 2:<br>• Label: `C2`<br>• Test age: `7 days`<br>• Length (in): `12.00`<br>• Width (in): `6.00`<br>Click **Save**. | Cylinder C2 saves. Modal closes, returning to the Batch Detail view. **Cylinders & breaks** count shows 2. | FR-4, FR-5 | ☐ |
>
> #### Cancel
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 3.15 | Click **Add cylinder** and enter a valid cylinder:<br>• Label: `C3`<br>• Test age: `28 days`<br>• Length (in): `12.00`<br>• Width (in): `6.00`<br>Click **Cancel**. | Modal closes without saving and returns to the Batch Detail view. **Cylinders & breaks** tab still lists only C1 and C2 (no C3). | FR-4 | ☐ |

---

### Section 4: Break Results

> *Verify break result guidance, required fields, rejections and warnings, then save a valid break for cylinder C1 and an early break for C2 that saves with a warning.*
>
> **Starting point:** Batch Detail for `VM-2026-M1-001`. Breaks are recorded one cylinder at a time in the **Record break** dialog. Don't save until the valid save step.
>
> #### Open & helper text
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.1 | Click **Enter break**. | **Break results** screen opens with the filter pre-filled with `VM-2026-M1-001`. **Waiting to be broken** lists:<br>• **C1** — Test age `3 days` · Age `3 days` · **Due** tag<br>• **C2** — Test age `7 days` · Age `3 days` · no Due tag | FR-5 | ☐ |
> | 4.2 | Click **Record break** on the C1 row. Verify the header details and each field's helper text before entry. | **Record break** dialog opens showing Batch `VM-2026-M1-001`, Cylinder `C1 · 3-day`, Cast **T−3**, Age today `3 days`. Helper text appears in soft gray: **Strength (PSI)** shows its allowed range; **Break date** defaults to today with the hint "Today unless it was broken earlier". **Save** is disabled with the message "Enter the strength to save". | FR-5; Supporting UI behavior (SCRUM-123) | ☐ |
>
> #### Blank, whitespace, tab away (red errors)
>
> **Expected for every step in this table:** Field displays inline red error: "This field is required". **Save** is disabled.
>
> | Step | Field (leave blank or enter whitespace, then tab away) | Req ID | Pass / Fail |
> |---|---|---|:---:|
> | 4.3 | **Strength (PSI)** | FR-5 | ☐ |
> | 4.4 | **Break date** *(clear the default date)* | FR-5 | ☐ |
>
> #### Invalid values (red errors)
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.5 | Open the **Break date** picker and attempt to select a future date. | Future dates are disabled on the date picker and cannot be selected. | FR-5, NFR-6 | ☐ |
> | 4.6 | Open the **Break date** picker and attempt to select a date before the batch was cast (**T−4**). | Dates before the cast date (**T−3**) are disabled on the date picker and cannot be selected. | FR-5, NFR-6 | ☐ |
> | 4.7 | In **Strength (PSI)**, enter `0`. | Red boundary error: "PSI must be within the range [1-10000]". Save remains disabled. | FR-5, NFR-6 | ☐ |
>
> #### Warnings on unusual values (amber)
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.8 | In **Strength (PSI)**, enter `2,500`. | Input turns amber with warning icon (⚠); helper text reads "Break strength is below 3,000 PSI. Saved and flagged for review.". Save remains enabled. | FR-5 | ☐ |
>
> #### Valid save – break for C1
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.9 | Enter valid break result values:<br>• Strength (PSI): `3,710`<br>• Break date: **T** (today, the default) *(3 days after cast, matching C1's 3-day test age)*<br>Click **Save**. | Dialog closes. Confirmation reads "Saved VM-2026-M1-001 · C1: 3,710 PSI." with a **View batch** link. C1 is removed from **Waiting to be broken**; C2 remains. | FR-5 | ☐ |
>
> #### Save with a warning – early break for C2
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 4.10 | On the C2 row in **Waiting to be broken**, click **Record break** *(C2 is not yet due; the app still allows an early break)*. Enter:<br>• Strength (PSI): `2,500`<br>• Break date: **T** (today, the default)<br>Click **Save**. | Strength turns amber with warning icon (⚠) and "Break strength is below 3,000 PSI. Saved and flagged for review." before saving; **Save** stays enabled. After saving, the dialog closes and the confirmation shows a warning icon: "Saved VM-2026-M1-001 · C2: 2,500 PSI. Break strength is below 3,000 PSI. Saved and flagged for review.". C2 is removed from **Waiting to be broken**, leaving no cylinders for this batch. | FR-5 | ☐ |
---

### Section 5: Curing Readings

> *Verify the curing grid's guidance, required cells, rejections and warnings, then save a valid reading for cylinder C1.*
>
> **Starting point:** Batch Detail for `VM-2026-M1-001`. Readings are typed into a grid, one row per reading. Each row has its own **Save** (or press **Enter**). Cells with a bad value turn red as you type; the row's messages appear below the grid when you try to save. Don't save a valid row until the valid save step, or the reading count in Section 7 will be off.
>
> #### Open & helper text
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.1 | On Batch Detail, click **Curing readings**. | **Curing readings** screen opens with **Batch** `VM-2026-M1-001` selected and **Cylinder** defaulting to `C1`. Shows "Cast **T−3** 08:00 · View batch". A **C1 · readings** grid shows one empty new row (Row 1) with **Time taken** defaulted to now. | FR-6 | ☐ |
> | 5.2 | Check the grid's guidance and controls. | Grid header shows the allowed ranges: "Internal 0–200 °F · Ambient 0–150 °F (normal 60–80) · Moisture 0–100% · RH 0–100% · Resistivity ≥ 0.01 ohms/ft · CO₂ ≥ 0 ppm". **Batch** and **Cylinder** are dropdowns; **Time taken** is a date/time field. Footer hint reads "Tab moves across · Enter saves the row". | FR-6; Supporting UI behavior (SCRUM-123) | ☐ |
>
> #### Blank cells (red errors)
>
> **Action for this table:** Clear **Time taken**, leave all six readings blank, and click the row's **Save**. **Expected:** Row status shows **Not saved**, every blank cell turns red, and each message below appears under the grid.
>
> | Step | Cell | Expected Message | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.3 | **Time taken** | "Row 1 · Time taken — required." | FR-6 | ☐ |
> | 5.4 | **Internal (°F)** | "Row 1 · Internal — required." | FR-6 | ☐ |
> | 5.5 | **Ambient (°F)** | "Row 1 · Ambient — required." | FR-6 | ☐ |
> | 5.6 | **Moisture (%)** | "Row 1 · Moisture — required." | FR-6 | ☐ |
> | 5.7 | **RH (%)** | "Row 1 · RH — required." | FR-6 | ☐ |
> | 5.8 | **Resistivity (Ω/ft)** | "Row 1 · Resistivity — required." | FR-6 | ☐ |
> | 5.9 | **CO₂ (ppm)** | "Row 1 · CO₂ — required." | FR-6 | ☐ |
>
> #### Invalid values (red errors)
>
> **For each step:** enter the value in Row 1, then click the row's **Save**. **Expected:** the cell turns red, the row stays **Not saved**, and the message shown appears under the grid (messages for other blank cells may also show).
>
> | Step | Action / What to Type | Expected Message | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.10 | In **Time taken**, enter a future date and time: **T+1** at `10:00 AM`. | "Row 1 · Time taken — can't be in the future." | FR-6, NFR-6 | ☐ |
> | 5.11 | In **Time taken**, enter a time before the batch was cast: **T−4** at `8:00 AM`. | "Row 1 · Time taken — can't be before the batch was cast." | FR-6, NFR-6 | ☐ |
> | 5.12 | In **Internal (°F)**, enter `201`. | "Row 1 · Internal 201 °F — must be 0–200 °F." | FR-6, NFR-6 | ☐ |
> | 5.13 | In **Internal (°F)**, enter `72.12`. **Don't click Save for this step;** just check the cell. | ⚠️ **FAIL / TO CONFIRM:** Cell turns red: "Internal temperature must be within one decimal". | FR-6, NFR-6 | ☐ |
> | 5.14 | In **Ambient (°F)**, enter `151`. | "Row 1 · Ambient 151 °F — must be 0–150 °F." | FR-6, NFR-6 | ☐ |
> | 5.15 | In **Moisture (%)**, enter `101`. | "Row 1 · Moisture 101% — must be 0–100%." | FR-6, NFR-6 | ☐ |
> | 5.16 | In **RH (%)**, enter `101`. | "Row 1 · RH 101% — must be 0–100%." | FR-6, NFR-6 | ☐ |
> | 5.17 | In **Resistivity (Ω/ft)**, enter `-1`. | "Row 1 · Resistivity -1 ohms/ft — must be ≥ 0.01 ohms/ft." | FR-6, NFR-6 | ☐ |
> | 5.18 | In **CO₂ (ppm)**, enter `-1`. | "Row 1 · CO₂ -1 ppm — must be ≥ 0 ppm." | FR-6, NFR-6 | ☐ |
>
> #### Warnings on unusual values (amber)
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.19 | In **Ambient (°F)**, enter `59`. **Don't save.** | Cell turns amber; hovering shows "Ambient 59 °F — outside the normal 60–80 °F. Saved and flagged." | FR-6 | ☐ |
>
> #### Valid save – reading for C1
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 5.20 | Clear the previous values and enter in Row 1:<br>• Time taken: **T−2** at `10:00 AM`<br>• Internal (°F): `72.5`<br>• Ambient (°F): `72`<br>• Moisture (%): `65`<br>• RH (%): `55`<br>• Resistivity (Ω/ft): `12.50`<br>• CO₂ (ppm): `450`<br>Click the row's **Save** (or press **Enter**). | Row 1 locks and reads `72.5 °F`, `72.0 °F`, `65.0%`, `55.0%`, `12.50 Ω/ft`, `450.0 ppm` with status **Saved** (no warnings). A new empty Row 2 appears below. | FR-6 | ☐ |
---

### Section 6: Search & Retrieval by Batch Number

> *Verify batch entries can be searched for and retrieved successfully.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 6.1 | Click **Find a batch** on the Home dashboard. | Search batches screen opens with search input focused. Side navigation shows Search batches as the active tab. | FR-9 | ☐ |
> | 6.2 | Enter non-existent batch query: `XYZ-9999`. | Friendly empty state: "No batches match 'XYZ-9999'". No crash or unhandled error. | FR-9 | ☐ |
> | 6.3 | Enter partial / case-insensitive query: `vm-2026-m1`. | Search results filter to show `VM-2026-M1-001`. | FR-9 | ☐ |
> | 6.4 | Verify search result columns. | Table displays:<br>• Batch number<br>• Cast date/time<br>• Cement type<br>• Expected final break (PSI)<br>• Water/Cementitious ratio<br>• 28-day average (PSI)<br>• Breaks<br>• Warnings | FR-9, FR-10 | ☐ |
> | 6.5 | Click row `VM-2026-M1-001`. | Application navigates to full Batch Detail view, showing high-level batch information (cast date, cement type, final weight, etc.), composition and cement oxide percentages, cylinder and break data, curing readings, and projected strength over time. | FR-10 | ☐ |

---

### Section 7: Batch Detail & Audit Trail Verification

> *Verify every saved field is properly persisted and returned for user view.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 7.1 | Inspect the Batch Detail summary strip. | Displays correct:<br>• Batch number: `VM-2026-M1-001`<br>• Cast date/time: **T−3** `08:00`<br>• Cement type: `Type I`<br>• Final weight (lb): `4,050`<br>• Expected final break (PSI): `6,200`<br>• Mix water pH: `7.2`<br>• Water/Cementitious ratio: `0.46` *(no warning)*<br>• Air/Other (%): `0.0` | FR-10 | ☐ |
> | 7.2 | Verify audit trail metadata on the summary strip. | Clearly shows creator audit information: "Created by [Operator Name] on [Date/Time]". | FR-22 | ☐ |
> | 7.3 | Inspect the **Composition & Oxides** cards. | All saved composition percentages and cement oxide values match original inputs exactly, displayed to two decimal places:<br>• Cement (%): `10.00`<br>• Water (%): `20.00`<br>• Coarse aggregate (%): `40.00`<br>• Fine aggregate / sand (%): `25.00`<br>• SCM type: `Fly Ash`<br>• SCM (%): `5.00`<br>• Alumina Al₂O₃ (%): `5.10`<br>• Iron oxide Fe₂O₃ (%): `3.20`<br>• Magnesium oxide MgO (%): `2.10`<br>• Sulfur trioxide SO₃ (%): `2.40` | FR-22 | ☐ |
> | 7.4 | Select the **Cylinders & breaks** tab. | Table lists both cylinders:<br>**C1** — Age `Day 3` · Length `12.00 in` · Width `6.00 in` · Broken at **T** `@` time it was saved (`MM/dd @ HH:mm`) · Strength `3,710 PSI` (no warning) · Readings `1`<br>**C2** — Age `Day 7` · Length `12.00 in` · Width `6.00 in` · Broken at **T** `@` time it was saved · Strength `2,500 PSI ⚠` (hover shows "Break strength is below 3,000 PSI. Saved and flagged for review.") · Readings `0` | FR-5, FR-22 | ☐ |
> | 7.5 | Select the **Curing readings** tab. | Readings are grouped under **Cylinder C1 · 1 reading**, with correct date and values:<br>• Taken at: **T−2** `10:00`<br>• Internal (°F): `72.5 °F`<br>• Ambient (°F): `72.0 °F`<br>• Moisture (%): `65.0%`<br>• RH (%): `55.0%`<br>• Resistivity (Ω/ft): `12.50 Ω/ft`<br>• CO₂ (ppm): `450.0 ppm` | FR-6, FR-10 | ☐ |
> | 7.6 | In **Search batches**, open a known batch seeded with warning values. | Batch detail displays total warning count in the list of tabs. Save remains enabled. | Supporting UI behavior (SCRUM-123) | ☐ |

---

### Section 8: Synthetic Data Tagging & Filtering

> *Verify synthetic data is properly tagged, persisted, and returned for user view.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 8.1 | In **Search**, enter seeded test batch `VM-SYN-001` (or any test batch tagged synthetic). | Search row prominently displays the **SYNTHETIC** badge. | FR-10, FR-20 | ☐ |
> | 8.2 | Click `VM-SYN-001` to view Batch Detail. | Header prominently displays the **SYNTHETIC** badge. Associated cylinders are marked synthetic. | FR-10, FR-20 | ☐ |
> | 8.3 | Return to Search and uncheck / toggle OFF **Include Synthetic Data**. | Synthetic batch `VM-SYN-001` is excluded from the results. | FR-10, FR-20 | ☐ |

---

### Section 9: Local Database & Application Logging

> *Verify the single internal database and application logging are properly implemented.*
>
> | Step | Action / What to Type | Expected Result | Req ID | Pass / Fail |
> |---|---|---|---|:---:|
> | 9.1 | After saving the happy-path batch, open `%LOCALAPPDATA%\VulcanCBAS\` and verify `vulcan.db` exists. Close and reopen Vulcan CBAS, then retrieve `VM-2026-M1-001`. | A single internal SQLite database file `vulcan.db` exists in the application data folder, and the previously saved batch is still retrievable after restart. | NFR-2 | ☐ |
> | 9.2 | With Vulcan CBAS running, open `%LOCALAPPDATA%\VulcanCBAS\logs\` and open the latest `app-*.log` file. | A current application log exists on disk and contains diagnostic startup information such as application startup/version, log location, and local API startup details. | NFR-4 | ☐ |

---

## Sign-Off & Acceptance Summary

| Metric | Value |
|---|---|
| **Total Test Steps** | 109 |
| **Steps Passed** | _____ |
| **Steps Failed** | _____ |
| **Total Elapsed Time** | _____ min *(pass criterion: ≤ 20 min)* |

### Remediation / Notes on Failures

*Document any failed step numbers, error messages, and associated Jira issue keys here.*

| Step # | Error / Observation | Jira Key |
|---|---|---|
| 3.6 | Cylinder label character restriction (letters, digits and dashes only) not enforced as expected. | |
| 5.13 | Curing Internal (°F) accepts more than one decimal place (`72.12`); no "within one decimal" check in the app. | |
| | | |
| | | |

### Sign-off Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Test Operator | | | |
| Lead / Reviewer | | | |
