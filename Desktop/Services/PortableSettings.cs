using System.IO;
using System.Text.Json;

namespace RFAQuickPreview.Desktop.Services;

public sealed class PortableSettings
{
    public string? RevitExePath { get; set; }

    public static PortableSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RFAQuickPreview.config.json");
        if (!File.Exists(path))
        {
            return new PortableSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<PortableSettings>(File.ReadAllText(path)) ?? new PortableSettings();
        }
        catch
        {
            return new PortableSettings();
        }
    }
}
