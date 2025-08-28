# PowerShell script to check API changes between versions
param(
    [string]$BaseVersion = "main",
    [string]$CurrentBranch = "HEAD"
)

Write-Host "🔍 Checking API differences between $BaseVersion and $CurrentBranch" -ForegroundColor Cyan

function Get-PublicAPI {
    param([string]$commit)
    
    # Checkout commit
    git checkout $commit --quiet
    
    # Build project
    dotnet build src/OSDP.Net/OSDP.Net.csproj --verbosity quiet --configuration Release
    
    # Extract public API using reflection
    $assemblyPath = "src/OSDP.Net/bin/Release/net8.0/OSDP.Net.dll"
    if (-not (Test-Path $assemblyPath)) {
        Write-Host "❌ Assembly not found for commit $commit" -ForegroundColor Red
        return @()
    }
    
    Add-Type -Path $assemblyPath
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $assemblyPath))
    
    $api = @()
    foreach ($type in $assembly.GetExportedTypes()) {
        $api += "$($type.FullName)"
        
        # Add public members
        foreach ($member in $type.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static)) {
            if ($member.DeclaringType -eq $type) {
                $api += "  $($type.FullName).$($member.Name)"
            }
        }
    }
    
    return $api | Sort-Object
}

# Save current branch
$currentBranch = git rev-parse --abbrev-ref HEAD

try {
    Write-Host "Analyzing base version ($BaseVersion)..." -ForegroundColor Yellow
    $baseAPI = Get-PublicAPI $BaseVersion
    
    Write-Host "Analyzing current version..." -ForegroundColor Yellow
    git checkout $currentBranch --quiet
    $currentAPI = Get-PublicAPI $CurrentBranch
    
    # Compare APIs
    $added = $currentAPI | Where-Object { $_ -notin $baseAPI }
    $removed = $baseAPI | Where-Object { $_ -notin $currentAPI }
    
    Write-Host "`n📈 API Changes Summary:" -ForegroundColor Cyan
    
    if ($added.Count -gt 0) {
        Write-Host "✅ Added ($($added.Count) items):" -ForegroundColor Green
        $added | ForEach-Object { Write-Host "  + $_" -ForegroundColor Green }
    }
    
    if ($removed.Count -gt 0) {
        Write-Host "❌ Removed ($($removed.Count) items):" -ForegroundColor Red
        $removed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        Write-Host "`n⚠️  WARNING: Breaking changes detected!" -ForegroundColor Yellow
    }
    
    if ($added.Count -eq 0 -and $removed.Count -eq 0) {
        Write-Host "✅ No API changes detected" -ForegroundColor Green
    }
    
} finally {
    # Restore current branch
    git checkout $currentBranch --quiet
}

Write-Host "`n🎉 API diff check complete!" -ForegroundColor Green