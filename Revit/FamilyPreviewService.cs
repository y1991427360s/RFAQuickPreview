using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RFAQuickPreview.Models;

namespace RFAQuickPreview.Revit
{
    public class FamilyPreviewService : IFamilyPreviewService
    {
        private readonly UIApplication _uiApplication;

        public FamilyPreviewService(UIApplication uiApplication)
        {
            _uiApplication = uiApplication;
        }

        public FamilyPreviewInfo Generate(string rfaPath, string thumbnailPath)
        {
            var info = FamilyPreviewInfo.FromFile(rfaPath);
            Document document = null;

            try
            {
                document = _uiApplication.Application.OpenDocumentFile(rfaPath);
                if (!document.IsFamilyDocument)
                {
                    throw new InvalidOperationException("The file is not a Revit family document.");
                }

                ReadFamilyInfo(document, info);
                ReadFrontViewDimensions(document, info);
                ExportThumbnail(document, thumbnailPath);
                info.ThumbnailPath = thumbnailPath;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }
            finally
            {
                if (document != null)
                {
                    try
                    {
                        document.Close(false);
                    }
                    catch
                    {
                    }
                }
            }

            return info;
        }

        private static void ReadFamilyInfo(Document document, FamilyPreviewInfo info)
        {
            var family = document.OwnerFamily;
            info.FamilyName = family == null ? info.FamilyName : family.Name;
            info.FamilyCategory = family?.FamilyCategory?.Name ?? string.Empty;

            var manager = document.FamilyManager;
            if (manager == null)
            {
                return;
            }

            var types = manager.Types.Cast<FamilyType>().ToList();
            info.TypeNames = types.Select(t => t.Name).OrderBy(n => n).ToList();

            var currentType = manager.CurrentType ?? types.FirstOrDefault();
            foreach (FamilyParameter parameter in manager.Parameters)
            {
                if (parameter == null || parameter.Definition == null)
                {
                    continue;
                }

                info.Parameters.Add(new FamilyParameterInfo
                {
                    Name = parameter.Definition.Name,
                    StorageType = parameter.StorageType.ToString(),
                    Value = GetParameterValue(currentType, parameter),
                    IsInstance = parameter.IsInstance,
                    GroupName = parameter.Definition.ParameterGroup.ToString()
                });
            }
        }

        private static string GetParameterValue(FamilyType type, FamilyParameter parameter)
        {
            if (type == null || parameter == null)
            {
                return string.Empty;
            }

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.Double:
                        var doubleValue = type.AsDouble(parameter);
                        return doubleValue.HasValue
                            ? doubleValue.Value.ToString("0.####", CultureInfo.InvariantCulture)
                            : string.Empty;
                    case StorageType.Integer:
                        var integerValue = type.AsInteger(parameter);
                        return integerValue.HasValue
                            ? integerValue.Value.ToString(CultureInfo.InvariantCulture)
                            : string.Empty;
                    case StorageType.String:
                        return type.AsString(parameter) ?? string.Empty;
                    case StorageType.ElementId:
                        return type.AsElementId(parameter)?.IntegerValue.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void ReadFrontViewDimensions(Document document, FamilyPreviewInfo info)
        {
            var points = new List<XYZ>();
            var options = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true
            };

            foreach (var element in new FilteredElementCollector(document)
                .WhereElementIsNotElementType())
            {
                try
                {
                    AddGeometryPoints(element.get_Geometry(options), Transform.Identity, points);
                }
                catch
                {
                }
            }

            if (points.Count == 0)
            {
                return;
            }

            var minX = points.Min(point => point.X);
            var maxX = points.Max(point => point.X);
            var minY = points.Min(point => point.Y);
            var maxY = points.Max(point => point.Y);
            var minZ = points.Min(point => point.Z);
            var maxZ = points.Max(point => point.Z);

            info.FrontViewWidthFeet = Math.Max(0, maxX - minX);
            info.FrontViewHeightFeet = Math.Max(0, maxZ - minZ);
            info.LeftViewWidthFeet = Math.Max(0, maxY - minY);
        }

