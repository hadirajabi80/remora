# Remora installer — copies the app, adds the servers, makes shortcuts, and launches it.
# Run by double-clicking install-remora.bat (no admin needed).

$ErrorActionPreference = 'Stop'
$src  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Remora'

Write-Host "Installing Remora to $dest ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $src 'Remora.exe') (Join-Path $dest 'Remora.exe') -Force

$exe = Join-Path $dest 'Remora.exe'

# Add the servers if a list is present next to this script (creates a fresh encrypted vault if needed).
$list = Join-Path $src 'servers.txt'
if (Test-Path $list) {
    Copy-Item $list (Join-Path $dest 'servers.txt') -Force
    Write-Host "Adding servers from servers.txt ..." -ForegroundColor Cyan
    Start-Process -FilePath $exe -ArgumentList '--import', (Join-Path $dest 'servers.txt') -Wait
} else {
    Write-Host "No servers.txt found — skipping server import (add them later with Import list)." -ForegroundColor Yellow
}

# Optional: if a bastion.txt sits next to this script with the bastion address on the first line, pre-fill it.
$hostFile = Join-Path $src 'bastion.txt'
if (Test-Path $hostFile) {
    $bhost = (Get-Content $hostFile | Select-Object -First 1).Trim()
    if ($bhost) { Start-Process -FilePath $exe -ArgumentList '--set-host', $bhost -Wait }
}

# Desktop + Start Menu shortcuts.
$ws = New-Object -ComObject WScript.Shell
$targets = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Remora.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Remora.lnk')
)
foreach ($lnkPath in $targets) {
    $lnk = $ws.CreateShortcut($lnkPath)
    $lnk.TargetPath       = $exe
    $lnk.WorkingDirectory = $dest
    $lnk.IconLocation     = $exe
    $lnk.Description       = 'Remora - Remote Desktop'
    $lnk.Save()
}

Write-Host ""
Write-Host "Done. Remora is installed and the servers were added." -ForegroundColor Green
Write-Host "Next, inside Remora open Settings and enter YOUR bastion login + password," -ForegroundColor Yellow
Write-Host "then add your machine login under the Credentials button." -ForegroundColor Yellow
Write-Host ""
Start-Process -FilePath $exe
