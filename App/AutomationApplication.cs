using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RFAQuickPreview.Cache;
using RFAQuickPreview.Revit;
using RFAQuickPreview.Services;

namespace RFAQuickPreview.App
{
    public class AutomationApplication : IExternalApplication
    {
        private bool _scanProcessed;
        private System.Threading.Timer _dialogWatcher;

        public Result OnStartup(UIControlledApplication application)
        {
            WriteLoadedHelperInfo();
            application.DialogBoxShowing += OnDialogBoxShowing;
            application.Idling += OnIdling;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            StopDialogWatcher();
            application.DialogBoxShowing -= OnDialogBoxShowing;
            application.Idling -= OnIdling;
            return Result.Succeeded;
        }

        private static void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            var taskDialog = e as TaskDialogShowingEventArgs;
            if (taskDialog != null)
            {
                e.OverrideResult(TaskDialogCommandLink1);
                return;
            }

            e.OverrideResult(DialogResultOk);
        }

        #region Win32 Dialog Watcher

        private void StartDialogWatcher()
        {
            if (_dialogWatcher != null)
            {
                return;
            }

            _dialogWatcher = new System.Threading.Timer(_ =>
            {
                CloseRevitWarningDialogs();
            }, null, 500, 500);
        }

        private void StopDialogWatcher()
        {
            if (_dialogWatcher != null)
            {
                _dialogWatcher.Dispose();
                _dialogWatcher = null;
            }
        }

