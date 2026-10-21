# Copilot review instructions — Vulcan CBAS

## Project context
- Student project for SWE 4713, one semester. Single Windows 11 workstation, single user, single app instance.
- WPF desktop app hosting an in-process ASP.NET Core API on 127.0.0.1 (ADR-002). SQLite database (ADR-001).
- No multi-user, multi-server, cloud or high-availability requirements. Don't suggest them.

## Architecture rules (flag violations)
- WPF must never access EF Core, SQLite or application services directly. All data goes through the API over HTTP.
- Validation rules and limits live only in `Vulcan.Domain` / `ValidationLimit` data. Flag hard-coded ranges anywhere else.
- Units are converted only in `Vulcan.Domain.Units`. Field names must match `docs/schema.md` exactly.
- Times are stored in UTC and displayed in local time.
- No deletes in Milestone 1.

## What to prioritize
1. Correctness bugs, unhandled exceptions, data loss.
2. Violations of the rules above.
3. Missing edge-case tests for validation rules (min, max, just below, just above).

## What not to flag
- Scaling, concurrency across machines, or performance tuning.
- Release/versioning strategy (no installer yet).
- Style issues already enforced by `dotnet format` and `.editorconfig`.
- Suggestions to add new NuGet packages. Package changes require lead review.

## Tone
- Only comment when something should change. Mark optional suggestions as "Nit:".