        private static void AddGeometryPoints(GeometryElement geometryElement, Transform transform, IList<XYZ> points)
        {
            if (geometryElement == null)
            {
                return;
            }

            foreach (var geometryObject in geometryElement)
            {
                var solid = geometryObject as Solid;
                if (solid != null && solid.Volume > 1e-9)
                {
                    foreach (Face face in solid.Faces)
                    {
                        var mesh = face.Triangulate();
                        foreach (XYZ vertex in mesh.Vertices)
                        {
                            points.Add(transform.OfPoint(vertex));
                        }
                    }
                    continue;
                }

                var meshObject = geometryObject as Mesh;
                if (meshObject != null && meshObject.Vertices.Count > 0)
                {
                    foreach (XYZ vertex in meshObject.Vertices)
                    {
                        points.Add(transform.OfPoint(vertex));
                    }
                    continue;
                }

                var instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    AddGeometryPoints(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform), points);
                }
            }
        }

        private static void ExportThumbnail(Document document, string thumbnailPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath));
            var view = GetOrCreatePreviewView(document);
            ConfigurePreviewView(document, view);

            var tempDir = Path.Combine(Path.GetTempPath(), "RFAQuickPreview", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var exportBase = Path.Combine(tempDir, "preview");

            try
            {
                var options = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews,
                    FilePath = exportBase,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    PixelSize = 512,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ShouldCreateWebSite = false,
                    ZoomType = ZoomFitType.FitToPage
                };
                options.SetViewsAndSheets(new List<ElementId> { view.Id });

                document.ExportImage(options);

                var png = Directory.GetFiles(tempDir, "*.png", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (png == null)
                {
                    throw new InvalidOperationException("Revit did not produce a PNG thumbnail.");
                }

                File.Copy(png, thumbnailPath, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }

        private static View3D GetOrCreatePreviewView(Document document)
        {
            var existing = new FilteredElementCollector(document)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);

            if (existing != null)
            {
                return existing;
            }

            var viewFamilyType = new FilteredElementCollector(document)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

            if (viewFamilyType == null)
            {
                throw new InvalidOperationException("No 3D view family type is available in this family.");
            }

            using (var transaction = new Transaction(document, "Create RFAQuickPreview 3D View"))
            {
                transaction.Start();
                var view = View3D.CreateIsometric(document, viewFamilyType.Id);
                view.Name = "RFAQuickPreview 3D";
                transaction.Commit();
                return view;
            }
        }

        private static void ConfigurePreviewView(Document document, View3D view)
        {
            using (var transaction = new Transaction(document, "Configure RFAQuickPreview View"))
            {
                transaction.Start();

                view.DetailLevel = ViewDetailLevel.Fine;
                view.DisplayStyle = DisplayStyle.Shading;
                var forward = new XYZ(-1, 1, -0.6).Normalize();
                var right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                var up = right.CrossProduct(forward).Normalize();
                view.SetOrientation(new ViewOrientation3D(new XYZ(10, -10, 8), up, forward));

                HideCategory(document, view, BuiltInCategory.OST_CLines);
                HideCategory(document, view, BuiltInCategory.OST_Dimensions);
                HideCategory(document, view, BuiltInCategory.OST_TextNotes);
                HideCategory(document, view, BuiltInCategory.OST_GenericAnnotation);
                HideCategory(document, view, BuiltInCategory.OST_Levels);
                HideCategory(document, view, BuiltInCategory.OST_Grids);

                transaction.Commit();
            }
        }

        private static void HideCategory(Document document, View view, BuiltInCategory builtInCategory)
        {
            try
            {
                var category = Category.GetCategory(document, builtInCategory);
                if (category != null && view.CanCategoryBeHidden(category.Id))
                {
                    view.SetCategoryHidden(category.Id, true);
                }
            }
            catch
            {
            }
        }
    }
}
