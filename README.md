# Vulcan CBAS — Concrete Batch Analysis & Strength Prediction System

A Windows desktop application for Vulcan Materials that records concrete batch data (mix recipe, cement chemistry, test
cylinders, curing readings and break results), validates every value against DOT limits, and lets operators find any
batch by its number. Later milestones add strength charts and 3/7/28-day strength predictions.

Built by the KSU Student Development Team for SWE 4713 (Section W01), under the signed Service Level Agreement with
Vulcan Materials.

---

## Status

| Milestone                               | Scope                                                                                    | Due                  |
| --------------------------------------- | ---------------------------------------------------------------------------------------- | -------------------- |
| **M1 — Batch Record & Data Validation** | Installable build, batch/cylinder/curing/break entry, validation, search by batch number | **Fri, Oct 9, 2026** |
| M2 — Strength Charting & Prediction     | PSI-vs-time chart, 3/7/28-day predictions, early-warning flags                           | Fri, Oct 30, 2026    |
| M3 — Calibration & Reporting            | Calibration against real breaks, printable/exportable reports                            | Fri, Nov 20, 2026    |
| Final delivery                          | Signed installer, docs, training, source handoff                                         | Fri, Dec 4, 2026     |

Current version: `0.1.0-m2` (pre-release)

---

## Tech stack

| Layer              | Choice                                                         |
| ------------------ | -------------------------------------------------------------- |
| Language / runtime | C# on .NET 10                                                  |
| Desktop UI         | **Angular + PrimeNG** in WebView2, hosted by WPF               |
| Desktop host       | WPF window that starts the server and shows WebView2           |
| Server             | ASP.NET Core, hosted **inside** the desktop app on `127.0.0.1` |
| Database           | SQLite via EF Core                                             |
| Tests              | xUnit + Shouldly (.NET), `ng test` (Angular)                   |
| Logging            | Serilog (rolling file)                                         |
| Installer          | Inno Setup (bundles the WebView2 runtime)                      |
| CI                 | GitHub Actions (`windows-latest`)                              |

The reasons behind these choices are in [`docs/adr/`](docs/adr/).

**How the pieces fit:** the app opens one WPF window. That window starts the built-in server on a random local port,
then points WebView2 at it. The server hands out the compiled Angular app from its `wwwroot` folder, and every API
route lives under `/api`. Nothing leaves the computer.

---

## Prerequisites

