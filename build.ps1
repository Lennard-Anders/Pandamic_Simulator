# RealTime Mod Build Script
# Setzt Umgebungsvariablen und baut das Projekt

$env:CITIES_SKYLINES_BINARIES = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"
$env:CITIES_SKYLINES_MOD_DIR = "C:\Users\mail\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime"

Write-Host "🔨 Building RealTime mod..." -ForegroundColor Cyan
cd (Split-Path -Parent $MyInvocation.MyCommand.Path)

dotnet clean src/RealTime.sln -c Release
dotnet build src/RealTime.sln -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build erfolgreich!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Release-Dateien:" -ForegroundColor Green
    Get-ChildItem "src\bin\Release\" -Filter "*.dll" | ForEach-Object { 
        Write-Host "  ✓ $($_.Name)" 
    }
    Write-Host ""
    Write-Host "🎮 Deployed ins Mods-Verzeichnis:" -ForegroundColor Green
    Get-ChildItem "$env:CITIES_SKYLINES_MOD_DIR" -Filter "*.dll" | ForEach-Object { 
        Write-Host "  ✓ $($_.Name)" 
    }
} else {
    Write-Host "❌ Build fehlgeschlagen!" -ForegroundColor Red
}
