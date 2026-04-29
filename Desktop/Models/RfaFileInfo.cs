using System.IO;

namespace RFAQuickPreview.Desktop.Models;

public sealed class RfaFileInfo
{
    public string FileName { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public string DirectoryName { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public DateTime ModifiedTime { get; init; }

    public string ThumbnailPath { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string FileSizeText => FileSizeBytes >= 1024L * 1024L
        ? $"{FileSizeBytes / 1024d / 1024d:0.0} MB"
        : $"{FileSizeBytes / 1024d:0.0} KB";

    public static RfaFileInfo FromPath(string path)
    {
        var file = new FileInfo(path);
        return new RfaFileInfo
        {
            FileName = file.Name,
            FullPath = file.FullName,
            DirectoryName = file.DirectoryName ?? string.Empty,
            FileSizeBytes = file.Length,
            ModifiedTime = file.LastWriteTime
        };
    }
}
