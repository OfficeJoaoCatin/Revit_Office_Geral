using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class WorksetAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Workset";
        public int Priority => 5;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Workset",
                Title = "Workset",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            if (!context.HostDocument.IsWorkshared && !context.ElementDocument.IsWorkshared)
            {
                issue.Summary = "Projeto não é workshared.";
                issue.Details.Add("Worksets não se aplicam a este documento.");
                return issue;
            }

            try
            {
                Element hostElement = context.HostVisibilityElement;
                WorksetId hostWorksetId = hostElement.WorksetId;
                Workset workset = context.HostDocument.IsWorkshared ? context.HostDocument.GetWorksetTable().GetWorkset(hostWorksetId) : null;
                WorksetVisibility viewVisibility = context.HostDocument.IsWorkshared ? context.View.GetWorksetVisibility(hostWorksetId) : WorksetVisibility.UseGlobalSetting;
                issue.Details.Add(context.IsLinkedElement
                    ? $"Workset da instância do link: {workset?.Name ?? hostWorksetId.ToString()}"
                    : $"Workset do elemento: {workset?.Name ?? hostWorksetId.ToString()}");
                issue.Details.Add($"Workset aberto: {RevitText.YesNo(workset != null && workset.IsOpen)}");
                issue.Details.Add($"Configuração na vista: {viewVisibility}");
                AddLinkedElementWorkset(context, issue);

                if (workset != null && !workset.IsOpen)
                {
                    issue.Severity = IssueSeverity.Blocked;
                    issue.Summary = context.IsLinkedElement ? "Workset da instância do link está fechado." : "Workset do elemento está fechado.";
                }
                else if (viewVisibility == WorksetVisibility.Hidden)
                {
                    issue.Severity = IssueSeverity.Blocked;
                    issue.Summary = context.IsLinkedElement ? "Workset da instância do link oculto na vista." : "Workset oculto na vista.";
                }
                else
                {
                    issue.Summary = "Workset aberto e não oculto explicitamente na vista.";
                }
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar workset.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }

        private static void AddLinkedElementWorkset(VisibilityAnalysisContext context, VisibilityIssue issue)
        {
            if (!context.IsLinkedElement || !context.ElementDocument.IsWorkshared)
                return;

            try
            {
                Workset linkedWorkset = context.ElementDocument.GetWorksetTable().GetWorkset(context.Element.WorksetId);
                issue.Details.Add($"Workset do elemento dentro do link: {linkedWorkset?.Name ?? context.Element.WorksetId.ToString()}");
                issue.Details.Add($"Workset do link aberto: {RevitText.YesNo(linkedWorkset != null && linkedWorkset.IsOpen)}");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Workset do elemento no link: não foi possível avaliar ({ex.Message}).");
            }
        }
    }
}
