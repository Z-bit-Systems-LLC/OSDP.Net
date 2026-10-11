#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generates and publishes Doxygen HTML documentation for OSDP.Net
.DESCRIPTION
    This script generates HTML documentation using Doxygen, checks the links from the Markdown docs into it,
    and optionally publishes it to a specified output directory.

    The version comes from Directory.Build.props and is passed to Doxygen on stdin, so the Doxyfile is never
    modified. The CI docs job (ci/docs.yml) builds the published API reference the same way.
.PARAMETER OutputPath
    The path where generated documentation will be published (default: ./docs, HTML output in ./docs/html)
.PARAMETER DoxygenPath
    Path to the Doxygen executable (default: assumes 'doxygen' is in PATH)
.PARAMETER DoxyfilePath
    Path to the Doxyfile configuration (default: Doxyfile in repository root)
.PARAMETER SkipGeneration
    If specified, skips the documentation generation and only publishes existing docs
.PARAMETER PublishPath
    If specified, copies the generated documentation to this path (e.g., for deployment)
.EXAMPLE
    ./Publish-Documentation.ps1
    Generates documentation using default settings
.EXAMPLE
    ./Publish-Documentation.ps1 -PublishPath "\\server\share\docs"
    Generates documentation and copies it to a network share
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "docs",

    [Parameter(Mandatory = $false)]
    [string]$DoxygenPath = "doxygen",

    [Parameter(Mandatory = $false)]
    [string]$DoxyfilePath,

    [Parameter(Mandatory = $false)]
    [switch]$SkipGeneration,

    [Parameter(Mandatory = $false)]
    [string]$PublishPath
)

# Set default paths if not provided
if (-not $DoxyfilePath) {
    if ($PSScriptRoot) {
        $DoxyfilePath = Join-Path $PSScriptRoot ".." "Doxyfile"
    } else {
        $DoxyfilePath = Join-Path (Get-Location) "Doxyfile"
    }
}

$RepoRoot = if ($PSScriptRoot) { Join-Path $PSScriptRoot ".." } else { Get-Location }
$BuildPropsPath = Join-Path $RepoRoot "Directory.Build.props"

function Test-DoxygenInstalled {
    param([string]$DoxygenExe)

    try {
        $version = & $DoxygenExe --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Found Doxygen version: $version" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Error "Doxygen not found. Please install Doxygen from https://www.doxygen.nl/download.html"
        Write-Error "Or install via Chocolatey: choco install doxygen.install"
        return $false
    }

    return $false
}

function Get-ProjectVersion {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        Write-Warning "Directory.Build.props not found, using default version"
        return "1.0.0"
    }

    $content = Get-Content $FilePath -Raw
    $versionMatch = [regex]::Match($content, '<VersionPrefix>([^<]+)</VersionPrefix>')
    $suffixMatch = [regex]::Match($content, '<VersionSuffix>([^<]+)</VersionSuffix>')

    if ($versionMatch.Success) {
        $version = $versionMatch.Groups[1].Value
        if ($suffixMatch.Success) {
            $version += "-" + $suffixMatch.Groups[1].Value
        }
        return $version
    }

    return "1.0.0"
}

function Invoke-DoxygenGeneration {
    param(
        [string]$DoxyfilePath,
        [string]$DoxygenExe,
        [string]$ProjectVersion
    )

    Write-Host "Generating documentation..." -ForegroundColor Green
    Write-Host "Using Doxyfile: $DoxyfilePath" -ForegroundColor Cyan

    if (-not (Test-Path $DoxyfilePath)) {
        throw "Doxyfile not found at: $DoxyfilePath"
    }

    # Settings later in the stream override the Doxyfile, so the version is supplied without editing it
    $config = (Get-Content $DoxyfilePath -Raw) + "`nPROJECT_NUMBER=$ProjectVersion`nOUTPUT_DIRECTORY=$OutputPath`n"

    Push-Location $RepoRoot
    try {
        $config | & $DoxygenExe -

        if ($LASTEXITCODE -ne 0) {
            throw "Doxygen generation failed with exit code $LASTEXITCODE"
        }

        Write-Host "Documentation generated successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

function Publish-GeneratedDocs {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    $fullSourcePath = Join-Path $RepoRoot $SourcePath

    if (-not (Test-Path $fullSourcePath)) {
        throw "Generated documentation not found at: $fullSourcePath"
    }

    Write-Host "Publishing documentation from $fullSourcePath to $DestinationPath" -ForegroundColor Green

    # Create destination directory if it doesn't exist
    if (-not (Test-Path $DestinationPath)) {
        New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
        Write-Host "Created destination directory: $DestinationPath" -ForegroundColor Cyan
    }

    # Copy files
    Copy-Item -Path "$fullSourcePath\*" -Destination $DestinationPath -Recurse -Force

    Write-Host "Documentation published successfully to: $DestinationPath" -ForegroundColor Green
}

# Main execution
try {
    Write-Host "=== OSDP.Net Documentation Generator ===" -ForegroundColor Cyan
    Write-Host ""

    # Check if Doxygen is installed
    if (-not (Test-DoxygenInstalled -DoxygenExe $DoxygenPath)) {
        exit 1
    }

    # Get project version
    $projectVersion = Get-ProjectVersion -FilePath $BuildPropsPath
    Write-Host "Project version: $projectVersion" -ForegroundColor Cyan
    Write-Host ""

    # Generate documentation
    if (-not $SkipGeneration) {
        Invoke-DoxygenGeneration -DoxyfilePath $DoxyfilePath -DoxygenExe $DoxygenPath -ProjectVersion $projectVersion
        Write-Host ""
    }

    # Check the links from the Markdown docs into the generated reference
    $htmlSourcePath = Join-Path $RepoRoot $OutputPath "html"
    & (Join-Path $PSScriptRoot "Test-DocLinks.ps1") -HtmlPath $htmlSourcePath
    if ($LASTEXITCODE -ne 0) {
        throw "Broken links into the API reference; fix them before releasing"
    }
    Write-Host ""

    # Publish to specified path if provided
    if ($PublishPath) {
        Publish-GeneratedDocs -SourcePath (Join-Path $OutputPath "html") -DestinationPath $PublishPath
        Write-Host ""
    }

    $htmlPath = Join-Path $htmlSourcePath "index.html"
    if (Test-Path $htmlPath) {
        Write-Host "Documentation available at: $htmlPath" -ForegroundColor Green
        Write-Host "You can open it in your browser to view the generated documentation." -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "Done!" -ForegroundColor Green
}
catch {
    Write-Error "Error generating documentation: $($_.Exception.Message)"
    exit 1
}
