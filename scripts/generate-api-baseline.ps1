# Simple PowerShell script to generate the API baseline
param(
    [string]$ProjectPath = "src/OSDP.Net/OSDP.Net.csproj",
    [string]$BaselineFile = "api-baseline.txt",
    [string]$Configuration = "Release"
)

Write-Host "🔍 Generating OSDP.Net API Baseline..." -ForegroundColor Cyan

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build $ProjectPath --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green

# Find the assembly
$assemblyPath = "src/OSDP.Net/bin/$Configuration/net8.0/OSDP.Net.dll"
if (-not (Test-Path $assemblyPath)) {
    Write-Host "❌ Assembly not found at $assemblyPath" -ForegroundColor Red
    exit 1
}

# Load assembly and extract public API
Write-Host "📋 Extracting public API..." -ForegroundColor Yellow

try {
    Add-Type -Path $assemblyPath
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $assemblyPath))
    
    $api = @()
    $api += "# OSDP.Net Public API Baseline"
    $api += "# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $api += "# Configuration: $Configuration"
    $api += "#"
    $api += ""
    
    $publicTypes = $assembly.GetExportedTypes() | Sort-Object FullName
    
    foreach ($type in $publicTypes) {
        $category = "CLASS"
        if ($type.IsInterface) { $category = "INTERFACE" }
        elseif ($type.IsEnum) { $category = "ENUM" }
        elseif ($type.IsValueType) { $category = "STRUCT" }
        
        $api += "[$category] $($type.FullName)"
        
        # Add constructors
        $constructors = $type.GetConstructors() | Where-Object { $_.IsPublic }
        foreach ($ctor in $constructors) {
            $params = ($ctor.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            $api += "  [CONSTRUCTOR] $($type.FullName)($params)"
        }
        
        # Add methods (non-special)
        $methods = $type.GetMethods() | Where-Object { $_.IsPublic -and -not $_.IsSpecialName -and $_.DeclaringType -eq $type }
        foreach ($method in $methods) {
            $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            $api += "  [METHOD] $($type.FullName).$($method.Name)($params) -> $($method.ReturnType.Name)"
        }
        
        # Add properties
        $properties = $type.GetProperties() | Where-Object { $_.DeclaringType -eq $type }
        foreach ($prop in $properties) {
            $access = @()
            if ($prop.CanRead -and $prop.GetMethod.IsPublic) { $access += "get" }
            if ($prop.CanWrite -and $prop.SetMethod.IsPublic) { $access += "set" }
            $api += "  [PROPERTY] $($type.FullName).$($prop.Name) { $($access -join "; ") } -> $($prop.PropertyType.Name)"
        }
        
        # Add events
        $events = $type.GetEvents() | Where-Object { $_.DeclaringType -eq $type }
        foreach ($event in $events) {
            $api += "  [EVENT] $($type.FullName).$($event.Name) -> $($event.EventHandlerType.Name)"
        }
        
        # Add enum values
        if ($type.IsEnum) {
            $enumValues = [Enum]::GetValues($type)
            foreach ($value in $enumValues) {
                $api += "  [ENUM_VALUE] $($type.FullName).$value = $([int]$value)"
            }
        }
        
        $api += ""
    }
    
    # Save to file
    $api | Out-File -FilePath $BaselineFile -Encoding UTF8
    
    Write-Host "✅ API baseline generated with $($publicTypes.Count) public types" -ForegroundColor Green
    Write-Host "📄 Saved to: $BaselineFile" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Error extracting API: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "🎉 Complete!" -ForegroundColor Green