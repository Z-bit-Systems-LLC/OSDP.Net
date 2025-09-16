#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Increments the version in Directory.Build.props
.DESCRIPTION
    This script increments the version number in Directory.Build.props based on the specified increment type.
.PARAMETER IncrementType
    The type of version increment: Major, Minor, or Patch (default)
.PARAMETER BuildPropsPath
    Path to Directory.Build.props file (default: Directory.Build.props in repository root)
.EXAMPLE
    ./Increment-Version.ps1 -IncrementType Patch
.EXAMPLE
    ./Increment-Version.ps1 -IncrementType Minor
.EXAMPLE
    ./Increment-Version.ps1 -IncrementType Major
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Major", "Minor", "Patch")]
    [string]$IncrementType = "Patch",

    [Parameter(Mandatory = $false)]
    [string]$BuildPropsPath
)

# Set default path if not provided
if (-not $BuildPropsPath) {
    $BuildPropsPath = Join-Path $PSScriptRoot ".." "Directory.Build.props"
}

function Get-CurrentVersion {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        throw "Directory.Build.props not found at: $FilePath"
    }
    
    $content = Get-Content $FilePath -Raw
    $versionMatch = [regex]::Match($content, '<VersionPrefix>(\d+)\.(\d+)\.(\d+)</VersionPrefix>')
    
    if (-not $versionMatch.Success) {
        throw "Could not find VersionPrefix in Directory.Build.props"
    }
    
    return @{
        Major = [int]$versionMatch.Groups[1].Value
        Minor = [int]$versionMatch.Groups[2].Value
        Patch = [int]$versionMatch.Groups[3].Value
        FullMatch = $versionMatch.Groups[0].Value
    }
}

function Set-NewVersion {
    param(
        [string]$FilePath,
        [hashtable]$CurrentVersion,
        [string]$IncrementType
    )
    
    $newMajor = $CurrentVersion.Major
    $newMinor = $CurrentVersion.Minor
    $newPatch = $CurrentVersion.Patch
    
    switch ($IncrementType) {
        "Major" {
            $newMajor++
            $newMinor = 0
            $newPatch = 0
        }
        "Minor" {
            $newMinor++
            $newPatch = 0
        }
        "Patch" {
            $newPatch++
        }
    }
    
    $newVersionString = "$newMajor.$newMinor.$newPatch"
    $newVersionElement = "<VersionPrefix>$newVersionString</VersionPrefix>"
    
    $content = Get-Content $FilePath -Raw
    $newContent = $content -replace [regex]::Escape($CurrentVersion.FullMatch), $newVersionElement
    
    Set-Content $FilePath -Value $newContent -NoNewline
    
    return $newVersionString
}

try {
    Write-Host "Incrementing version ($IncrementType)..." -ForegroundColor Green
    
    $currentVersion = Get-CurrentVersion -FilePath $BuildPropsPath
    $currentVersionString = "$($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Patch)"
    
    Write-Host "Current version: $currentVersionString" -ForegroundColor Yellow
    
    $newVersionString = Set-NewVersion -FilePath $BuildPropsPath -CurrentVersion $currentVersion -IncrementType $IncrementType
    
    Write-Host "New version: $newVersionString" -ForegroundColor Green
    Write-Host "Version updated successfully!" -ForegroundColor Green
}
catch {
    Write-Error "Error updating version: $($_.Exception.Message)"
    exit 1
}