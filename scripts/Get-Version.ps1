#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Gets the current version from Directory.Build.props
.DESCRIPTION
    This script reads and displays the current version from Directory.Build.props.
.PARAMETER BuildPropsPath
    Path to Directory.Build.props file (default: Directory.Build.props in repository root)
.PARAMETER Format
    Output format: Simple (version only) or Detailed (with labels)
.EXAMPLE
    ./Get-Version.ps1
.EXAMPLE
    ./Get-Version.ps1 -Format Simple
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$BuildPropsPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Simple", "Detailed")]
    [string]$Format = "Detailed"
)

# Set default path if not provided
if (-not $BuildPropsPath) {
    if ($PSScriptRoot) {
        $BuildPropsPath = Join-Path $PSScriptRoot ".." "Directory.Build.props"
    } else {
        # Fallback for cases where PSScriptRoot is not available
        $BuildPropsPath = Join-Path (Get-Location) "Directory.Build.props"
    }
}

function Get-Version {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        throw "Directory.Build.props not found at: $FilePath"
    }

    $content = Get-Content $FilePath -Raw
    $versionMatch = [regex]::Match($content, '<VersionPrefix>([^<]+)</VersionPrefix>')

    if (-not $versionMatch.Success) {
        throw "Could not find VersionPrefix in Directory.Build.props"
    }

    $versionPrefix = $versionMatch.Groups[1].Value

    # Also extract VersionSuffix if present
    $suffixMatch = [regex]::Match($content, '<VersionSuffix>([^<]+)</VersionSuffix>')
    $versionSuffix = if ($suffixMatch.Success) { $suffixMatch.Groups[1].Value } else { "" }

    return @{
        VersionPrefix = $versionPrefix
        VersionSuffix = $versionSuffix
        FullVersion = if ($versionSuffix) { "$versionPrefix-$versionSuffix" } else { $versionPrefix }
    }
}

try {
    $versionInfo = Get-Version -FilePath $BuildPropsPath

    if ($Format -eq "Simple") {
        Write-Output $versionInfo.FullVersion
    } else {
        Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
        Write-Host $versionInfo.VersionPrefix -ForegroundColor Green
        if ($versionInfo.VersionSuffix) {
            Write-Host "Version suffix: " -NoNewline -ForegroundColor Yellow
            Write-Host $versionInfo.VersionSuffix -ForegroundColor Green
            Write-Host "Full version: " -NoNewline -ForegroundColor Yellow
            Write-Host $versionInfo.FullVersion -ForegroundColor Green
        }
    }
}
catch {
    Write-Error "Error reading version: $($_.Exception.Message)"
    exit 1
}