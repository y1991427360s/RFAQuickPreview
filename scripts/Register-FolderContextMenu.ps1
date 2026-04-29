param(
    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $ExePath = Join-Path $repoRoot "dist\RFAQuickPreviewPortable\RFAQuickPreview.exe"
}

if (-not (Test-Path $ExePath)) {
    throw "EXE was not found: $ExePath"
}

$baseKey = "HKCU:\Software\Classes\Directory\shell\RFAQuickPreview"
$commandKey = Join-Path $baseKey "command"

New-Item -Path $commandKey -Force | Out-Null
Set-ItemProperty -Path $baseKey -Name "(default)" -Value "Preview RFA files"
Set-ItemProperty -Path $commandKey -Name "(default)" -Value "`"$ExePath`" `"%1`""

Write-Host "Registered folder context menu: Preview RFA files"
