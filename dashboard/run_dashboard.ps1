[CmdletBinding()]
param([string]$PythonExe, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$venvPython = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $venvPython)) {
    $candidates = @()
    if ($PythonExe) { $candidates += ,@($PythonExe) }
    elseif (Get-Command py -ErrorAction SilentlyContinue) {
        foreach ($version in @('3.12','3.11','3.10','3.9')) { $candidates += ,@('py', "-$version") }
    }
    if (-not $PythonExe -and (Get-Command python -ErrorAction SilentlyContinue)) { $candidates += ,@('python') }
    $selected = $null
    foreach ($candidate in $candidates) {
        $command = $candidate[0]
        $arguments = @($candidate | Select-Object -Skip 1)
        try {
            $probe = & $command @arguments -c "import sys; print(sys.executable if (3,9) <= sys.version_info[:2] <= (3,12) else '')" 2>$null
            if ($LASTEXITCODE -eq 0 -and $probe) { $selected = $probe.Trim(); break }
        } catch { continue }
    }
    if (-not $selected) { throw 'Install Python 3.12 (or 3.9-3.11), then rerun the launcher. The pinned packages do not support Python 3.13+.' }
    & $selected -m venv (Join-Path $PSScriptRoot '.venv')
    if ($LASTEXITCODE -ne 0) { throw 'Could not create dashboard virtual environment.' }
}
& $venvPython -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt') --disable-pip-version-check
if ($LASTEXITCODE -ne 0) { throw 'Dashboard dependency installation failed. Check network access and the pip error above.' }
if ($CheckOnly) {
    & $venvPython -c "import dash, pandas, numpy, plotly, dash_bootstrap_components; print('Dashboard dependencies OK')"
    if ($LASTEXITCODE -ne 0) { throw 'Dashboard import check failed.' }
    exit 0
}
Write-Host 'Dashboard: http://localhost:8050 (Ctrl+C to stop)'
& $venvPython (Join-Path $PSScriptRoot 'app.py')
exit $LASTEXITCODE
