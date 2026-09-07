function Resolve-GameManagedPath {
    [CmdletBinding()]
    param([string]$ExplicitPath, [string[]]$SteamRoots)
    $required = @('Assembly-CSharp.dll', 'ColossalManaged.dll', 'ICities.dll', 'UnityEngine.dll', 'UnityEngine.UI.dll')
    function Test-ManagedFolder([string]$Folder) {
        foreach ($file in $required) {
            if (-not (Test-Path -LiteralPath (Join-Path $Folder $file) -PathType Leaf)) { return $false }
        }
        return $true
    }
    if ($ExplicitPath) {
        if (-not (Test-ManagedFolder $ExplicitPath)) { throw 'The supplied Managed directory does not contain all five required Cities: Skylines assemblies.' }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path.TrimEnd('\', '/')
    }
    if (-not $SteamRoots) {
        $SteamRoots = @()
        foreach ($entry in @(
            @('HKCU:\Software\Valve\Steam', 'SteamPath'),
            @('HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath'),
            @('HKLM:\SOFTWARE\Valve\Steam', 'InstallPath'))) {
            $value = Get-ItemProperty -LiteralPath $entry[0] -ErrorAction SilentlyContinue
            if ($value -and $value.($entry[1])) { $SteamRoots += $value.($entry[1]) }
        }
        foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
            if ($base) { $SteamRoots += Join-Path $base 'Steam' }
        }
    }
    $libraries = @($SteamRoots)
    foreach ($root in $SteamRoots) {
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdf)) { continue }
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"(?:path|[0-9]+)"\s+"([^"\r\n]+)"')) {
            $candidate = $match.Groups[1].Value.Replace('\\', '\')
            if ([IO.Path]::IsPathRooted($candidate)) { $libraries += $candidate }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $gameFolder = 'Cities_Skylines'
        $manifest = Join-Path $library 'steamapps\appmanifest_255710.acf'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s+"([^"\r\n]+)"')
            if ($match.Success) { $gameFolder = $match.Groups[1].Value }
        }
        $folder = Join-Path $library "steamapps\common\$gameFolder\Cities_Data\Managed"
        if (Test-ManagedFolder $folder) { return (Resolve-Path -LiteralPath $folder).Path.TrimEnd('\', '/') }
    }
    throw 'Cities: Skylines (1) was not found. Install it in Steam, or use build.ps1 -GameManagedPath <Cities_Data\Managed> for a custom installation.'
}
