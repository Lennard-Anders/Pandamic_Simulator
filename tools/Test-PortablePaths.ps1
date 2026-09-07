$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Resolve-GamePaths.ps1')
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = Join-Path $tempRoot ('tenus-path-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $steam = Join-Path $fixture 'Steam Client'
    $library = Join-Path $fixture 'Second Library'
    $managed = Join-Path $library 'steamapps/common/Custom City Folder/Cities_Data/Managed'
    New-Item -ItemType Directory -Path (Join-Path $steam 'steamapps'), $managed -Force | Out-Null
    foreach ($name in @('Assembly-CSharp.dll','ColossalManaged.dll','ICities.dll','UnityEngine.dll','UnityEngine.UI.dll')) {
        [IO.File]::WriteAllBytes((Join-Path $managed $name), [byte[]]@())
    }
    [IO.File]::WriteAllText((Join-Path $library 'steamapps/appmanifest_255710.acf'), '"AppState" { "installdir" "Custom City Folder" }')
    $escaped = $library.Replace('\', '\\')
    $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
    [IO.File]::WriteAllText($vdf, ('"libraryfolders" { "1" { "path" "' + $escaped + '" } }'))
    if ((Resolve-GameManagedPath -SteamRoots @($steam)) -ne $managed) { throw 'Modern additional Steam library detection failed.' }
    [IO.File]::WriteAllText($vdf, ('"LibraryFolders" { "1" "' + $escaped + '" }'))
    if ((Resolve-GameManagedPath -SteamRoots @($steam)) -ne $managed) { throw 'Legacy library detection failed.' }
    if ((Resolve-GameManagedPath -ExplicitPath $managed) -ne $managed) { throw 'Explicit path resolution failed.' }
    $rejected = $false
    try { Resolve-GameManagedPath -ExplicitPath $steam | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Incomplete game installation was accepted.' }
    Write-Host 'Portable paths: 4 checks passed.'
} finally {
    $resolved = (Resolve-Path -LiteralPath $fixture).Path
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing cleanup outside temporary root.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
