# Pandemic Simulator Dashboard - Launcher
# Double-click this file or run it in a PowerShell terminal.

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

Write-Host ""
Write-Host "  Pandemic Simulator - Analytics Dashboard" -ForegroundColor Cyan
Write-Host "  -----------------------------------------" -ForegroundColor DarkGray

# Check Python
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-Host "  ERROR: Python not found. Install Python 3.9+ from python.org" -ForegroundColor Red
    pause; exit 1
}

# Install / update dependencies silently
Write-Host "  Checking dependencies..." -ForegroundColor DarkGray
python -m pip install -r requirements.txt -q --disable-pip-version-check

Write-Host ""
Write-Host "  Starting dashboard at http://localhost:8050" -ForegroundColor Green
Write-Host "  Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

python app.py
