using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class DesignOptionService : IVisibilityAnalyzer
    {
        public string Name => "Design Option";
        public int Priority => 12;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "DesignOption",
                Title = "Design Option",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            try
            {
                DesignOption option = context.Element.DesignOption;
                issue.Details.Add($"Elemento em Design Option: {option?.Name ?? "Modelo principal"}");
                Parameter viewConfig = context.View.get_Parameter(BuiltInParameter.VIEWER_OPTION_VISIBILITY);
                issue.Details.Add($"Configuração de Design Options da vista: {RevitText.ParameterValue(context.HostDocument, viewConfig)}");
                if (context.IsLinkedElement)
                    issue.Details.Add("Design Option do elemento vem do documento vinculado; configuração da vista vem do modelo host.");
                issue.Summary = option == null ? "Elemento no modelo principal." : "Elemento pertence a uma Design Option; confira a configuração da vista.";
                if (option != null)
                    issue.Severity = IssueSeverity.Warning;
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar Design Option.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }
    }
}
