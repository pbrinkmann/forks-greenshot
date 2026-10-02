# Custom Greenshot fork

This is a personal fork of [greenshot/greenshot](https://github.com/greenshot/greenshot), with a few extra features on top of upstream `main`.

## Cheat sheet

The scripts need a clean working tree, run them from the repository root.

| I want to... | Do this |
|---|---|
| **Build my Greenshot** | `git switch custom`<br>`pwsh fork/Build.ps1` (Release, use `-Configuration Debug` for Debug)<br>Result: `src/Greenshot/bin/Release/net480/Greenshot.exe` |
| **Get the latest upstream changes** | `git switch custom`<br>`pwsh fork/Update-Fork.ps1 -Push`<br>then build |
| **Start a new feature** | `git switch -c feature/x main`, work and commit<br>`git switch fork/meta`, add `feature/x` to `fork/branches.txt` and the Features table below, commit<br>`pwsh fork/Update-Fork.ps1 -Push` |
| **Fix or change an existing feature** | `git switch feature/x`, work and commit<br>`pwsh fork/Update-Fork.ps1 -Push` (rebuilds `custom` with the change) |
| **Undo an accidental commit on `custom` or `main`** | `git switch feature/x`, then `git cherry-pick <commit>`<br>The script recreates `custom`, which drops the commit there. If it was on `main`, the script refuses to run until `main` is reset: `git switch main`, then `git reset --hard upstream/main`. |
| **Script stopped: rebase conflict** | Fix the files, `git add` them, `git rebase --continue`, then rerun the script |
| **Script stopped: merge conflict in `custom`** | Two features touch the same code. Fix the files, `git add` them, `git commit`, then rerun the script (rerere replays the fix next time) |
| **Back out of a half-done rebase** | `git rebase --abort` (the branch is back to how it was) |
| **Drop a feature (e.g. upstream merged it)** | On `fork/meta`: remove it from `fork/branches.txt`, update the table below, commit, rerun the script |
| **See what's in my build** | `git log --oneline --first-parent main..custom` |
| **See a feature's full change** | `git diff main...feature/x` |
| **Read this file from any branch** | `git show fork/meta:FORK.md` |
| **Set up a fresh clone** | `git clone https://github.com/pbrinkmann/forks-greenshot.git`<br>`git remote add upstream https://github.com/greenshot/greenshot.git`<br>`git remote set-url --push upstream DISABLED`<br>`git config rerere.enabled true`<br>`git fetch upstream`, then `git branch -u upstream/main main`<br>`git switch fork/meta` and each branch in `fork/branches.txt` once, which creates the local branches the script needs |

## Branches

| Branch | Role |
|---|---|
| `main` | Exact mirror of `upstream/main`. Never commit to it. Only fast-forward. |
| `feature/*` | One custom feature per branch, based on `main`. Rebased when upstream moves. |
| `fork/meta` | This file, the update script, and the build script. |
| `custom` | What gets built and used: `main` + `fork/meta` + every feature. **Rebuilt from scratch** by the script, so never commit to it. |

Remotes: `origin` = this fork (pbrinkmann/forks-greenshot), `upstream` = greenshot/greenshot (push disabled).

## Features

| Branch | Description | Status |
|---|---|---|
| `feature/select-region-tool` | "Select region" tool in the editor, to copy or cut part of the image (pixels, including the elements on top) | Local only |
| `feature/text-autosize` | Text boxes and speech bubbles grow to fit their text while typing, until the user resizes the width | Local only |

Status values: *Local only*, *Upstream PR #nnn*, *Merged upstream (drop)*.
When a feature is merged upstream, remove it from `fork/branches.txt` and mark it here. Delete the branch later.

## Dropped

| Change | Reason |
|---|---|
| ColorDialog HiDPI layout fix (was part of the text resize branch) | Upstream replaced the WinForms ColorDialog with a WPF `ColorPickerWindow` (19dc47ae) |
| HTML clipboard size fix (was part of the select region branch) | Upstream's clipboard rewrite (82984641) takes the HTML size from the copied image |

## Updating from upstream

Have a clean working tree, then from the `custom` branch (or any branch which has this script):

```powershell
pwsh fork/Update-Fork.ps1            # sync main, rebase features, rebuild custom
pwsh fork/Update-Fork.ps1 -Push      # ...and push everything to origin
pwsh fork/Update-Fork.ps1 -Tag       # ...and tag the result, e.g. custom-2026-10-02-30573f78
```

When a rebase stops on a conflict: resolve it, `git rebase --continue`, and run the script again.
It's safe to rerun because branches that are already rebased are left alone.

`git rerere` is enabled, so conflicts are only resolved once. With `git config rerere.autoUpdate true`, the script continues by itself when rerere resolved everything during the rebuild of `custom`.

## Adding a feature

1. `git switch -c feature/x main`, then work and commit.
2. On `fork/meta`: add the branch to `fork/branches.txt` and to the table above, and commit.
3. Run the update script.
