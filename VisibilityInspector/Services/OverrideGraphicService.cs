using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class OverrideGraphicService : IVisibilityAnalyzer
    {
        public string Name => "Overrides";
        public int Priority => 15;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Overrides",
                Title = "Overrides",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Overrides gráficos coletados."
            };

            AddOverride(
                context,
                issue,
                context.IsLinkedElement ? "Instância do link" : "Elemento",
                RevitText.Id(context.HostVisibilityElement.Id),
                () => context.View.GetElementOverrides(context.HostVisibilityElement.Id));
            if (context.Element.Category != null)
                AddOverride(context, issue, "Categoria", context.Element.Category.Name, () => context.View.GetCategoryOverrides(context.Element.Category.Id));

            foreach (FilterResult filter in context.Report.Filters)
            {
                if (filter.Applied && filter.ElementPasses == true)
                    context.Report.Overrides.Add(new OverrideRecord
                    {
                        Source = "Filtro",
                        Target = filter.Name,
                        Summary = filter.Conclusion,
                        Color = filter.Color,
                        Weight = filter.Weight,
                        Transparency = filter.Transparency,
                        Halftone = filter.Halftone
                    });
            }

            try
            {
                if (context.View.ViewTemplateId != ElementId.InvalidElementId && context.HostDocument.GetElement(context.View.ViewTemplateId) is RevitView template)
                {
                    context.Report.Overrides.Add(new OverrideRecord
                    {
                        Source = "Template",
                        Target = template.Name,
                        Summary = "A vista usa template; parâmetros gráficos podem ser controlados pelo template."
                    });
                    issue.Details.Add($"View Template: {template.Name}");
                }
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Template: não foi possível avaliar ({ex.Message}).");
            }

            if (context.Report.Overrides.Count == 0)
                issue.Details.Add("Nenhum override explícito detectado.");
            else
                issue.Details.Add($"Overrides listados: {context.Report.Overrides.Count}");

            return issue;
        }

        private static void AddOverride(VisibilityAnalysisContext context, VisibilityIssue issue, string source, string target, Func<OverrideGraphicSettings> read)
        {
            try
            {
                OverrideGraphicSettings settings = read();
                if (!GraphicOverrideFormatter.HasAnyOverride(settings))
                    return;

                context.Report.Overrides.Add(new OverrideRecord
                {
                    Source = source,
                    Target = target,
                    Summary = GraphicOverrideFormatter.Summary(settings),
                    Color = GraphicOverrideFormatter.ColorText(settings.ProjectionLineColor),
                    Weight = settings.ProjectionLineWeight == OverrideGraphicSettings.InvalidPenNumber ? string.Empty : settings.ProjectionLineWeight.ToString(),
                    Transparency = settings.Transparency >= 0 ? settings.Transparency.ToString() : string.Empty,
                    Halftone = RevitText.YesNo(settings.Halftone)
                });
            }
            catch (Exception ex)
            {
                issue.Details.Add($"{source}: não foi possível ler override ({ex.Message}).");
            }
        }
    }
}
