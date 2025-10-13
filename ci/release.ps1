#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Release script for OSDP.Net project
.DESCRIPTION
    This script automates the release process by merging develop into master and triggering CI/CD pipeline
.PARAMETER DryRun
    Perform a dry run without making actual changes
#>

param(
    [switch]$DryRun = $false
)

# Color functions for better output
function Write-ColorOutput($ForegroundColor) {
    # Store the current color
    $fc = $host.UI.RawUI.ForegroundColor
    # Set the new color
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    
    # Output
    if ($args) {
        Write-Output $args
    } else {
        $input | Write-Output
    }
    
    # Restore the original color
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Info($message) { Write-ColorOutput Cyan $message }
function Write-Success($message) { Write-ColorOutput Green $message }
function Write-Warning($message) { Write-ColorOutput Yellow $message }
function Write-Error($message) { Write-ColorOutput Red $message }

Write-Info "=== OSDP.Net Release Script ==="
Write-Info ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - No changes will be made"
    Write-Info ""
}

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Error "Error: Not in a git repository"
    exit 1
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Error "Error: You have uncommitted changes. Please commit or stash them first."
    Write-Info "Uncommitted changes:"
    git status --short
    exit 1
}

# Get current branch
$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Info "Current branch: $currentBranch"

# Ensure we're on develop branch
if ($currentBranch -ne "develop") {
    Write-Error "Error: You must be on the 'develop' branch to create a release."
    Write-Info "Current branch: $currentBranch"
    Write-Info "Please checkout develop branch: git checkout develop"
    exit 1
}

# Fetch latest changes
Write-Info "Fetching latest changes from remote..."
if (-not $DryRun) {
    git fetch origin
}

# Check if develop is ahead of master
$developCommits = git rev-list --count origin/master..develop 2>$null
if (-not $developCommits -or $developCommits -eq "0") {
    Write-Warning "Warning: develop branch is not ahead of master. No changes to release."
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Info "Release cancelled."
        exit 0
    }
} else {
    Write-Success "Found $developCommits commit(s) to release"
}

# Show what will be released
Write-Info ""
Write-Info "Changes to be released:"
Write-Info "======================="
git log --oneline origin/master..develop

Write-Info ""
Write-Info "The release process will:"
Write-Info "1. Increment patch version in develop"
Write-Info "2. Commit version bump in develop"
Write-Info "3. Push develop branch"
Write-Info "4. Checkout master branch"
Write-Info "5. Merge develop into master"
Write-Info "6. Push master to trigger CI/CD pipeline"
Write-Info "7. Return to develop branch"
Write-Info ""
Write-Info "The CI pipeline will automatically:"
Write-Info "- Run tests"
Write-Info "- Create NuGet packages"
Write-Info "- Create version tag"
Write-Info "- Create GitHub release"
Write-Info ""

if (-not $DryRun) {
    $confirm = Read-Host "Proceed with release? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Info "Release cancelled."
        exit 0
    }
}

Write-Info ""
Write-Info "Starting release process..."

# Increment version in develop branch
Write-Info "Incrementing patch version in develop branch..."
if (-not $DryRun) {
    $scriptPath = Join-Path $PSScriptRoot ".." "scripts" "Increment-Version.ps1"
    $buildPropsPath = Join-Path $PSScriptRoot ".." "Directory.Build.props"

    & $scriptPath -BuildPropsPath $buildPropsPath -IncrementType Patch

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to increment version"
        exit 1
    }

    # Get the new version
    $getVersionScript = Join-Path $PSScriptRoot ".." "scripts" "Get-Version.ps1"
    $newVersion = & $getVersionScript -BuildPropsPath $buildPropsPath -Format Simple

    Write-Success "Version incremented to: $newVersion"
}

# Commit version bump in develop
Write-Info "Committing version bump in develop branch..."
if (-not $DryRun) {
    git add Directory.Build.props
    $result = git commit -m "Bump version to $newVersion" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to commit version bump"
        Write-Error $result
        exit 1
    }
}

# Push develop branch
Write-Info "Pushing develop branch..."
if (-not $DryRun) {
    $result = git push origin develop 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to push develop branch"
        Write-Error $result
        exit 1
    }
}

# Checkout master branch
Write-Info "Switching to master branch..."
if (-not $DryRun) {
    $result = git checkout master 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to checkout master branch"
        Write-Error $result
        exit 1
    }
}

# Pull latest master
Write-Info "Pulling latest master..."
if (-not $DryRun) {
    $result = git pull origin master 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to pull latest master"
        Write-Error $result
        exit 1
    }
}

# Merge develop into master
Write-Info "Merging develop into master..."
if (-not $DryRun) {
    $result = git merge develop --no-ff -m "Release: Merge develop into master" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to merge develop into master"
        Write-Error $result
        Write-Info "You may need to resolve conflicts manually"
        exit 1
    }
}

# Push master
Write-Info "Pushing master to trigger CI/CD pipeline..."
if (-not $DryRun) {
    $result = git push origin master 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to push master"
        Write-Error $result
        exit 1
    }
}

# Return to develop branch
Write-Info "Returning to develop branch..."
if (-not $DryRun) {
    $result = git checkout develop 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to return to develop branch"
        Write-Error $result
        exit 1
    }
}

Write-Info ""
if ($DryRun) {
    Write-Success "Dry run completed successfully!"
    Write-Info "Run without -DryRun flag to perform actual release."
} else {
    Write-Success "Release process completed successfully!"
    Write-Info ""
    Write-Info "Version $newVersion has been committed in develop and merged to master."
    Write-Info ""
    Write-Info "The CI pipeline will automatically:"
    Write-Info "1. Run tests"
    Write-Info "2. Create NuGet packages"
    Write-Info "3. Create version tag v$newVersion"
    Write-Info "4. Publish to NuGet (if configured)"
    Write-Info "5. Create GitHub release"
    Write-Info ""
    Write-Info "Monitor the pipeline at: https://dev.azure.com/your-org/your-project/_build"
}

Write-Info ""
Write-Info "Release script completed."