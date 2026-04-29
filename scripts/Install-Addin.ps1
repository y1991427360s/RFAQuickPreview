param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoRoot "bin\$Configuration"
$addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
$pluginDir = Join-Path $addinRoot "RFAQuickPreviewAutomation"

if (-not (Test-Path $sourceDir)) {
    throw "Build output was not found: $sourceDir"
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

Copy-Item -Path (Join-Path $sourceDir "RFAQuickPreview.dll") -Destination $pluginDir -Force

$assemblyPath = Join-Path $pluginDir "RFAQuickPreview.dll"
$addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>RFAQuickPreviewAutomation</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>1A7CBA6E-0553-4CBB-9D5F-206D36E7E20D</AddInId>
    <FullClassName>RFAQuickPreview.App.AutomationApplication</FullClassName>
    <VendorId>RFQP</VendorId>
    <VendorDescription>RFAQuickPreview</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

Set-Content -Path (Join-Path $addinRoot "RFAQuickPreview.Automation.addin") -Value $addinContent -Encoding UTF8

Write-Host "Installed RFAQuickPreview to $addinRoot"
Write-Host "Assembly path: $assemblyPath"
