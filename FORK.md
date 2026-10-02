# Custom Greenshot fork

This is a personal fork of [greenshot/greenshot](https://github.com/greenshot/greenshot), with a few extra features on top of upstream `main`.

## Branches

| Branch | Role |
|---|---|
| `main` | Exact mirror of `upstream/main`. Never commit to it. Only fast-forward. |
| `feature/*` | One custom feature per branch, based on `main`. Rebased when upstream moves. |
| `fork/meta` | This file and the update script. |
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
