# Project Context

- **Owner:** Morpheus
- **Project:** EFCoreDemos
- **Stack:** .NET, C#, Entity Framework Core, ASP.NET Core, SQLite, SQL Server
- **Created:** 2026-05-27T05:53:14.442+02:00

## Learnings

- Most demos are intentionally self-contained EF Core samples instead of layers built around shared libraries.
- Runtime changes may affect the launcher and lightweight health-check tests, even when a demo itself is otherwise isolated.

## Session: 2026-05-27T09:56:54Z

**Fix: executeupdate-executedelete SQLite Track table exception**
- Root cause: `chinook.db` in project root was 0 bytes (empty file placeholder).
- The csproj also lacked `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` for `chinook.db`, meaning even a valid source db would not reach the output directory.
- Fix: replaced the empty `chinook.db` with the populated canonical database from `database/chinook.db`, and added the `None Update` item group to the csproj.
- Build confirmed clean; Track table verified present in both project root and `bin/Debug/net10.0/`.
- Pattern: every SQLite demo project needs both a populated `chinook.db` and the `CopyToOutputDirectory` directive — check both when diagnosing missing-table errors.
- **Team Decision:** Formalized as "SQLite Demo Projects Pattern" in decisions.md; applies to all future SQLite demo projects.

## Session: 2026-05-27T04:32:09Z

**Benchmark Verification (AsNoTracking Perf + Silent Bug)**
- Reviewed simulated benchmark for Morpheus.
- Confirmed launcher values are not reversed.
- DemoLauncher.Web/benchmarks/as-no-tracking-perf.txt matches artifacts.
- Intentional behavior: tracked queries faster on Chinook Track table.
- Status: Verified and ready.
