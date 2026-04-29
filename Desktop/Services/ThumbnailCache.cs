using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RFAQuickPreview.Desktop.Services;

public sealed class ThumbnailCache
{
    private readonly string _cacheRoot;

    public ThumbnailCache()
    {
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RFAQuickPreview",
            "Cache");
        Directory.CreateDirectory(_cacheRoot);
    }

    public string GetPath(string rfaPath)
    {
        return Path.Combine(_cacheRoot, GetCacheKey(rfaPath) + ".png");
    }

    public string GetMetadataPath(string rfaPath)
    {
        return Path.Combine(_cacheRoot, GetCacheKey(rfaPath) + ".json");
    }

    public bool TryReadDimensions(string rfaPath, out double frontWidthFeet, out double frontHeightFeet, out double leftWidthFeet)
    {
        frontWidthFeet = 0;
        frontHeightFeet = 0;
        leftWidthFeet = 0;
        var metadataPath = GetMetadataPath(rfaPath);
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            frontWidthFeet = root.TryGetProperty("FrontViewWidthFeet", out var widthElement)
                ? widthElement.GetDouble()
                : 0;
            frontHeightFeet = root.TryGetProperty("FrontViewHeightFeet", out var heightElement)
                ? heightElement.GetDouble()
                : 0;
            leftWidthFeet = root.TryGetProperty("LeftViewWidthFeet", out var leftWidthElement)
                ? leftWidthElement.GetDouble()
                : 0;

            return frontWidthFeet > 0 || frontHeightFeet > 0 || leftWidthFeet > 0;
        }
        catch
        {
            frontWidthFeet = 0;
            frontHeightFeet = 0;
            leftWidthFeet = 0;
            return false;
        }
    }

    public bool HasFreshDimensions(string rfaPath)
    {
        var metadataPath = GetMetadataPath(rfaPath);
        return File.Exists(metadataPath)
            && File.GetLastWriteTimeUtc(metadataPath) >= File.GetLastWriteTimeUtc(rfaPath)
            && TryReadDimensions(rfaPath, out var frontWidthFeet, out var frontHeightFeet, out var leftWidthFeet)
            && (frontWidthFeet > 0 || frontHeightFeet > 0)
            && leftWidthFeet > 0;
    }

    private string GetCacheKey(string rfaPath)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(rfaPath).ToLowerInvariant()));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    public bool IsFresh(string rfaPath, string thumbnailPath)
    {
        if (!File.Exists(thumbnailPath))
        {
            return false;
        }

        return File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(rfaPath);
    }
}
