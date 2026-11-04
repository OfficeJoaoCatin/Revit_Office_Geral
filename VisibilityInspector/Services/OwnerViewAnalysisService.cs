using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class OwnerViewAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Owner View";
        public int Priority => 14;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "OwnerView",
                Title = "Owner View",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            ElementId ownerViewId = context.Element.OwnerViewId;
            if (ownerViewId == ElementId.InvalidElementId)
            {
                issue.Summary = "Elemento não é específico de uma vista.";
                issue.Details.Add("OwnerViewId: N/A");
                return issue;
            }

            RevitView owner = context.ElementDocument.GetElement(ownerViewId) as RevitView;
            if (context.IsLinkedElement)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Details.Add($"Owner View no documento vinculado: {owner?.Name ?? RevitText.Id(ownerViewId)}");
                issue.Details.Add($"ViewSpecific: {RevitText.YesNo(context.Element.ViewSpecific)}");
                issue.Summary = "Elemento é específico de uma vista dentro do modelo vinculado; a visibilidade no host depende da configuração de exibição do link.";
                return issue;
            }

            bool sameView = ownerViewId == context.View.Id;
            issue.Details.Add($"Owner View: {owner?.Name ?? RevitText.Id(ownerViewId)}");
            issue.Details.Add($"Elemento específico da vista atual: {RevitText.YesNo(sameView)}");
            issue.Details.Add($"ViewSpecific: {RevitText.YesNo(context.Element.ViewSpecific)}");

            issue.Severity = sameView ? IssueSeverity.Ok : IssueSeverity.Blocked;
            issue.Summary = sameView ? "Elemento pertence à vista analisada." : "Elemento pertence somente a outra vista.";
            return issue;
        }
    }
}
