# PowerShell script to validate public API surface
param(
    [string]$ProjectPath = "src/OSDP.Net/OSDP.Net.csproj"
)

Write-Host "🔍 Validating OSDP.Net Public API..." -ForegroundColor Cyan

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = dotnet build $ProjectPath --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green

# Check for main public classes
$requiredPublicClasses = @(
    "OSDP.Net.ControlPanel",
    "OSDP.Net.Device", 
    "OSDP.Net.DeviceConfiguration",
    "OSDP.Net.Connections.IOsdpConnection",
    "OSDP.Net.Connections.TcpClientOsdpConnection",
    "OSDP.Net.Connections.SerialPortOsdpConnection"
)

# Use reflection to check API
$assemblyPath = "src/OSDP.Net/bin/Debug/net8.0/OSDP.Net.dll"
if (Test-Path $assemblyPath) {
    Write-Host "📋 Checking public API surface..." -ForegroundColor Yellow
    
    Add-Type -Path $assemblyPath
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $assemblyPath))
    
    $publicTypes = $assembly.GetExportedTypes()
    $publicTypeNames = $publicTypes | ForEach-Object { $_.FullName }
    
    Write-Host "Found $($publicTypes.Count) public types" -ForegroundColor Green
    
    $missing = @()
    foreach ($required in $requiredPublicClasses) {
        if ($required -notin $publicTypeNames) {
            $missing += $required
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Host "❌ Missing required public classes:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    } else {
        Write-Host "✅ All required public classes are available" -ForegroundColor Green
    }
    
    # List public types for review
    Write-Host "`n📝 Public API Summary:" -ForegroundColor Cyan
    $publicTypes | Sort-Object FullName | ForEach-Object { 
        Write-Host "  $($_.FullName)" -ForegroundColor White
    }
} else {
    Write-Host "❌ Assembly not found at $assemblyPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n🎉 Public API validation complete!" -ForegroundColor Green