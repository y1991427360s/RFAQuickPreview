param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Desktop\RFAQuickPreview.Desktop.csproj"
$publishDir = Join-Path $repoRoot "dist\RFAQuickPreviewPortable"
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

& $msbuild (Join-Path $repoRoot "RFAQuickPreview.csproj") /t:Restore,Build /p:Configuration=Debug /v:minimal
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publishDir

$helperDir = Join-Path $publishDir "RevitHelper"
New-Item -ItemType Directory -Force -Path $helperDir | Out-Null
Copy-Item -Path (Join-Path $repoRoot "bin\Debug\RFAQuickPreview.dll") -Destination $helperDir -Force

$configPath = Join-Path $publishDir "RFAQuickPreview.config.json"
if (-not (Test-Path $configPath)) {
@"
{
  "RevitExePath": "D:\\Autodesk\\REVIT2020\\Revit 2020\\Revit.exe"
}
"@ | Set-Content -Path $configPath -Encoding UTF8
}

@'
$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "RFAQuickPreview.exe"
$baseKey = "HKCU:\Software\Classes\Directory\shell\RFAQuickPreview"
$commandKey = Join-Path $baseKey "command"
New-Item -Path $commandKey -Force | Out-Null
Set-ItemProperty -Path $baseKey -Name "(default)" -Value "Preview RFA files"
Set-ItemProperty -Path $commandKey -Name "(default)" -Value "`"$exe`" `"%1`""
Write-Host "Registered folder context menu: Preview RFA files"
'@ | Set-Content -Path (Join-Path $publishDir "RegisterRightClick.ps1") -Encoding UTF8

@'
$ErrorActionPreference = "Stop"
$baseKey = "HKCU:\Software\Classes\Directory\shell\RFAQuickPreview"
if (Test-Path $baseKey) {
    Remove-Item -Path $baseKey -Recurse -Force
}
Write-Host "Removed folder context menu: Preview RFA files"
'@ | Set-Content -Path (Join-Path $publishDir "UnregisterRightClick.ps1") -Encoding UTF8

Write-Host "Published desktop app to $publishDir"
Write-Host "EXE: $(Join-Path $publishDir 'RFAQuickPreview.exe')"
