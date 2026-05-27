# Project Context

- **Owner:** Morpheus
- **Project:** EFCoreDemos
- **Stack:** .NET, C#, Entity Framework Core, ASP.NET Core, SQLite, SQL Server
- **Created:** 2026-05-27T05:53:14.442+02:00

## Learnings

- The existing test suite is lightweight and health-focused, so direct demo runs may matter more than unit tests for behavior changes.
- Changes in provider configuration, launcher wiring, or context construction are likely regression hotspots.

## Session: 2026-05-27T04:32:09Z

**Benchmark Verification (AsNoTracking Perf + Silent Bug)**
- Reviewed simulated benchmark for Morpheus.
- Confirmed launcher values are not reversed.
- DemoLauncher.Web/benchmarks/as-no-tracking-perf.txt matches artifacts.
- Intentional behavior: tracked queries faster on Chinook Track table.
- Status: Verified and ready.

## Session: 2026-05-27T09:56:54.915+02:00

**executeupdate-executedelete Track-Table Exception Fix**
- Root cause #1: `chinook.db` in project folder was 0 bytes (empty placeholder). Real 770KB db copied from `/database/chinook.db`.
- Root cause #2 (pre-existing, masked by #1): `Program.cs` line 18 had a negated WHERE condition (`!c.Invoices.Any(...)`) that attempted to delete ALL Chinook customers — all of whom have invoices, causing `SQLite Error 19: FOREIGN KEY constraint failed`. The comment said "Deleting customers with invoices older than 2005" but the code was inverted. Removed the `!`.
- `CopyToOutputDirectory Always` was already present in the `.csproj` (no change needed).
- Both fixes verified: UPDATE on Track runs cleanly; DELETE on Customer executes without exception (0 rows affected — correct, since no Chinook invoices predate 2005).
- All 28 existing tests pass. No regressions.
- Key files: `executeupdate-executedelete/chinook.db`, `executeupdate-executedelete/Program.cs:18`
- **Team Decision:** Validated and approved. Formalized SQLite project pattern (valid db + CopyToOutputDirectory) in decisions.md for future reference.

