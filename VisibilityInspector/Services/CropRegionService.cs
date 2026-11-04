using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class CropRegionService : IVisibilityAnalyzer
    {
        public string Name => "Crop Region";
        public int Priority => 6;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Crop",
                Title = "Crop Region",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            try
            {
                issue.Details.Add($"Crop ativo: {RevitText.YesNo(context.View.CropBoxActive)}");
                Parameter annotationCrop = context.View.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                issue.Details.Add($"Annotation Crop: {RevitText.ParameterValue(context.Document, annotationCrop)}");
                Parameter scopeBox = context.View.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                issue.Details.Add($"Scope Box: {RevitText.ParameterValue(context.Document, scopeBox)}");

                if (!context.View.CropBoxActive)
                {
                    issue.Summary = "Crop Region não está ativo.";
                    return issue;
                }

                BoundingBoxXYZ crop = context.View.CropBox;
                BoundingBoxXYZ elementBox = context.GetElementBoundingBoxInHostCoordinates();
                bool intersects = BoundingBoxUtil.Intersects(crop, elementBox);
                bool inside = BoundingBoxUtil.Contains(crop, elementBox);
                issue.Details.Add(context.IsLinkedElement ? "BoundingBox do elemento vinculado transformada para coordenadas do host." : "BoundingBox do elemento no modelo ativo.");
                issue.Details.Add($"BoundingBox intersecta o crop: {RevitText.YesNo(intersects)}");
                issue.Details.Add($"BoundingBox completamente dentro: {RevitText.YesNo(inside)}");

                issue.Severity = intersects ? IssueSeverity.Ok : IssueSeverity.Blocked;
                issue.Summary = intersects ? "Elemento intersecta a região de crop." : "Elemento fora da região de crop.";
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar Crop Region.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }
    }
}
