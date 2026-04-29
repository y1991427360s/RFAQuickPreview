using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using RFAQuickPreview.Models;

namespace RFAQuickPreview.Cache
{
    public class PreviewCacheManager
    {
        private readonly string _cacheRoot;

        public PreviewCacheManager()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFAQuickPreview", "Cache"))
        {
        }

        public PreviewCacheManager(string cacheRoot)
        {
            _cacheRoot = cacheRoot;
            Directory.CreateDirectory(_cacheRoot);
        }

        public string GetThumbnailPath(string rfaPath)
        {
            return Path.Combine(_cacheRoot, GetCacheKey(rfaPath) + ".png");
        }

        public string GetMetadataPath(string rfaPath)
        {
            return Path.Combine(_cacheRoot, GetCacheKey(rfaPath) + ".json");
        }

        public bool TryLoad(string rfaPath, out FamilyPreviewInfo info)
        {
            info = null;
            var metadataPath = GetMetadataPath(rfaPath);
            var thumbnailPath = GetThumbnailPath(rfaPath);
            if (!File.Exists(metadataPath) || !File.Exists(thumbnailPath) || !File.Exists(rfaPath))
            {
                return false;
            }

            try
            {
                var file = new FileInfo(rfaPath);
                using (var stream = File.OpenRead(metadataPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(FamilyPreviewInfo));
                    info = (FamilyPreviewInfo)serializer.ReadObject(stream);
                }

                if (info == null || info.ModifiedTimeUtc != file.LastWriteTimeUtc)
                {
                    info = null;
                    return false;
                }

                info.ThumbnailPath = thumbnailPath;
                return true;
            }
            catch
            {
                info = null;
                return false;
            }
        }

        public void Save(FamilyPreviewInfo info)
        {
            Directory.CreateDirectory(_cacheRoot);
            info.ThumbnailPath = GetThumbnailPath(info.FullPath);
            var metadataPath = GetMetadataPath(info.FullPath);

            using (var stream = File.Create(metadataPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(FamilyPreviewInfo));
                serializer.WriteObject(stream, info);
            }
        }

        private static string GetCacheKey(string path)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant()));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
