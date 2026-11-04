using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class DisciplineAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Discipline";
        public int Priority => 10;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Discipline",
                Title = "Discipline",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Disciplina da vista coletada."
            };

            issue.Details.Add($"Disciplina: {context.View.Discipline}");
            issue.Details.Add($"Categoria do elemento: {context.Element.Category?.Name ?? "Sem categoria"}");
            issue.Details.Add("Compatibilidade específica depende da lógica interna da categoria/discipline do Revit.");
            return issue;
        }
    }
}
