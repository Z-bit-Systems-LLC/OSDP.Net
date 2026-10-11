#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Checks the links from the Markdown docs into the published API reference
.DESCRIPTION
    Finds every https://z-bitco.com/osdp-net/api/<file>.html#<anchor> link in the Markdown docs and checks
    it against a local Doxygen build: the page must exist and, when the link has an anchor, the page must
    contain an element with that id. Doxygen member anchors are hashes of the signature, so they change
    when an overload's parameters change.

    Links that still use the retired https://z-bitco.com/downloads/OSDP.Net/docs/ path are reported as broken.

    Exits 1 if any link is broken or the HTML is missing.
.PARAMETER HtmlPath
    The generated Doxygen HTML folder (default: docs/html in the repository root)
.PARAMETER Path
    The Markdown files to check (default: docs/*.md and README.md in the repository root)
.EXAMPLE
    ./Test-DocLinks.ps1
    Checks the docs against docs/html after a local Publish-Documentation.ps1 run
.EXAMPLE
    ./Test-DocLinks.ps1 -HtmlPath $(Build.ArtifactStagingDirectory)/api-docs/html
    Checks the docs against the HTML built in CI
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$HtmlPath,

    [Parameter(Mandatory = $false)]
    [string[]]$Path
)

$RepoRoot = Join-Path $PSScriptRoot ".."

if (-not $HtmlPath) {
    $HtmlPath = Join-Path $RepoRoot "docs" "html"
}

if (-not $Path) {
    $Path = @(
        (Get-ChildItem (Join-Path $RepoRoot "docs") -Filter *.md).FullName
        (Join-Path $RepoRoot "README.md")
    )
}

if (-not (Test-Path (Join-Path $HtmlPath "index.html"))) {
    Write-Host "No index.html in $HtmlPath. Generate the docs first with scripts/Publish-Documentation.ps1." -ForegroundColor Red
    exit 1
}

$apiLink = [regex]'https://z-bitco\.com/osdp-net/api/(?<file>[^)\s#"''>]+)(?:#(?<anchor>[^)\s"''>]+))?'
$retiredLink = [regex]'https://z-bitco\.com/downloads/OSDP\.Net/docs/[^)\s"''>]*'

$pageCache = @{}
$checked = 0
$broken = [System.Collections.Generic.List[string]]::new()

foreach ($file in $Path) {
    $relativeFile = [System.IO.Path]::GetRelativePath($RepoRoot, $file)
    $lineNumber = 0

    foreach ($line in Get-Content $file) {
        $lineNumber++

        foreach ($match in $retiredLink.Matches($line)) {
            $broken.Add("${relativeFile}:${lineNumber}: $($match.Value) uses the retired path; use https://z-bitco.com/osdp-net/api/")
        }

        foreach ($match in $apiLink.Matches($line)) {
            $checked++
            $page = $match.Groups['file'].Value
            $anchor = $match.Groups['anchor'].Value
            $pagePath = Join-Path $HtmlPath $page

            if (-not $pageCache.ContainsKey($page)) {
                $pageCache[$page] = if (Test-Path $pagePath -PathType Leaf) { Get-Content $pagePath -Raw } else { $null }
            }

            if ($null -eq $pageCache[$page]) {
                $broken.Add("${relativeFile}:${lineNumber}: $($match.Value) - page $page does not exist")
            }
            elseif ($anchor -and -not $pageCache[$page].Contains("id=`"$anchor`"")) {
                $broken.Add("${relativeFile}:${lineNumber}: $($match.Value) - no anchor $anchor in $page")
            }
        }
    }
}

if ($broken.Count -gt 0) {
    Write-Host "$($broken.Count) broken API doc link(s):" -ForegroundColor Red
    $broken | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All $checked API doc links resolve against $HtmlPath" -ForegroundColor Green
exit 0
