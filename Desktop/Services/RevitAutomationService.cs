using System.Diagnostics;
using System.IO;

namespace RFAQuickPreview.Desktop.Services;

public sealed class RevitAutomationService
{
    private readonly string _automationRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RFAQuickPreview",
        "Automation");

    private readonly PortableSettings _settings = PortableSettings.Load();

    public async Task<string> RequestPlaceFamilyAsync(string familyPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(familyPath) || !File.Exists(familyPath))
        {
            return "Family file not found.";
        }

        var installResult = EnsureAutomationAddinInstalled(out var installedHelperPath);
        if (!string.IsNullOrWhiteSpace(installResult))
        {
            return installResult;
        }

        if (!Process.GetProcessesByName("Revit").Any())
        {
            return "Revit is not running. Open your Revit model first, then try again.";
        }

        if (!IsInstalledHelperLoadedInRevit(installedHelperPath))
        {
            return "The Revit helper was updated. Close and reopen Revit once, then place the family again.";
        }

        Directory.CreateDirectory(_automationRoot);
        var requestId = Guid.NewGuid().ToString("N");
        var requestPath = Path.Combine(_automationRoot, "place_request.txt");
        var donePath = Path.Combine(_automationRoot, "place_done_" + requestId + ".txt");
        File.WriteAllLines(requestPath, new[] { requestId, familyPath }, System.Text.Encoding.UTF8);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(donePath))
            {
                var text = await File.ReadAllTextAsync(donePath, cancellationToken);
                return text.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                    ? "Revit placement started. Click in the model to place the family."
                    : text;
            }

            await Task.Delay(250, cancellationToken);
        }

        return "Placement request sent. If Revit does not respond, restart Revit once so the helper add-in can load.";
    }

    public async Task<string> GenerateFolderPreviewsAsync(string folderPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        return await GeneratePreviewsAsync(new[] { folderPath }, "Revit previews generated.", progress, cancellationToken);
    }

    public async Task<string> GenerateFilePreviewsAsync(IReadOnlyList<string> familyPaths, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var existingPaths = familyPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existingPaths.Count == 0)
        {
            return "No changed family previews to generate.";
        }

        return await GeneratePreviewsAsync(existingPaths, "Changed Revit previews generated.", progress, cancellationToken);
    }

    private async Task<string> GeneratePreviewsAsync(IReadOnlyList<string> requestLines, string successMessage, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var revitExePath = FindRevitExePath();
        if (string.IsNullOrWhiteSpace(revitExePath))
        {
            return "Revit.exe not found.";
        }

        var installResult = EnsureAutomationAddinInstalled(out _);
        if (!string.IsNullOrWhiteSpace(installResult))
        {
            return installResult;
        }

        if (Process.GetProcessesByName("Revit").Any())
        {
            return "Revit is already running. Close Revit or use existing cache.";
        }

        Directory.CreateDirectory(_automationRoot);
        var requestId = Guid.NewGuid().ToString("N");
        var requestPath = Path.Combine(_automationRoot, "request.txt");
        var donePath = Path.Combine(_automationRoot, "done_" + requestId + ".txt");
        var logPath = Path.Combine(_automationRoot, "log_" + requestId + ".txt");

        File.WriteAllLines(requestPath, new[] { requestId }.Concat(requestLines), System.Text.Encoding.UTF8);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = revitExePath,
            Arguments = "/nosplash",
            UseShellExecute = false,
            CreateNoWindow = false
        });

        progress?.Report("Started Revit automation.");
        var lastLogLength = 0L;
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(logPath))
            {
                var info = new FileInfo(logPath);
                if (info.Length != lastLogLength)
                {
                    lastLogLength = info.Length;
                    var lastLine = File.ReadLines(logPath).LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(lastLine))
                    {
                        progress?.Report(lastLine);
                    }
                }
            }

            if (File.Exists(donePath))
            {
                var text = await File.ReadAllTextAsync(donePath, cancellationToken);
                return text.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                    ? successMessage
                    : text;
            }

            await Task.Delay(1000, cancellationToken);
        }

        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }

        return "Timed out while waiting for Revit preview generation.";
    }

    public async Task<string> RefreshFamilyPreviewAsync(string familyPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(familyPath) || !File.Exists(familyPath))
        {
            return "Family file not found.";
        }

        var revitExePath = FindRevitExePath();
        if (string.IsNullOrWhiteSpace(revitExePath))
        {
            return "Revit.exe not found.";
        }

        var installResult = EnsureAutomationAddinInstalled(out _);
        if (!string.IsNullOrWhiteSpace(installResult))
        {
            return installResult;
        }

        if (Process.GetProcessesByName("Revit").Any())
        {
            return "Revit is already running. Close Revit or use existing cache.";
        }

        Directory.CreateDirectory(_automationRoot);
        var requestId = Guid.NewGuid().ToString("N");
        var requestPath = Path.Combine(_automationRoot, "refresh_request.txt");
        var donePath = Path.Combine(_automationRoot, "refresh_done_" + requestId + ".txt");
        var logPath = Path.Combine(_automationRoot, "refresh_log_" + requestId + ".txt");

        File.WriteAllLines(requestPath, new[] { requestId, familyPath }, System.Text.Encoding.UTF8);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = revitExePath,
            Arguments = "/nosplash",
            UseShellExecute = false,
            CreateNoWindow = false
        });

        progress?.Report("Started Revit refresh.");
        var lastLogLength = 0L;
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(logPath))
            {
                var info = new FileInfo(logPath);
                if (info.Length != lastLogLength)
                {
                    lastLogLength = info.Length;
                    var lastLine = File.ReadLines(logPath).LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(lastLine))
                    {
                        progress?.Report(lastLine);
                    }
                }
            }

            if (File.Exists(donePath))
            {
                var text = await File.ReadAllTextAsync(donePath, cancellationToken);
                return text.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                    ? "Revit preview refreshed."
                    : text;
            }

            await Task.Delay(1000, cancellationToken);
        }

        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }

        return "Timed out while refreshing Revit preview.";
    }

    private string? FindRevitExePath()
    {
        var candidates = new[]
        {
            _settings.RevitExePath,
            @"D:\Autodesk\REVIT2020\Revit 2020\Revit.exe",
            @"C:\Program Files\Autodesk\Revit 2020\Revit.exe",
            @"C:\Program Files\Autodesk\Revit 2020\Revit.exe"
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string? EnsureAutomationAddinInstalled(out string installedHelperPath)
    {
        installedHelperPath = string.Empty;
        var source = Path.Combine(AppContext.BaseDirectory, "RevitHelper", "RFAQuickPreview.dll");
        if (!File.Exists(source))
        {
            return "Revit helper was not found: " + source;
        }

        var addinRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "Revit",
            "Addins",
            "2020");
        var sourceInfo = new FileInfo(source);
        var helperVersion = sourceInfo.LastWriteTimeUtc.Ticks.ToString("x") + "_" + sourceInfo.Length.ToString("x");
        var helperDir = Path.Combine(addinRoot, "RFAQuickPreviewAutomation", helperVersion);
        Directory.CreateDirectory(helperDir);

        var target = Path.Combine(helperDir, "RFAQuickPreview.dll");
        installedHelperPath = target;
        if (!File.Exists(target))
        {
            File.Copy(source, target, false);
        }

        var manifestPath = Path.Combine(addinRoot, "RFAQuickPreview.Automation.addin");
        var manifest = $"""
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>RFAQuickPreviewAutomation</Name>
    <Assembly>{target}</Assembly>
    <AddInId>1A7CBA6E-0553-4CBB-9D5F-206D36E7E20D</AddInId>
    <FullClassName>RFAQuickPreview.App.AutomationApplication</FullClassName>
    <VendorId>RFQP</VendorId>
    <VendorDescription>RFAQuickPreview</VendorDescription>
  </AddIn>
</RevitAddIns>
""";
        File.WriteAllText(manifestPath, manifest, System.Text.Encoding.UTF8);
        return null;
    }

    private bool IsInstalledHelperLoadedInRevit(string installedHelperPath)
    {
        var loadedPathFile = Path.Combine(_automationRoot, "helper_loaded.txt");
        if (!File.Exists(loadedPathFile))
        {
            return false;
        }

        var loadedPath = File.ReadLines(loadedPathFile).FirstOrDefault();
        return string.Equals(
            Path.GetFullPath(loadedPath ?? string.Empty),
            Path.GetFullPath(installedHelperPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
