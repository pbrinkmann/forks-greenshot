<#
.SYNOPSIS
    Bring the fork up to date with upstream and rebuild the "custom" branch.

.DESCRIPTION
    1. Fetch upstream and origin, fast-forward main to upstream/main
    2. Rebase fork/meta and every branch in fork/branches.txt onto main
    3. Recreate "custom" from main and merge fork/meta plus every feature branch into it
    4. Optionally tag the result and push everything to origin

    Stops on the first conflict, resolve it and run the script again: branches which are already up to date are skipped.
    See FORK.md for the branch layout.

.PARAMETER Push
    Push main, fork/meta, the feature branches and custom to origin (rebased branches with --force-with-lease)

.PARAMETER Tag
    Tag the rebuilt custom branch as custom-<date>-<upstream commit>

.PARAMETER NoFetch
    Don't fetch, work with the refs which are already there
#>
[CmdletBinding()]
param(
    [switch]$Push,
    [switch]$Tag,
    [switch]$NoFetch
)

$ErrorActionPreference = 'Stop'

$MainBranch = 'main'
$MetaBranch = 'fork/meta'
$CustomBranch = 'custom'
$BranchListPath = 'fork/branches.txt'

function Invoke-Git {
    git @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Test-GitPath([string]$name) {
    Test-Path (git rev-parse --git-path $name)
}

function Stop-WithMessage([string]$message) {
    Write-Host ''
    Write-Host $message -ForegroundColor Yellow
    exit 1
}

# The order matters: features are merged into custom in the order of the list
function Get-FeatureBranches {
    $content = git show "${MetaBranch}:$BranchListPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Couldn't read $BranchListPath from $MetaBranch"
    }
    $content | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') }
}

function Get-UnmergedFiles {
    git diff --name-only --diff-filter=U
}

Set-Location (git rev-parse --show-toplevel)

if ((Test-GitPath 'rebase-merge') -or (Test-GitPath 'rebase-apply')) {
    Stop-WithMessage 'A rebase is in progress: finish it (git rebase --continue) or abort it, then run this script again.'
}
if (Test-GitPath 'MERGE_HEAD') {
    Stop-WithMessage 'A merge is in progress: commit or abort it, then run this script again.'
}
if (git status --porcelain --untracked-files=no) {
    Stop-WithMessage 'The working tree has uncommitted changes, commit or stash them first.'
}

# 1. Sync main with upstream
if (-not $NoFetch) {
    Write-Host "Fetching upstream and origin" -ForegroundColor Cyan
    Invoke-Git fetch upstream --prune
    Invoke-Git fetch origin --prune
}

Write-Host "Fast-forwarding $MainBranch to upstream/$MainBranch" -ForegroundColor Cyan
Invoke-Git switch --quiet $MainBranch
git merge --ff-only --quiet "upstream/$MainBranch"
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "$MainBranch has commits which aren't in upstream/$MainBranch. It must stay a mirror of upstream, move those commits to a feature branch."
}

# 2. Rebase meta and the features onto main
$featureBranches = @(Get-FeatureBranches)
foreach ($branch in @($MetaBranch) + $featureBranches) {
    git rev-parse --verify --quiet "refs/heads/$branch" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Stop-WithMessage "Branch $branch from $BranchListPath doesn't exist."
    }

    git merge-base --is-ancestor $MainBranch $branch
    if ($LASTEXITCODE -eq 0) {
        Write-Host "$branch is up to date" -ForegroundColor DarkGray
        continue
    }

    Write-Host "Rebasing $branch onto $MainBranch" -ForegroundColor Cyan
    git rebase $MainBranch $branch
    if ($LASTEXITCODE -ne 0) {
        Stop-WithMessage "Rebasing $branch stopped on a conflict. Resolve it, run 'git rebase --continue' (or 'git rebase --abort'), then run this script again."
    }
}

# The list may have changed with the rebase of meta
$featureBranches = @(Get-FeatureBranches)

# 3. Rebuild custom from scratch
Write-Host "Rebuilding $CustomBranch" -ForegroundColor Cyan
Invoke-Git switch --quiet -C $CustomBranch $MainBranch
foreach ($branch in @($MetaBranch) + $featureBranches) {
    Write-Host "  merging $branch"
    git merge --no-ff --no-edit --quiet $branch
    if ($LASTEXITCODE -ne 0) {
        if (Get-UnmergedFiles) {
            Stop-WithMessage "Merging $branch into $CustomBranch conflicts with an earlier feature. Resolve and commit it, then run this script again: rerere remembers the resolution for the next rebuild. Consider basing $branch on the feature it conflicts with, see FORK.md."
        }
        # rerere (with rerere.autoUpdate) resolved everything
        Write-Host "  conflicts resolved by rerere" -ForegroundColor DarkGray
        Invoke-Git commit --no-edit --quiet
    }
}

# 4. Tag and push
$tagName = $null
if ($Tag) {
    $tagName = "custom-$(Get-Date -Format 'yyyy-MM-dd')-$(git rev-parse --short=8 $MainBranch)"
    git rev-parse --verify --quiet "refs/tags/$tagName" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Tag $tagName already exists, not tagging" -ForegroundColor Yellow
        $tagName = $null
    }
    else {
        Invoke-Git tag $tagName $CustomBranch
        Write-Host "Tagged $tagName" -ForegroundColor Cyan
    }
}

if ($Push) {
    Write-Host "Pushing to origin" -ForegroundColor Cyan
    Invoke-Git push origin $MainBranch
    Invoke-Git push --force-with-lease --set-upstream origin $MetaBranch @featureBranches $CustomBranch
    if ($tagName) {
        Invoke-Git push origin $tagName
    }
}

Write-Host ''
Write-Host "Done, $CustomBranch is checked out:" -ForegroundColor Green
git log --oneline --first-parent "$MainBranch..$CustomBranch"
