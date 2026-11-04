using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class FilterAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "View Filters";
        public int Priority => 3;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "ViewFilter",
                Title = "View Filters",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Nenhum filtro aplicado oculta o elemento."
            };

            ICollection<ElementId> ordered = GetOrderedFilters(context.View);
            HashSet<int> appliedIds = new HashSet<int>(ordered.Select(id => id.IntegerValue));
            List<ParameterFilterElement> allFilters = new FilteredElementCollector(context.Document)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .OrderBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int appliedPriority = 0;
            foreach (ElementId filterId in ordered)
            {
                appliedPriority++;
                ParameterFilterElement filter = context.Document.GetElement(filterId) as ParameterFilterElement;
                if (filter == null)
                    continue;

                FilterResult result = AnalyzeFilter(context, filter, true, appliedPriority);
                context.Report.Filters.Add(result);
                if (result.Hidden && result.ElementPasses == true)
                {
                    issue.Severity = IssueSeverity.Blocked;
                    issue.Summary = $"Elemento ocultado pelo filtro {filter.Name}.";
                    issue.Details.Add($"Filtro {filter.Name}: oculta o elemento.");
                }
            }

            foreach (ParameterFilterElement filter in allFilters.Where(filter => !appliedIds.Contains(filter.Id.IntegerValue)))
                context.Report.Filters.Add(AnalyzeFilter(context, filter, false, 0));

            if (issue.Details.Count == 0)
                issue.Details.Add($"Filtros aplicados na vista: {ordered.Count}");

            return issue;
        }

        private static ICollection<ElementId> GetOrderedFilters(RevitView view)
        {
            try { return view.GetOrderedFilters(); }
            catch { return view.GetFilters(); }
        }

        private static FilterResult AnalyzeFilter(VisibilityAnalysisContext context, ParameterFilterElement filter, bool applied, int priority)
        {
            FilterResult result = new FilterResult
            {
                Name = filter.Name,
                Id = RevitText.Id(filter.Id),
                Priority = priority,
                Applied = applied
            };

            try
            {
                ICollection<ElementId> categories = filter.GetCategories();
                bool categoryMatches = context.Element.Category != null && categories.Contains(context.Element.Category.Id);
                result.Rules.Add(new VisibilityRule
                {
                    Name = "Categoria atendida pelo filtro",
                    Expected = string.Join(", ", categories.Select(RevitText.Id)),
                    Actual = context.Element.Category?.Name ?? "Sem categoria",
                    Passed = categoryMatches
                });

                ElementFilter elementFilter = filter.GetElementFilter();
                result.ElementPasses = categoryMatches && elementFilter != null && elementFilter.PassesFilter(context.Element);
                result.Rules.Add(new VisibilityRule
                {
                    Name = "Regras internas do filtro",
                    Expected = elementFilter?.GetType().Name ?? "Sem regra",
                    Actual = "Avaliado pela API ElementFilter.PassesFilter",
                    Passed = result.ElementPasses
                });
            }
            catch (Exception ex)
            {
                result.ElementPasses = null;
                result.Rules.Add(new VisibilityRule
                {
                    Name = "Erro ao avaliar regras",
                    Actual = ex.Message,
                    Passed = null
                });
            }

            if (applied)
            {
                try
                {
                    result.Hidden = !context.View.GetFilterVisibility(filter.Id);
                    OverrideGraphicSettings overrides = context.View.GetFilterOverrides(filter.Id);
                    result.Color = GraphicOverrideFormatter.ColorText(overrides.ProjectionLineColor);
                    result.Weight = overrides.ProjectionLineWeight == OverrideGraphicSettings.InvalidPenNumber ? string.Empty : overrides.ProjectionLineWeight.ToString();
                    result.Transparency = overrides.Transparency >= 0 ? overrides.Transparency.ToString() : string.Empty;
                    result.Halftone = RevitText.YesNo(overrides.Halftone);
                    result.Pattern = RevitText.Id(overrides.SurfaceForegroundPatternId);
                    result.Conclusion = result.Hidden && result.ElementPasses == true
                        ? "Elemento ocultado por este filtro."
                        : result.ElementPasses == true
                            ? "Filtro aplicado ao elemento."
                            : "Filtro aplicado na vista, mas o elemento não atende.";
                }
                catch (Exception ex)
                {
                    result.Conclusion = $"Filtro aplicado, mas overrides não puderam ser lidos: {ex.Message}";
                }
            }
            else
            {
                result.Conclusion = result.ElementPasses == true
                    ? "Filtro existe no projeto e afetaria este elemento se fosse aplicado."
                    : "Filtro existe no projeto, mas não afeta este elemento.";
            }

            return result;
        }
    }
}
