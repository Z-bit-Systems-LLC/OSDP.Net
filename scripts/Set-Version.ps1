#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets a specific version in Directory.Build.props
.DESCRIPTION
    This script sets the version number in Directory.Build.props to a specific value.
.PARAMETER Version
    The version to set (e.g., "5.1.0")
.PARAMETER BuildPropsPath
    Path to Directory.Build.props file (default: Directory.Build.props in repository root)
.EXAMPLE
    ./Set-Version.ps1 -Version "5.1.0"
.EXAMPLE
    ./Set-Version.ps1 -Version "6.0.0-beta"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    
    [Parameter(Mandatory = $false)]
    [string]$BuildPropsPath = (Join-Path $PSScriptRoot ".." "Directory.Build.props")
)

function Validate-Version {
    param([string]$Version)
    
    # Allow semantic version format (Major.Minor.Patch with optional pre-release)
    $versionPattern = '^(\d+)\.(\d+)\.(\d+)(?:-([a-zA-Z0-9\-\.]+))?$'
    
    if (-not ($Version -match $versionPattern)) {
        throw "Invalid version format. Expected format: Major.Minor.Patch (e.g., '5.1.0' or '5.1.0-beta')"
    }
    
    return $true
}

function Set-Version {
    param(
        [string]$FilePath,
        [string]$NewVersion
    )
    
    if (-not (Test-Path $FilePath)) {
        throw "Directory.Build.props not found at: $FilePath"
    }
    
    $content = Get-Content $FilePath -Raw
    $versionMatch = [regex]::Match($content, '<VersionPrefix>[^<]+</VersionPrefix>')
    
    if (-not $versionMatch.Success) {
        throw "Could not find VersionPrefix in Directory.Build.props"
    }
    
    $currentVersionElement = $versionMatch.Groups[0].Value
    $newVersionElement = "<VersionPrefix>$NewVersion</VersionPrefix>"
    
    $newContent = $content -replace [regex]::Escape($currentVersionElement), $newVersionElement
    
    Set-Content $FilePath -Value $newContent -NoNewline
}

function Get-CurrentVersion {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $versionMatch = [regex]::Match($content, '<VersionPrefix>([^<]+)</VersionPrefix>')
    
    if ($versionMatch.Success) {
        return $versionMatch.Groups[1].Value
    }
    
    return "Unknown"
}

try {
    Write-Host "Setting version to: $Version" -ForegroundColor Green
    
    Validate-Version -Version $Version
    
    $currentVersion = Get-CurrentVersion -FilePath $BuildPropsPath
    Write-Host "Current version: $currentVersion" -ForegroundColor Yellow
    
    Set-Version -FilePath $BuildPropsPath -NewVersion $Version
    
    Write-Host "Version updated successfully to: $Version" -ForegroundColor Green
}
catch {
    Write-Error "Error setting version: $($_.Exception.Message)"
    exit 1
}