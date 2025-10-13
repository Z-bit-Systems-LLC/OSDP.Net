#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Release script for OSDP.Net project
.DESCRIPTION
    This script automates the release process by incrementing version, creating a tag, and triggering CI/CD pipeline
.PARAMETER IncrementType
    Type of version increment: Patch (default), Minor, or Major
.PARAMETER DryRun
    Perform a dry run without making actual changes
#>

param(
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$IncrementType = "Patch",
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

# Ensure we're on main branch
if ($currentBranch -ne "main") {
    Write-Error "Error: You must be on the 'main' branch to create a release."
    Write-Info "Current branch: $currentBranch"
    Write-Info "Please checkout main branch: git checkout main"
    exit 1
}

# Fetch latest changes
Write-Info "Fetching latest changes from remote..."
if (-not $DryRun) {
    git fetch origin --tags
}

# Pull latest main
Write-Info "Pulling latest changes from main..."
if (-not $DryRun) {
    $result = git pull origin main 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to pull latest main"
        Write-Error $result
        exit 1
    }
}

# Get current version before increment
$getVersionScript = Join-Path $PSScriptRoot ".." "scripts" "Get-Version.ps1"
$buildPropsPath = Join-Path $PSScriptRoot ".." "Directory.Build.props"
$currentVersion = & $getVersionScript -BuildPropsPath $buildPropsPath -Format Simple

Write-Info "Current version: $currentVersion"

# Show recent changes since last tag
$lastTag = git describe --tags --abbrev=0 2>$null
if ($lastTag) {
    Write-Info ""
    Write-Info "Changes since last release ($lastTag):"
    Write-Info "======================================="
    git log --oneline "$lastTag..HEAD"
} else {
    Write-Info ""
    Write-Info "Recent changes:"
    Write-Info "==============="
    git log --oneline -10
}

Write-Info ""
Write-Info "The release process will:"
Write-Info "1. Increment $IncrementType version on main branch"
Write-Info "2. Commit version bump"
Write-Info "3. Create version tag"
Write-Info "4. Push main branch and tag"
Write-Info ""
Write-Info "The CI pipeline will automatically:"
Write-Info "- Run tests"
Write-Info "- Build NuGet packages"
Write-Info "- Build console applications for multiple platforms"
Write-Info "- Publish artifacts"
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

# Increment version on main branch
Write-Info "Incrementing $IncrementType version on main branch..."
if (-not $DryRun) {
    $scriptPath = Join-Path $PSScriptRoot ".." "scripts" "Increment-Version.ps1"

    & $scriptPath -BuildPropsPath $buildPropsPath -IncrementType $IncrementType

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to increment version"
        exit 1
    }

    # Get the new version
    $newVersion = & $getVersionScript -BuildPropsPath $buildPropsPath -Format Simple

    Write-Success "Version incremented to: $newVersion"
}

# Commit version bump
Write-Info "Committing version bump..."
if (-not $DryRun) {
    git add Directory.Build.props
    $result = git commit -m "Bump version to $newVersion" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to commit version bump"
        Write-Error $result
        exit 1
    }
}

# Create version tag
Write-Info "Creating version tag v$newVersion..."
if (-not $DryRun) {
    $result = git tag -a "v$newVersion" -m "Release version $newVersion" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to create tag"
        Write-Error $result
        exit 1
    }
}

# Push main branch
Write-Info "Pushing main branch..."
if (-not $DryRun) {
    $result = git push origin main 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to push main branch"
        Write-Error $result
        exit 1
    }
}

# Push tag to trigger CI/CD pipeline
Write-Info "Pushing tag to trigger CI/CD pipeline..."
if (-not $DryRun) {
    $result = git push origin "v$newVersion" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to push tag"
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
    Write-Info "Version $newVersion has been committed and tagged on main branch."
    Write-Info ""
    Write-Info "The CI pipeline will automatically:"
    Write-Info "1. Run tests"
    Write-Info "2. Build NuGet packages"
    Write-Info "3. Build console applications for multiple platforms"
    Write-Info "4. Publish artifacts"
    Write-Info "5. Publish to NuGet (if configured)"
    Write-Info ""
    Write-Info "Monitor the pipeline at: https://dev.azure.com/your-org/your-project/_build"
}

Write-Info ""
Write-Info "Release script completed."