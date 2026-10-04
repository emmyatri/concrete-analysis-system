# Vulcan CBAS — Concrete Batch Analysis & Strength Prediction System

A Windows desktop application for Vulcan Materials that records concrete batch data (mix recipe, cement chemistry, test
cylinders, curing readings and break results), validates every value against DOT limits, and lets operators find any
batch by its number. Later milestones add strength charts and 3/7/28-day strength predictions.

Built by the KSU Student Development Team for SWE 4713 (Section W01), under the signed Service Level Agreement with
Vulcan Materials.

---

## Status

| Milestone                               | Scope                                                        | Due                  |
| --------------------------------------- | ------------------------------------------------------------ | -------------------- |
| **M1 — Batch Record & Data Validation** | Installable build, batch/cylinder/curing/break entry, validation, search by batch number | **Fri, Oct 9, 2026** |
| M2 — Strength Charting & Prediction     | PSI-vs-time chart, 3/7/28-day predictions, early-warning flags | Fri, Oct 30, 2026    |
| M3 — Calibration & Reporting            | Calibration against real breaks, printable/exportable reports | Fri, Nov 20, 2026    |
| Final delivery                          | Signed installer, docs, training, source handoff             | Fri, Dec 4, 2026     |

Current version: `0.1.0-m1` (pre-release)

---

## Tech stack

| Layer              | Choice                                                       |
| ------------------ | ------------------------------------------------------------ |
| Language / runtime | C# on .NET 10                                                |
| Desktop UI         | **Angular + PrimeNG** in WebView2, hosted by WPF             |
| Desktop host       | WPF window that starts the server and shows WebView2         |
| Server             | ASP.NET Core, hosted **inside** the desktop app on `127.0.0.1` |
| Database           | SQLite via EF Core                                           |
| Tests              | xUnit + Shouldly (.NET), `ng test` (Angular)                 |
| Logging            | Serilog (rolling file)                                       |
| Installer          | Inno Setup (bundles the WebView2 runtime)                    |
| CI                 | GitHub Actions (`windows-latest`)                            |

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
- **Visual Studio 2026** (with the *.NET desktop development* and *ASP.NET and web development* workloads) or
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
- hint.

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

## Documentation

| File                                                     | What's in it                                                 |
| -------------------------------------------------------- | ------------------------------------------------------------ |
| [`docs/schema.md`](docs/schema.md)                       | Every field the app stores: name, type, unit, rules, plus the ERD diagram |
| [`docs/validation-matrix.md`](docs/validation-matrix.md) | Every validation rule, its range, severity (Reject/Warn) and message |
| [`docs/api.md`](docs/api.md)                             | Every server endpoint, request/response shape and status code |
| [`docs/client-decisions.md`](docs/client-decisions.md)   | Decisions adopted from the client's Rev 1 spec and industry standards, with sources |
| [`docs/adr/`](docs/adr/)                                 | Architecture decisions (SQLite, in-process API, canonical units, Angular in WebView2) |
| [`docs/m1-acceptance.md`](docs/m1-acceptance.md)         | The Milestone 1 test script used for the bug bash and the demo |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)                     | Branching, PR rules and the Definition of Done. **Read before your first PR.** |

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

| Setting             | Default | What it does                                                 |
| ------------------- | ------- | ------------------------------------------------------------ |
| `UseMocks`          | `false` | `true` makes the screens use fake API responses inside the desktop app (for UI work before endpoints exist). `npm start` always uses mocks. |
| `SeedSyntheticData` | `false` | `true` loads `data/synthetic/` into an empty database (turn on for demos) |

For personal overrides, use `appsettings.Development.local.json`. It's git-ignored.

---

## Data rules

- **Only synthetic data goes in this repo.** Everything in `data/synthetic/` is made up and marked
  `IsSynthetic = true` (SLA FR-20).
- **Real Vulcan data never leaves Vulcan's equipment** (SLA SC-6). If we ever receive real data, it goes in
  `data/vulcan/`, which is git-ignored so it can't be pushed. Never upload it to any cloud service.
- **Units:** °F, inches, pounds, PSI, ohms/ft, ppm. Mix percentages are **by volume**; the water-to-cement ratio is
  calculated **by weight**. All times are stored in UTC and shown in local time.

---

## Building the installer

CI builds the installer automatically on every push to `dev` and `main`; download it from the workflow run's **Artifacts**.

To build locally:

```powershell
dotnet publish src/Vulcan.Desktop -p:PublishProfile=win-x64-selfcontained
# then compile installer/vulcan.iss with Inno Setup
```

The installer is self-contained and works offline (SLA NFR-1, NFR-3). The target computer does not need .NET, Node or
internet access. If the WebView2 runtime is missing, the installer installs it from the copy bundled inside.

---

## Troubleshooting

| Problem                                    | Fix                                                          |
| ------------------------------------------ | ------------------------------------------------------------ |
| `dotnet format` fails in CI                | Run `dotnet format` locally and commit the changes           |
| App won't start / crashes                  | Check the latest file in `%LOCALAPPDATA%\VulcanCBAS\logs\`   |
| App opens to a blank white window          | Install the WebView2 Runtime, or run `npm ci` in `src/Vulcan.Web` and rebuild |
| Screen changes don't show in the app       | Rebuild: the app shows the last Angular build, not your live code |
| `npm ci` fails                             | Check you're on Node LTS; delete `node_modules` and retry    |
| Database looks wrong after a schema change | Delete `vulcan.db`; migrations recreate it                   |
| Can't push to `dev` or `main`              | Expected: open a pull request instead                        |

---

## Contacts

Questions about the project go in the team channel. Only the Project Lead and Co-Lead contact the client, and every
client email copies the faculty supervisor (SLA 6.2).

---

## Ownership

This software is being built for Vulcan Materials. At final delivery, the complete source code transfers to a repository
under Vulcan Materials' control (SLA §4). Third-party packages must allow commercial use; check with a lead before
adding any new NuGet or npm package.
