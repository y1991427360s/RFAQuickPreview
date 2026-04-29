using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public Result OnStartup(UIControlledApplication application)
        {
            WriteLoadedHelperInfo();
            application.Idling += OnIdling;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.Idling -= OnIdling;
            return Result.Succeeded;
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            var uiApplication = sender as UIApplication;
            if (uiApplication == null)
            {
                return;
            }

            var automationRoot = GetAutomationRoot();
            ProcessPlaceRequest(uiApplication, automationRoot);

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
