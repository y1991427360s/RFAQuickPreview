using RFAQuickPreview.Models;

namespace RFAQuickPreview.Revit
{
    public interface IFamilyPreviewService
    {
        FamilyPreviewInfo Generate(string rfaPath, string thumbnailPath);
    }
}
