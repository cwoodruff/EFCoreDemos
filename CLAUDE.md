# EFCoreDemos

- Repo `cwoodruff/EFCoreDemos`. Solution lives in `EFCoreDemos/` (`EFCoreDemos.sln`); tests in `EFCoreDemos/EFCoreDemos.Tests`.
- Every demo is listed in `EFCoreDemos/DemoLauncher.Web/demos.json` and linked from the root `README.md`. A new demo isn't done until both are updated.

## Git
- After a fix builds with 0 warnings and the tests pass, commit and push to the current branch without asking.
- Never commit local database files (e.g. `concurrency.db`, `identifier.sqlite`) or IDE data-source changes unless asked.
