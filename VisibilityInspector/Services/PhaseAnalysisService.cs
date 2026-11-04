using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class PhaseAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Phase";
        public int Priority => 9;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Phase",
                Title = "Phase",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            try
            {
                Element created = context.ElementDocument.GetElement(context.Element.CreatedPhaseId);
                Element demolished = context.ElementDocument.GetElement(context.Element.DemolishedPhaseId);
                Parameter viewPhase = context.View.get_Parameter(BuiltInParameter.VIEW_PHASE);
                Parameter phaseFilter = context.View.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER);

                issue.Details.Add($"Phase Created: {created?.Name ?? "N/A"}");
                issue.Details.Add($"Phase Demolished: {demolished?.Name ?? "N/A"}");
                issue.Details.Add($"View Phase: {RevitText.ParameterValue(context.HostDocument, viewPhase)}");
                issue.Details.Add($"Phase Filter: {RevitText.ParameterValue(context.HostDocument, phaseFilter)}");
                if (context.IsLinkedElement)
                    issue.Details.Add("Fases do elemento vêm do documento vinculado; fase/filtro da vista vêm do modelo host.");

                issue.Summary = "Dados de fase coletados. Validação visual exata depende da matriz interna do Phase Filter.";
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar fases.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }
    }
}
