---
name: sprint
description: Run a sprint over a GitHub milestone in cwoodruff/EFCoreDemos — implement every open issue on its own branch, open and merge PRs when verified, then report. Use when asked to "start a new sprint for the X milestone" or /sprint <milestone>.
argument-hint: <milestone name>
---

# /sprint <milestone>

Do every open issue in the milestone `$ARGUMENTS`. You are authorized to commit, push, create and merge PRs on your own once the work checks out.

1. `gh issue list --milestone "$ARGUMENTS" --state open --json number,title,body`. If the milestone doesn't exist or has no open issues, stop and say so.
2. For each issue, on its own branch `feature/<number>-<short-slug>` from an up-to-date `main` (independent issues can run in parallel subagents, each in its own worktree):
   - Build the demo as a new project folder under `EFCoreDemos/`, added to `EFCoreDemos.sln`.
   - Add the demo to `EFCoreDemos/DemoLauncher.Web/demos.json` so it appears in the Launcher, including its info-panel details.
   - Add tests in `EFCoreDemos/EFCoreDemos.Tests`.
   - Link the demo from the root `README.md`.
   - Verify: solution builds with 0 warnings, full test suite passes, and the demo runs.
   - Commit, push, and open a PR whose body starts with `Closes #<number>` and has a `## What` section.
3. Merge each PR only after its checks above pass; rebase later branches on `main` if they conflict. Leave a PR open and flag it if anything fails.
4. After all issues are merged, pull `main`, rebuild and rerun the full suite once.
5. Report a table: issue, PR, merged or not, test result. Don't close the milestone; ask first.
