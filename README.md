# RFAQuickPreview

RFAQuickPreview is a standalone WPF `.exe` for quickly previewing `.rfa` family files from a folder. The desktop app is the user-facing entry point.

For real geometry thumbnails, the app uses a small Revit 2020 automation helper in the background. The first scan of uncached files starts Revit automatically, exports PNG thumbnails, closes Revit, then the desktop app displays the cached previews. Later scans use the cache directly.

## Desktop Build

Publish the desktop app:

`.\scripts\Publish-Desktop.ps1 -Configuration Release`

Run:

`.\dist\RFAQuickPreview\RFAQuickPreview.exe`

Register the folder right-click menu:

`.\scripts\Register-FolderContextMenu.ps1`

## Revit Automation Helper

The helper is installed as a Revit application add-in so the desktop app can ask Revit to generate real family previews without the user clicking a Revit command.

Build and install the helper:

`& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" RFAQuickPreview.csproj /t:Restore,Build /p:Configuration=Debug`

`.\scripts\Install-Addin.ps1 -Configuration Debug`

Current Revit path used by the project:

`D:\Autodesk\REVIT2020\Revit 2020`

## Structure

- `App`: Revit external command entry point.
- `UI`: WPF window, search, card grid, details and logs.
- `Revit`: Revit API thumbnail export and parameter extraction.
- `Cache`: PNG and JSON cache management.
- `Services`: Recursive scan orchestration.
- `Models`: DTOs used by UI, cache and services.
