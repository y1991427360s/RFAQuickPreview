using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using RFAQuickPreview.Cache;
using RFAQuickPreview.Models;
using RFAQuickPreview.Revit;
using RFAQuickPreview.UI;

namespace RFAQuickPreview.App
{
    public class ScanExternalEventHandler : IExternalEventHandler
    {
        private readonly object _syncRoot = new object();
        private readonly List<FamilyPreviewInfo> _results = new List<FamilyPreviewInfo>();
        private string _folderPath;
        private MainWindow _window;
        private ExternalEvent _externalEvent;
        private PreviewCacheManager _cacheManager;
        private List<string> _files;
        private int _index;
        private bool _isScanning;

        public void SetExternalEvent(ExternalEvent externalEvent)
        {
            _externalEvent = externalEvent;
        }

        public void SetWindow(MainWindow window)
        {
            _window = window;
        }

        public void RequestScan(string folderPath)
        {
            lock (_syncRoot)
            {
                _folderPath = folderPath;
                _files = null;
                _index = 0;
                _results.Clear();
                _isScanning = true;
            }
        }

        public void Execute(UIApplication app)
        {
            if (_window == null)
            {
                return;
            }

            try
            {
                EnsureScanStarted();
                ProcessOne(app);
            }
            catch (Exception ex)
            {
                _isScanning = false;
                _window.ReportScanFailed(ex);
            }
        }

        private void EnsureScanStarted()
        {
            if (_files != null)
            {
                return;
            }

            string folderPath;
            lock (_syncRoot)
            {
                folderPath = _folderPath;
                _folderPath = null;
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                _isScanning = false;
                return;
            }

            _cacheManager = new PreviewCacheManager();
            _files = Directory.EnumerateFiles(folderPath, "*.rfa", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _index = 0;

            _window.ReportScanProgress(new ScanProgressInfo
            {
                Current = 0,
                Total = _files.Count,
                Message = "Found " + _files.Count + " RFA files."
            });

            if (_files.Count == 0)
            {
                _isScanning = false;
                _window.ReportScanCompleted(_results);
            }
        }

        private void ProcessOne(UIApplication app)
        {
            if (!_isScanning || _files == null || _index >= _files.Count)
            {
                _isScanning = false;
                _window.ReportScanCompleted(_results);
                return;
            }

            var currentIndex = _index;
            var file = _files[currentIndex];
            _index++;

            _window.ReportScanProgress(new ScanProgressInfo
            {
                Current = currentIndex + 1,
                Total = _files.Count,
                Message = "Processing " + file
            });

            FamilyPreviewInfo info;
            if (_cacheManager.TryLoad(file, out info))
            {
                _results.Add(info);
                _window.ReportFamilyScanned(info);
                _window.ReportScanProgress(new ScanProgressInfo
                {
                    Current = currentIndex + 1,
                    Total = _files.Count,
                    Message = "Loaded from cache: " + Path.GetFileName(file)
                });
            }
            else
            {
                var previewService = new FamilyPreviewService(app);
                var thumbnailPath = _cacheManager.GetThumbnailPath(file);
                info = previewService.Generate(file, thumbnailPath);
                _cacheManager.Save(info);
                _results.Add(info);
                _window.ReportFamilyScanned(info);
                _window.ReportScanProgress(new ScanProgressInfo
                {
                    Current = currentIndex + 1,
                    Total = _files.Count,
                    Message = string.IsNullOrWhiteSpace(info.ErrorMessage)
                        ? "Generated: " + Path.GetFileName(file)
                        : "Error: " + Path.GetFileName(file) + " - " + info.ErrorMessage
                });
            }

            if (_index >= _files.Count)
            {
                _isScanning = false;
                _window.ReportScanCompleted(_results);
                return;
            }

            _window.ScheduleNextScanStep();
        }

        public void ContinueScan()
        {
            if (_isScanning && _externalEvent != null)
            {
                _externalEvent.Raise();
            }
        }

        public string GetName()
        {
            return "RFAQuickPreview Scan";
        }
    }
}
