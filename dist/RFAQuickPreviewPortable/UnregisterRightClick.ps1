$ErrorActionPreference = "Stop"
$baseKey = "HKCU:\Software\Classes\Directory\shell\RFAQuickPreview"
if (Test-Path $baseKey) {
    Remove-Item -Path $baseKey -Recurse -Force
}
Write-Host "Removed folder context menu: Preview RFA files"