        private static void CloseRevitWarningDialogs()
        {
            try
            {
                var mainWindow = FindRevitMainWindow();
                if (mainWindow == IntPtr.Zero)
                {
                    return;
                }

                var dialogs = new List<IntPtr>();
                EnumWindows((hwnd, _) =>
                {
                    if (!IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    // Must be owned by Revit main window
                    if (GetWindow(hwnd, GW_OWNER) != mainWindow)
                    {
                        return true;
                    }

                    // Must be a popup/dialog style (not a child window)
                    var style = GetWindowLong(hwnd, GWL_STYLE);
                    if ((style & WS_CHILD) != 0)
                    {
                        return true;
                    }

                    dialogs.Add(hwnd);
                    return true;
                }, IntPtr.Zero);

                foreach (var hwnd in dialogs)
                {
                    if (IsUpdaterWarningDialog(hwnd))
                    {
                        ClickContinueButton(hwnd);
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsUpdaterWarningDialog(IntPtr hwnd)
        {
            var text = GetWindowTreeText(hwnd);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (ContainsAny(
                text,
                "\u7b2c\u4e09\u65b9\u66f4\u65b0\u7a0b\u5e8f",
                "\u66f4\u65b0\u7a0b\u5e8f",
                "third party updater",
                "updater",
                "BoChao.Revit.Events",
                "STDR Addition Updater"))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClickContinueButton(IntPtr dialog)
        {
            var buttons = new List<IntPtr>();
            EnumChildWindows(dialog, (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                var className = GetWindowClassName(hwnd);
                if (className.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    buttons.Add(hwnd);
                }

                return true;
            }, IntPtr.Zero);

            var continueButton = buttons.FirstOrDefault(hwnd =>
                ContainsAny(
                    GetWindowTitle(hwnd),
                    "\u7ee7\u7eed",
                    "\u5904\u7406\u6587\u4ef6",
                    "continue",
                    "working with file"));

            if (continueButton == IntPtr.Zero && buttons.Count > 0)
            {
                continueButton = buttons[0];
            }

            if (continueButton != IntPtr.Zero)
            {
                PostMessage(continueButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static IntPtr FindRevitMainWindow()
        {
            var result = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                var title = GetWindowTitle(hwnd);
                if (!string.IsNullOrEmpty(title) &&
                    title.IndexOf("Revit", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    GetWindow(hwnd, GW_OWNER) == IntPtr.Zero &&
                    (GetWindowLong(hwnd, GWL_STYLE) & WS_CHILD) == 0)
                {
                    result = hwnd;
                    return false; // stop enumeration
                }

                return true;
            }, IntPtr.Zero);

            return result;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetWindowClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetWindowTreeText(IntPtr hwnd)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetWindowTitle(hwnd));

            EnumChildWindows(hwnd, (child, _) =>
            {
                var text = GetWindowTitle(child);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }

                return true;
            }, IntPtr.Zero);

            return sb.ToString();
        }

        #endregion

        #region Win32 Imports

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint GW_OWNER = 4;
        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const uint BM_CLICK = 0x00F5;
        private const int DialogResultOk = 1;
        private const int TaskDialogCommandLink1 = 1001;

        #endregion

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            var uiApplication = sender as UIApplication;
            if (uiApplication == null)
            {
                return;
            }

            var automationRoot = GetAutomationRoot();
            ProcessPlaceRequest(uiApplication, automationRoot);
            ProcessRefreshRequest(uiApplication, automationRoot);

            if (_scanProcessed)
            {
                return;
            }

            var requestPath = Path.Combine(automationRoot, "request.txt");
            if (!File.Exists(requestPath))
            {
                return;
            }

            _scanProcessed = true;

            // Start watching for warning dialogs before opening any files
            StartDialogWatcher();

            string requestId = null;
            try
            {
                var lines = File.ReadAllLines(requestPath, Encoding.UTF8);
                if (lines.Length < 2)
                {
                    return;
                }

                requestId = lines[0];
                var folderPath = lines[1];
                File.Delete(requestPath);

                var cacheManager = new PreviewCacheManager();
                var previewService = new FamilyPreviewService(uiApplication);
                var scanService = new FamilyScanService(cacheManager, previewService);
                var logPath = Path.Combine(automationRoot, "log_" + requestId + ".txt");
                var results = scanService.Scan(folderPath, progress =>
                {
                    File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + progress.Message + Environment.NewLine, Encoding.UTF8);
                });

                File.WriteAllText(
                    Path.Combine(automationRoot, "done_" + requestId + ".txt"),
                    "OK" + Environment.NewLine + results.Count,
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(requestId))
                {
                    File.WriteAllText(
                        Path.Combine(automationRoot, "done_" + requestId + ".txt"),
                        "ERROR" + Environment.NewLine + ex,
                        Encoding.UTF8);
                }
            }
            finally
            {
                StopDialogWatcher();
                TryExitRevit(uiApplication);
            }
        }

        private void ProcessRefreshRequest(UIApplication uiApplication, string automationRoot)
        {
            var requestPath = Path.Combine(automationRoot, "refresh_request.txt");
            if (!File.Exists(requestPath))
            {
                return;
            }

            string requestId = null;
            try
            {
                StartDialogWatcher();

                var lines = File.ReadAllLines(requestPath, Encoding.UTF8);
                if (lines.Length < 2)
                {
                    File.Delete(requestPath);
                    return;
                }

                requestId = lines[0];
                var familyPath = lines[1];
                File.Delete(requestPath);

                var cacheManager = new PreviewCacheManager();
                var previewService = new FamilyPreviewService(uiApplication);
                var scanService = new FamilyScanService(cacheManager, previewService);
                var logPath = Path.Combine(automationRoot, "refresh_log_" + requestId + ".txt");
                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " Refreshing " + familyPath + Environment.NewLine, Encoding.UTF8);

                var info = scanService.RefreshFile(familyPath);
                File.AppendAllText(
                    logPath,
                    DateTime.Now.ToString("HH:mm:ss") + " " +
                    (string.IsNullOrWhiteSpace(info.ErrorMessage) ? "Refreshed: " : "Error: ") +
                    Path.GetFileName(familyPath) +
                    (string.IsNullOrWhiteSpace(info.ErrorMessage) ? string.Empty : " - " + info.ErrorMessage) +
                    Environment.NewLine,
                    Encoding.UTF8);

                WriteRefreshResult(
                    automationRoot,
                    requestId,
                    string.IsNullOrWhiteSpace(info.ErrorMessage)
                        ? "OK"
                        : "ERROR" + Environment.NewLine + info.ErrorMessage);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(requestId))
                {
                    WriteRefreshResult(automationRoot, requestId, "ERROR" + Environment.NewLine + ex);
                }
            }
            finally
            {
                StopDialogWatcher();
                TryExitRevit(uiApplication);
            }
        }

        private static void ProcessPlaceRequest(UIApplication uiApplication, string automationRoot)
        {
            var requestPath = Path.Combine(automationRoot, "place_request.txt");
            if (!File.Exists(requestPath))
            {
                return;
            }

            string requestId = null;
            try
            {
                var lines = File.ReadAllLines(requestPath, Encoding.UTF8);
                if (lines.Length < 2)
                {
                    File.Delete(requestPath);
                    return;
                }

                requestId = lines[0];
                var familyPath = lines[1];
                File.Delete(requestPath);

                var symbol = PrepareFamilyForPlacement(uiApplication, familyPath);
                WritePlaceResult(automationRoot, requestId, "OK");
                uiApplication.ActiveUIDocument.PostRequestForElementTypePlacement(symbol);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(requestId))
                {
                    WritePlaceResult(automationRoot, requestId, "ERROR" + Environment.NewLine + ex.Message);
                }
            }
        }

        private static FamilySymbol PrepareFamilyForPlacement(UIApplication uiApplication, string familyPath)
        {
            if (string.IsNullOrWhiteSpace(familyPath) || !File.Exists(familyPath))
            {
                throw new FileNotFoundException("Family file was not found.", familyPath);
            }

            var uiDocument = uiApplication.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document == null)
            {
                throw new InvalidOperationException("No active Revit document is open.");
            }

            var document = uiDocument.Document;
            if (IsSameModelPath(document.PathName, familyPath))
            {
                throw new InvalidOperationException("The active family cannot be placed into itself.");
            }

            Family family;
            using (var transaction = new Transaction(document, "Load RFAQuickPreview Family"))
            {
                transaction.Start();
                if (!document.LoadFamily(familyPath, out family))
                {
                    family = FindFamilyByName(document, Path.GetFileNameWithoutExtension(familyPath));
                }
                transaction.Commit();
            }

            if (family == null)
            {
                throw new InvalidOperationException("Revit could not load this family.");
            }

            var symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            if (symbolId == null || symbolId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("This family does not contain a placeable type.");
            }

            var symbol = document.GetElement(symbolId) as FamilySymbol;
            if (symbol == null)
            {
                throw new InvalidOperationException("This family type is not valid for placement.");
            }

            if (!symbol.IsActive)
            {
                using (var transaction = new Transaction(document, "Activate RFAQuickPreview Family Type"))
                {
                    transaction.Start();
                    symbol.Activate();
                    transaction.Commit();
                }
            }

            return symbol;
        }

