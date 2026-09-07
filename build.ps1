[CmdletBinding()]
param(
    [string]$GameManagedPath = $env:CITIES_SKYLINES_BINARIES,
    [string]$ModDirectory = $env:CITIES_SKYLINES_MOD_DIR,
    [switch]$Test,
    [switch]$Deploy
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools/Resolve-GamePaths.ps1')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 8 SDK, then rerun build.ps1.' }
$managed = Resolve-GameManagedPath -ExplicitPath $GameManagedPath
$managedArgument = $managed.Replace('\', '/') + '/'
if (-not $ModDirectory) {
    $ModDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Colossal Order/Cities_Skylines/Addons/Mods/RealTime'
}
if ($Deploy -and (Get-Process -Name Cities -ErrorAction SilentlyContinue)) { throw 'Close Cities: Skylines before deploying.' }
$cacheDir = Join-Path $PSScriptRoot 'src/obj'
New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
$escaped = [System.Security.SecurityElement]::Escape($managed + [IO.Path]::DirectorySeparatorChar)
$props = '<Project><PropertyGroup><CitiesSkylinesBinaries Condition="''$(CitiesSkylinesBinaries)'' == ''''">' + $escaped + '</CitiesSkylinesBinaries></PropertyGroup></Project>'
[IO.File]::WriteAllText((Join-Path $cacheDir 'Tenus.GamePaths.props'), $props)
$package = Join-Path $PSScriptRoot 'dist/RealTime'
Write-Host "Game libraries: $managed"
& dotnet build (Join-Path $PSScriptRoot 'src/RealTime/RealTime.csproj') -c Release "-p:CitiesSkylinesBinaries=$managedArgument" "-p:RealTimeDeployDir=$package"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
if ($Test) {
    & (Join-Path $PSScriptRoot 'tools/Test-PortablePaths.ps1')
    & dotnet test (Join-Path $PSScriptRoot 'src/RealTimeTests/RealTimeTests.csproj') -c Release "-p:CitiesSkylinesBinaries=$managedArgument" '-p:RealTimeDeployDir='
    if ($LASTEXITCODE -ne 0) { throw 'Mod tests failed.' }
}
if ($Deploy) {
    if (Get-Process -Name Cities -ErrorAction SilentlyContinue) { throw 'Cities started during the build; deployment stopped.' }
    New-Item -ItemType Directory -Path $ModDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $package '*') -Destination $ModDirectory -Recurse -Force
    Write-Host "Installed in: $ModDirectory"
}
Write-Host "Mod package: $package"
