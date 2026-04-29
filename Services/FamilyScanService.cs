using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RFAQuickPreview.Cache;
using RFAQuickPreview.Models;
using RFAQuickPreview.Revit;

namespace RFAQuickPreview.Services
{
    public class FamilyScanService
    {
        private readonly PreviewCacheManager _cacheManager;
        private readonly IFamilyPreviewService _previewService;

        public FamilyScanService(PreviewCacheManager cacheManager, IFamilyPreviewService previewService)
        {
            _cacheManager = cacheManager;
            _previewService = previewService;
        }

        public IList<FamilyPreviewInfo> Scan(string folderPath, Action<ScanProgressInfo> progress)
        {
            var results = new List<FamilyPreviewInfo>();
            var files = Directory.EnumerateFiles(folderPath, "*.rfa", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                progress?.Invoke(new ScanProgressInfo
                {
                    Current = index + 1,
                    Total = files.Count,
                    Message = "Processing " + file
                });

                FamilyPreviewInfo info;
                if (_cacheManager.TryLoad(file, out info))
                {
                    results.Add(info);
                    progress?.Invoke(new ScanProgressInfo
                    {
                        Current = index + 1,
                        Total = files.Count,
                        Message = "Loaded from cache: " + Path.GetFileName(file)
                    });
                    continue;
                }

                var thumbnailPath = _cacheManager.GetThumbnailPath(file);
                info = _previewService.Generate(file, thumbnailPath);
                _cacheManager.Save(info);
                results.Add(info);

                progress?.Invoke(new ScanProgressInfo
                {
                    Current = index + 1,
                    Total = files.Count,
                    Message = string.IsNullOrWhiteSpace(info.ErrorMessage)
                        ? "Generated: " + Path.GetFileName(file)
                        : "Error: " + Path.GetFileName(file) + " - " + info.ErrorMessage
                });
            }

            return results;
        }
    }
}
