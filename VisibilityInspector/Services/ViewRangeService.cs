using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class ViewRangeService : IVisibilityAnalyzer
    {
        public string Name => "View Range";
        public int Priority => 8;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "ViewRange",
                Title = "View Range",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            if (!(context.View is ViewPlan plan))
            {
                issue.Summary = "View Range não se aplica a esta vista.";
                issue.Details.Add($"Tipo de vista: {context.View.ViewType}");
                return issue;
            }

            try
            {
                PlanViewRange range = plan.GetViewRange();
                double top = PlaneElevation(context.HostDocument, range, PlanViewPlane.TopClipPlane);
                double cut = PlaneElevation(context.HostDocument, range, PlanViewPlane.CutPlane);
                double bottom = PlaneElevation(context.HostDocument, range, PlanViewPlane.BottomClipPlane);
                double depth = PlaneElevation(context.HostDocument, range, PlanViewPlane.ViewDepthPlane);
                BoundingBoxXYZ box = context.GetElementBoundingBoxInHostCoordinates();

                issue.Details.Add($"Top: {FormatFeet(top)}");
                issue.Details.Add($"Cut Plane: {FormatFeet(cut)}");
                issue.Details.Add($"Bottom: {FormatFeet(bottom)}");
                issue.Details.Add($"View Depth: {FormatFeet(depth)}");

                if (box == null)
                {
                    issue.Severity = IssueSeverity.Warning;
                    issue.Summary = "Elemento sem BoundingBox para comparar com View Range.";
                    return issue;
                }

                issue.Details.Add(context.IsLinkedElement ? "BoundingBox do elemento vinculado transformada para coordenadas do host." : "BoundingBox do elemento no modelo ativo.");
                issue.Details.Add($"Elemento Z min/max: {FormatFeet(box.Min.Z)} / {FormatFeet(box.Max.Z)}");
                if (box.Min.Z > top)
                {
                    issue.Severity = IssueSeverity.Blocked;
                    issue.Summary = "Elemento acima do plano superior.";
                }
                else if (box.Max.Z < depth)
                {
                    issue.Severity = IssueSeverity.Blocked;
                    issue.Summary = "Elemento abaixo do View Depth.";
                }
                else if (box.Max.Z < bottom)
                {
                    issue.Severity = IssueSeverity.Warning;
                    issue.Summary = "Elemento abaixo do Bottom, mas ainda pode intersectar View Depth.";
                }
                else
                {
                    issue.Summary = "Elemento dentro ou intersectando o View Range.";
                }
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar View Range.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }

        private static double PlaneElevation(Document doc, PlanViewRange range, PlanViewPlane plane)
        {
            ElementId levelId = range.GetLevelId(plane);
            double elevation = 0;
            if (doc.GetElement(levelId) is Level level)
                elevation = level.ProjectElevation;
            return elevation + range.GetOffset(plane);
        }

        private static string FormatFeet(double value)
        {
            return $"{value:0.###} ft";
        }
    }
}
