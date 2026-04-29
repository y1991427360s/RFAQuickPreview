$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "RFAQuickPreview.exe"
$baseKey = "HKCU:\Software\Classes\Directory\shell\RFAQuickPreview"
$commandKey = Join-Path $baseKey "command"
New-Item -Path $commandKey -Force | Out-Null
Set-ItemProperty -Path $baseKey -Name "(default)" -Value "Preview RFA files"
Set-ItemProperty -Path $commandKey -Name "(default)" -Value "`"$exe`" `"%1`""
Write-Host "Registered folder context menu: Preview RFA files"