- **Windows 10 or 11, 64-bit.** WPF does not build or run on macOS or Linux. Mac users need a Windows VM or lab
  machine to run the full app, but can work on the Angular screens on a Mac (see [UI work](#ui-work)).
- **.NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
- **Node.js LTS** (includes npm): https://nodejs.org
- **Visual Studio 2026** (with the _.NET desktop development_ and _ASP.NET and web development_ workloads) or
  **JetBrains Rider**
- **WebView2 Runtime**: already installed on Windows 11 and most Windows 10 machines. If the app opens to a blank
  window, install it from https://developer.microsoft.com/microsoft-edge/webview2/
- **Git**

Check your setup:

```powershell
dotnet --version   # should start with 10.
node --version     # should be an LTS version
git --version
```

---

## Getting started

```powershell
git clone https://github.com/emmyatri/concrete-analysis-system.git
cd concrete-analysis-system
git checkout dev

# Angular screens
cd src/Vulcan.Web
npm ci
npm test -- --watch=false
npm run build
cd ../..

# .NET
dotnet build
dotnet test
```

Everything should finish with **0 warnings and 0 errors**. Warnings are treated as errors, so a warning will fail the
build.

Build the Angular app **before** the .NET build: `npm run build` (in `src/Vulcan.Web`) writes it to
`src/Vulcan.Api/wwwroot/`, and `dotnet build` then copies it into the desktop app. CI runs them in that order.
That folder is generated, so **don't edit or commit it.**

### Run the app

Open `Vulcan.slnx` in Visual Studio or Rider, set **Vulcan.Desktop** as the startup project, and press **F5**.

Or from the command line:

```powershell
dotnet run --project src/Vulcan.Desktop
```

On first launch the app asks for a technician name, creates its database, and starts its built-in server. No setup is
needed.

### UI work

For fast screen work you don't need the desktop app. Run Angular on its own in a browser with fake data:

```powershell
cd src/Vulcan.Web
npm start          # opens http://localhost:4200 using mock API responses
```

Changes reload instantly. Before opening a PR, run the full desktop app once to check the screen inside WebView2.

The approved screen designs are in Figma: **Vulcan CBAS — Hi-Fi** (link pinned in the team channel). Build to the
mockup. If something in it doesn't work, ask a lead before changing it.

---

## Project structure

```
Vulcan.slnx
├─ src/
│  ├─ Vulcan.Domain/          Data classes and every validation rule. Uses nothing else.
│  ├─ Vulcan.Application/     App logic (services) and the data shapes sent to/from the API.
│  ├─ Vulcan.Prediction/      Strength prediction (empty until Milestone 2).
│  ├─ Vulcan.Infrastructure/  Database (EF Core + SQLite), audit stamping, logging.
│  ├─ Vulcan.Api/             Server endpoints under /api; re-checks every value (SLA NFR-6).
│  │                          Also serves the built Angular app from wwwroot/.
│  ├─ Vulcan.Web/             The screens: Angular + PrimeNG. Not part of Vulcan.sln; built by npm.
│  └─ Vulcan.Desktop/         The WPF window: starts the API and shows the screens in WebView2.
├─ tests/                     One test project per src project.
├─ data/
│  └─ synthetic/              Fake test/demo data. Every record is marked IsSynthetic.
├─ installer/                 Inno Setup script.
├─ docs/                      Schema, API, validation rules, decisions (see below).
└─ .github/                   CI workflow, PR template, CODEOWNERS.
```

**The one rule that matters most:** validation lives in `Vulcan.Domain` and nowhere else. The API runs those rules on
every save. The Angular screens get the limits from the API so they can show errors as you type, but the server always
has the final say. Never write a range check in an Angular component or a controller, and never type a limit into code:
every limit comes from the `ValidationLimit` table.

---

## UI conventions

The full look is in the Figma mockup. These are the rules people most often get wrong:

- **Use PrimeNG components** (table, dialog, dropdown, date picker, tag) before building your own.
- **Theme:** Vulcan navy `#263A78` as the primary color, **Inter** font, Vulcan logo top-left.
- **No internet (SLA NFR-3):** fonts, icons and images are bundled in the app. No CDN links, no Google Fonts.
- **Errors and warnings** use the shared field display (M1-45):
    - **Reject** is red. It blocks Save.
    - **Warn** is amber. It still saves.
    - In tables, ⚠ goes **after** the number, and the highlight covers **only the number**.
- **Synthetic data** shows a `SYNTHETIC` tag wherever a batch appears (SLA FR-20).

---

#### Shared AppField Usage

AppField wraps a PrimeNG form control and accepts five inputs:

- label
- inputId
- control
- limitKey
- hint

It introduces field state and validation messaging (local and server derived) sourcing from shared contracts (ValidationLimit, ValidationSeverity) and services (ValidationService, ReferenceDataService). ValidationService feeds local messages; control.errors.server feeds server messages.

The AppField component <app-field> can be reused across any form screen. How to use it:

```ts
import { FormControl } from '@angular/forms';

readonly mixWaterPh = new FormControl<number | null>(null);
```

```html
<app-field
    label="Mix Water pH"
    inputId="mixWaterPh"
    [control]="mixWaterPh"
    limitKey="Batch.MixWaterPh"
>
    <p-inputnumber inputId="mixWaterPh" [formControl]="mixWaterPh" />
</app-field>
```

### AppField Behavior

1. **Hint.** Provides guidance for expected or valid entry values. If `limitKey` matches a `ValidationLimit`, the hint comes from that data. If no match exists, AppField uses the explicit hint fallback.

2. **Local validation messages.** Via Angular control validation errors, AppField displays custom validator or default required messages. `ValidationService` supplies messages for Warn and Reject results.

    **Note:** AppField computes range errors internally, so `form.invalid` alone does not include out-of-range values. Parent Save wiring must use AppField state or add equivalent form-level validators.

3. **Server errors.** If an error comes from the server, the parent places the message on the control, and AppField displays it through `control.errors.server`.

    **Note:** The parent uses `matchFieldErrors()` to split API errors, calls `control.setErrors({ server: message })` for each matched field, and places unmatched messages in the page banner.

4. **State and save behavior.** AppField exposes normal, warn, and error states. Parent forms own the Save buttons, Save blocking, and warning summaries, using AppField’s validation state when they integrate it.

## Documentation

| File                                                     | What's in it                                                                          |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| [`docs/schema.md`](docs/schema.md)                       | Every field the app stores: name, type, unit, rules, plus the ERD diagram             |
| [`docs/validation-matrix.md`](docs/validation-matrix.md) | Every validation rule, its range, severity (Reject/Warn) and message                  |
| [`docs/calculation-method.md`](docs/calculation-method.md) | Strength prediction methodology, maturity equations, calibration coefficients, break-test adjustments, early warnings, and the VM-SYN-006 worked example |
| [`docs/api.md`](docs/api.md)                             | Every server endpoint, request/response shape and status code                         |
| [`docs/client-decisions.md`](docs/client-decisions.md)   | Decisions adopted from the client's Rev 1 spec and industry standards, with sources   |
| [`docs/adr/`](docs/adr/)                                 | Architecture decisions (SQLite, in-process API, canonical units, Angular in WebView2) |
| [`docs/m1-acceptance.md`](docs/m1-acceptance.md)         | The Milestone 1 test script used for the bug bash and the demo                        |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)                     | Branching, PR rules and the Definition of Done. **Read before your first PR.**        |

---

## How we work (short version)

1. Pick a ticket in Jira and move it to **In Progress**.
2. Branch from `dev` with the ticket key in the name:
   `feat/SCRUM-##-short-name` · `bugfix/SCRUM-##-short-name` · `doc/SCRUM-##-short-name`
3. Push daily and open a **draft PR** early.
4. When it's ready: CI green, request review, and a lead approves.
5. Squash-merge into `dev`.

`main` and `dev` are protected. Nobody pushes to them directly. Full rules are in [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Where the app keeps its files

| What           | Location                              |
| -------------- | ------------------------------------- |
| Database       | `%LOCALAPPDATA%\VulcanCBAS\vulcan.db` |
| Logs (14 days) | `%LOCALAPPDATA%\VulcanCBAS\logs\`     |
| Installed app  | `C:\Program Files\Vulcan CBAS\`       |

To start over with an empty database, close the app and delete `vulcan.db`. It's recreated on the next launch.

---

## Configuration

Settings live in `src/Vulcan.Api/appsettings.json`.

| Setting             | Default | What it does                                                                                                                                |
| ------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseMocks`          | `false` | `true` makes the screens use fake API responses inside the desktop app (for UI work before endpoints exist). `npm start` always uses mocks. |
| `SeedSyntheticData` | `false` | `true` loads the 9 demo batches from `data/synthetic/` on startup (turn on for demos)                                                       |

Keep `SeedSyntheticData` set to `false` in the repository. To turn it on for a demo, start the app with:

```powershell
& "C:\Program Files\Vulcan CBAS\Vulcan.Desktop.exe" --SeedSyntheticData=true
```

The `SeedSyntheticData=true` environment variable works too.

- The demo set loads **once**, next to any real batches, which are left unchanged. If synthetic batches are already there, nothing is added, so restarting never creates duplicates.
- A demo batch is skipped if a batch with the same number already exists.
- Demo batches are marked synthetic. Hide them with the **Synthetic data** switch in the top bar.

For personal overrides, use `appsettings.Development.local.json`. It's git-ignored.

---

## Data rules

- **Only synthetic data goes in this repo.** Everything in `data/synthetic/` is made up and marked
  `IsSynthetic = true` (SLA FR-20).
- **Real Vulcan data never leaves Vulcan's equipment** (SLA SC-6). If we ever receive real data, it goes in
  `data/vulcan/`, which is git-ignored so it can't be pushed. Never upload it to any cloud service.
- **Units:** °F, inches, pounds, PSI, ohms/ft, ppm. Mix percentages are **by volume**; the water-to-cement ratio is
  calculated **by weight**. All times are stored in UTC and shown in local time.

## Installing Vulcan CBAS

For Vulcan and anyone using the app. No .NET, Node or internet connection needed.

1. **Download the installer:**
   **[VulcanCBAS-Setup.exe](https://github.com/emmyatri/concrete-analysis-system/releases/download/v0.1.0-m2/VulcanCBAS-Setup.exe)**
   (about 265 MB, always the latest release). Older versions are on the
   [Releases page](https://github.com/emmyatri/concrete-analysis-system/releases).
2. **Double-click it.** Windows may say **"Windows protected your PC"** because the installer isn't signed yet. Click
   **More info → Run anyway**.
3. **Choose who it's for:**
    - **Install for me only**: no administrator rights needed. Pick this on a work laptop.
    - **Install for all users**: needs administrator rights; installs to `C:\Program Files\Vulcan CBAS`.
4. **Click Next** through the rest. Leave **Open Vulcan CBAS** ticked at the end.
5. Next time, open it from the **Start Menu → Vulcan CBAS**.

**Requirements:** Windows 11, 64-bit. The WebView2 runtime it needs is part of Windows 11; if it's missing, the
installer adds it from its own copy.

**Updating:** run the newer installer over the old one. Your data is kept.

**Uninstalling:** Settings → Apps → Vulcan CBAS → Uninstall. **Your data is kept** in `%LOCALAPPDATA%\VulcanCBAS`
(database, logs), so nothing is lost if you reinstall. To erase it too, delete that folder after uninstalling.

---

## Testing an installer from a branch

For reviewers checking a PR before it's released. Pick **one** way to get the installer:

**A. From GitHub (nothing to set up)**

1. On GitHub, open **Actions → CI → Run workflow**, pick the PR's branch, and click **Run workflow**.
2. Wait for the run to go green (about 10 minutes), open it, and download **vulcan-cbas-installer** under
   **Artifacts**.
3. It downloads as a `.zip`. **Unzip it first**, then run `VulcanCBAS-Setup.exe` from the unzipped folder.

**B. On your own computer (one command)**

```powershell
git switch <branch-name>
powershell -ExecutionPolicy Bypass -File installer\build.ps1
```

The installer appears in `installer\output\`. See [Building the installer](#building-the-installer) for what the
script does.

**Then check:**

| #   | Do this                                                     | Pass if                                                                                  |
| --- | ----------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| 1   | Run the installer, choose **Install for all users**         | Admin prompt appears; installs to `C:\Program Files\Vulcan CBAS`; app opens at the end   |
| 2   | Look at the side menu footer                                | It shows the version, e.g. **v0.1.0-m1**                                                 |
| 3   | Start Menu → type **Vulcan**                                | **Vulcan CBAS** is listed and opens the app                                              |
| 4   | Open `%LOCALAPPDATA%\VulcanCBAS\`                           | `vulcan.db` and a `logs` folder are there                                                |
| 5   | Close the app, run the **same installer again**             | Upgrades with no errors; the app still opens                                             |
| 6   | Settings → Apps → Vulcan CBAS → **Uninstall**               | Program folder and Start Menu entry are gone; **`vulcan.db` is still there**             |
| 7   | Run the installer again, choose **Install for me only**     | **No** admin prompt; installs to `%LOCALAPPDATA%\Programs\Vulcan CBAS`; app opens        |
| 8   | Uninstall that copy                                         | Uninstalls cleanly, no admin prompt                                                      |

The installed app talks to the real local server, not fake data. If a screen shows "Something went wrong on the local
service", the server endpoints for that screen haven't been merged yet. That's not an installer problem.

To see exactly what an install did, run it with a log:
`.\VulcanCBAS-Setup.exe /LOG="$env:TEMP\vulcan-setup.log"` (use whichever name your copy has)

---

## Building the installer

**Easiest: let CI build it.** Every push to `dev` and `main` builds the installer (download **vulcan-cbas-installer**
from the run's Artifacts), and tagging a version on `main` (e.g. `v0.1.0-m1`) publishes it as a GitHub Release, which
is what the download link in [Installing Vulcan CBAS](#installing-vulcan-cbas) points to.

**On your own computer**, one command from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build.ps1
```

It writes `installer\output\VulcanCBAS-Setup-<version>.exe`. You need Node.js and the .NET 10 SDK from
[Prerequisites](#prerequisites); the script installs Inno Setup if you don't have it. The first run takes a few
minutes because it downloads the WebView2 runtime (about 200 MB, once).

<details>
<summary>What the script does, step by step</summary>

1. `npm ci` and `npm run build` in `src/Vulcan.Web` (the screens, into `src/Vulcan.Api/wwwroot`)
2. `dotnet publish src/Vulcan.Desktop -p:PublishProfile=win-x64-selfcontained`: a self-contained folder in
   `src/Vulcan.Desktop/bin/publish/win-x64/` with the app, its own .NET runtime, the screens, SQLite and the WebView2
   loader. CI also uploads this folder as **vulcan-cbas-win-x64**, which you can unzip and run without installing.
3. Downloads Microsoft's offline WebView2 runtime to `installer/redist/` (git-ignored)
4. Finds Inno Setup 6, or installs it with `winget`
5. Compiles `installer/vulcan.iss` into `installer/output/` (git-ignored)

</details>

**Don't copy just `Vulcan.Desktop.exe` somewhere else.** It's a small launcher that needs the hundreds of files next
to it. Share the installer instead.

**New version:** bump `<Version>` in `Directory.Build.props`, `version` in `src/Vulcan.Web/src/app/core/app-info.ts`
`AppVersion` in `installer/vulcan.iss`, and "Current version" at the top of this README. Merge to `main`, then tag it:

```powershell
git switch main; git pull
git tag v0.1.0-m1
git push origin v0.1.0-m1
```

## Troubleshooting

| Problem                                    | Fix                                                                           |
| ------------------------------------------ | ----------------------------------------------------------------------------- |
| `dotnet format` fails in CI                | Run `dotnet format` locally and commit the changes                            |
| App won't start / crashes                  | Check the latest file in `%LOCALAPPDATA%\VulcanCBAS\logs\`                    |
| App opens to a blank white window          | Install the WebView2 Runtime, or run `npm ci` in `src/Vulcan.Web` and rebuild |
| Screen changes don't show in the app       | Rebuild: the app shows the last Angular build, not your live code             |
| `npm ci` fails                             | Check you're on Node LTS; delete `node_modules` and retry                     |
| Database looks wrong after a schema change | Delete `vulcan.db`; migrations recreate it                                    |
| Can't push to `dev` or `main`              | Expected: open a pull request instead                                         |

---

## Contacts

Questions about the project go in the team channel. Only the Project Lead and Co-Lead contact the client, and every
client email copies the faculty supervisor (SLA 6.2).

---

## Ownership

This software is being built for Vulcan Materials. At final delivery, the complete source code transfers to a repository
under Vulcan Materials' control (SLA §4). Third-party packages must allow commercial use; check with a lead before
adding any new NuGet or npm package.