        private static Family FindFamilyByName(Document document, string familyName)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSameModelPath(string activeDocumentPath, string familyPath)
        {
            if (string.IsNullOrWhiteSpace(activeDocumentPath) || string.IsNullOrWhiteSpace(familyPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(activeDocumentPath),
                    Path.GetFullPath(familyPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(activeDocumentPath, familyPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void WritePlaceResult(string automationRoot, string requestId, string text)
        {
            File.WriteAllText(
                Path.Combine(automationRoot, "place_done_" + requestId + ".txt"),
                text,
                Encoding.UTF8);
        }

        private static void WriteRefreshResult(string automationRoot, string requestId, string text)
        {
            File.WriteAllText(
                Path.Combine(automationRoot, "refresh_done_" + requestId + ".txt"),
                text,
                Encoding.UTF8);
        }

        private static string GetAutomationRoot()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RFAQuickPreview",
                "Automation");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteLoadedHelperInfo()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                File.WriteAllText(
                    Path.Combine(GetAutomationRoot(), "helper_loaded.txt"),
                    location + Environment.NewLine + DateTime.UtcNow.ToString("O"),
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static void TryExitRevit(UIApplication uiApplication)
        {
            try
            {
                uiApplication.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
            }
            catch
            {
            }
        }
    }
}
