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
    
    return $versionMatch.Groups[1].Value
}

try {
    $version = Get-Version -FilePath $BuildPropsPath
    
    if ($Format -eq "Simple") {
        Write-Output $version
    } else {
        Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
        Write-Host $version -ForegroundColor Green
    }
}
catch {
    Write-Error "Error reading version: $($_.Exception.Message)"
    exit 1
}