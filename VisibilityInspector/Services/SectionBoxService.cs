using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class SectionBoxService : IVisibilityAnalyzer
    {
        public string Name => "Section Box";
        public int Priority => 7;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "SectionBox",
                Title = "Section Box",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            if (!(context.View is View3D view3d))
            {
                issue.Summary = "Section Box não se aplica a esta vista.";
                return issue;
            }

            try
            {
                issue.Details.Add($"Section Box ativo: {RevitText.YesNo(view3d.IsSectionBoxActive)}");
                if (!view3d.IsSectionBoxActive)
                {
                    issue.Summary = "Section Box desligado.";
                    return issue;
                }

                BoundingBoxXYZ section = view3d.GetSectionBox();
                BoundingBoxXYZ elementBox = context.GetElementBoundingBoxInHostCoordinates();
                if (elementBox == null)
                {
                    issue.Severity = IssueSeverity.Warning;
                    issue.Summary = "Elemento sem BoundingBox para comparar com a Section Box.";
                    issue.Details.Add("A Revit API não retornou BoundingBox para este elemento nesta vista.");
                    return issue;
                }

                bool intersects = BoundingBoxUtil.IntersectsSectionBox(section, elementBox);
                issue.Details.Add(context.IsLinkedElement ? "BoundingBox do elemento vinculado transformada para coordenadas do host." : "BoundingBox do elemento no modelo ativo.");
                issue.Details.Add("Comparação feita no sistema local da Section Box, considerando rotação/transformação da vista 3D.");
                issue.Details.Add($"BoundingBox intersecta Section Box: {RevitText.YesNo(intersects)}");
                issue.Severity = intersects ? IssueSeverity.Ok : IssueSeverity.Blocked;
                issue.Summary = intersects ? "Elemento intersecta a Section Box." : "Elemento fora da Section Box.";
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar Section Box.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }
    }
}



