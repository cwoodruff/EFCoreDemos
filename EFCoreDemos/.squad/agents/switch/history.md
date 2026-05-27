# Project Context

- **Owner:** Morpheus
- **Project:** EFCoreDemos
- **Stack:** .NET, C#, Entity Framework Core, ASP.NET Core, SQLite, SQL Server
- **Created:** 2026-05-27T05:53:14.442+02:00

## Learnings

- Most demos are intentionally self-contained EF Core samples instead of layers built around shared libraries.
- Runtime changes may affect the launcher and lightweight health-check tests, even when a demo itself is otherwise isolated.

## Session: 2026-05-27T04:32:09Z

**Benchmark Verification (AsNoTracking Perf + Silent Bug)**
- Reviewed simulated benchmark for Morpheus.
- Confirmed launcher values are not reversed.
- DemoLauncher.Web/benchmarks/as-no-tracking-perf.txt matches artifacts.
- Intentional behavior: tracked queries faster on Chinook Track table.
- Status: Verified and ready.
