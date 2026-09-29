# Vulcan CBAS — Concrete Batch Analysis & Strength Prediction System

A Windows desktop application for Vulcan Materials that records concrete batch data (mix recipe, cement chemistry, test
cylinders, curing readings and break results), validates every value against DOT limits, and lets operators find any
batch by its number. Later milestones add strength charts and 3/7/28-day strength predictions.

Built by the KSU Student Development Team for SWE 4713 (Section W01), under the signed Service Level Agreement with
Vulcan Materials.

---

## Status

| Milestone                               | Scope                                                                                    | Due                  |
|-----------------------------------------|------------------------------------------------------------------------------------------|----------------------|
| **M1 — Batch Record & Data Validation** | Installable build, batch/cylinder/curing/break entry, validation, search by batch number | **Fri, Oct 9, 2026** |
| M2 — Strength Charting & Prediction     | PSI-vs-time chart, 3/7/28-day predictions, early-warning flags                           | Fri, Oct 30, 2026    |
| M3 — Calibration & Reporting            | Calibration against real breaks, printable/exportable reports                            | Fri, Nov 20, 2026    |
| Final delivery                          | Signed installer, docs, training, source handoff                                         | Fri, Dec 4, 2026     |

Current version: `0.1.0-m1` (pre-release)

---

## Tech stack

| Layer              | Choice                                                         |
|--------------------|----------------------------------------------------------------|
| Language / runtime | C# on .NET 10                                                  |
| Desktop UI         | WPF, MVVM (CommunityToolkit.Mvvm)                              |
| Server             | ASP.NET Core, hosted **inside** the desktop app on `127.0.0.1` |
| Database           | SQLite via EF Core                                             |
| Tests              | xUnit + Shouldly                                               |
| Logging            | Serilog (rolling file)                                         |
| Installer          | Inno Setup                                                     |
| CI                 | GitHub Actions (`windows-latest`)                              |

The reasons behind these choices are in [`docs/adr/`](docs/adr/).

---

## Prerequisites

- **Windows 10 or 11, 64-bit.** WPF does not build or run on macOS or Linux. Mac users need a Windows VM or lab machine.
- **.NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
- **Visual Studio 2026** with these workloads:
    - .NET desktop development
    - ASP.NET and web development
- **Git**

Check your setup:

```powershell
dotnet --version   # should start with 10.
git --version
```

---

## Getting started

```powershell
git clone https://github.com/emmyatri/concrete-analysis-system.git
cd concrete-analysis-system
git checkout dev
dotnet build
dotnet test
```

Both commands should finish with **0 warnings and 0 errors**. Warnings are treated as errors, so a warning will fail the
build.

### Run the app

Open `Vulcan.sln` in Visual Studio, set **Vulcan.Desktop** as the startup project, and press **F5**.

Or from the command line:

```powershell
dotnet run --project src/Vulcan.Desktop
```

On first launch the app asks for a technician name, creates its database, and starts its built-in server. No setup is
needed.

---

## Project structure

```
Vulcan.sln
├─ src/
│  ├─ Vulcan.Domain/          Data classes and every validation rule. Uses nothing else.
│  ├─ Vulcan.Application/     App logic (services) and the data shapes sent to/from the API.
│  ├─ Vulcan.Prediction/      Strength prediction (empty until Milestone 2).
│  ├─ Vulcan.Infrastructure/  Database (EF Core + SQLite), audit stamping, logging.
│  ├─ Vulcan.Api/             Server endpoints; re-checks every value (SLA NFR-6).
│  └─ Vulcan.Desktop/         The WPF window and screens; hosts the API.
├─ tests/                     One test project per src project.
├─ data/
│  └─ synthetic/              Fake test/demo data. Every record is marked IsSynthetic.
├─ installer/                 Inno Setup script.
├─ docs/                      Schema, API, validation rules, decisions (see below).
└─ .github/                   CI workflow, PR template, CODEOWNERS.
```

**The one rule that matters most:** validation lives in `Vulcan.Domain` and nowhere else. The screens and the API both
call the same rules. Never write a range check in a ViewModel or controller, and never type a limit into code: every
limit comes from the `ValidationLimit` table.

---

## Documentation

| File                                                     | What's in it                                                                        |
|----------------------------------------------------------|-------------------------------------------------------------------------------------|
| [`docs/schema.md`](docs/schema.md)                       | Every field the app stores: name, type, unit, rules, plus the ERD diagram           |
| [`docs/validation-matrix.md`](docs/validation-matrix.md) | Every validation rule, its range, severity (Reject/Warn) and message                |
| [`docs/api.md`](docs/api.md)                             | Every server endpoint, request/response shape and status code                       |
| [`docs/client-decisions.md`](docs/client-decisions.md)   | Decisions adopted from the client's Rev 1 spec and industry standards, with sources |
| [`docs/adr/`](docs/adr/)                                 | Architecture decisions (SQLite, in-process API, canonical units)                    |
| [`docs/m1-acceptance.md`](docs/m1-acceptance.md)         | The Milestone 1 test script used for the bug bash and the demo                      |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)                     | Branching, PR rules and the Definition of Done. **Read before your first PR.**      |

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
|----------------|---------------------------------------|
| Database       | `%LOCALAPPDATA%\VulcanCBAS\vulcan.db` |
| Logs (14 days) | `%LOCALAPPDATA%\VulcanCBAS\logs\`     |
| Installed app  | `C:\Program Files\Vulcan CBAS\`       |

To start over with an empty database, close the app and delete `vulcan.db`. It's recreated on the next launch.

---

## Configuration

Settings live in `src/Vulcan.Desktop/appsettings.json`.

| Setting             | Default | What it does                                                                         |
|---------------------|---------|--------------------------------------------------------------------------------------|
| `UseMocks`          | `false` | `true` makes the screens use fake API responses (for UI work before endpoints exist) |
| `SeedSyntheticData` | `false` | `true` loads `data/synthetic/` into an empty database (turn on for demos)            |

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

CI builds the installer automatically on every push to `dev` and `main`; download it from the workflow run's **Artifacts
**.

To build locally:

```powershell
dotnet publish src/Vulcan.Desktop -p:PublishProfile=win-x64-selfcontained
# then compile installer/vulcan.iss with Inno Setup
```

The installer is self-contained: the target computer does not need .NET installed.

---

## Troubleshooting

| Problem                                    | Fix                                                        |
|--------------------------------------------|------------------------------------------------------------|
| `dotnet format` fails in CI                | Run `dotnet format` locally and commit the changes         |
| App won't start / crashes                  | Check the latest file in `%LOCALAPPDATA%\VulcanCBAS\logs\` |
| Database looks wrong after a schema change | Delete `vulcan.db`; migrations recreate it                 |
| Can't push to `dev` or `main`              | Expected: open a pull request instead                      |

---

## Contacts

Questions about the project go in the team channel. Only the Project Lead and Co-Lead contact the client, and every
client email copies the faculty supervisor (SLA 6.2).

---

## Ownership

This software is being built for Vulcan Materials. At final delivery, the complete source code transfers to a repository
under Vulcan Materials' control (SLA §4). Third-party packages must allow commercial use; check with a lead before
adding any new NuGet package.
