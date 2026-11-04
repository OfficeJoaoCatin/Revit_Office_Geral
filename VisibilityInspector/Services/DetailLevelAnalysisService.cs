using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class DetailLevelAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Detail Level";
        public int Priority => 11;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "DetailLevel",
                Title = "Detail Level",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Nível de detalhe da vista coletado."
            };

            issue.Details.Add($"Detail Level: {context.View.DetailLevel}");
            issue.Details.Add("A API não expõe uma regra universal para prever se a geometria interna da família aparece em Coarse/Medium/Fine.");
            return issue;
        }
    }
}
