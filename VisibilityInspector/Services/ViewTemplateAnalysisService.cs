using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class ViewTemplateAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Modelo de Vista";
        public int Priority => 13;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "ViewTemplate",
                Title = "Modelo de Vista",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Nenhum modelo de vista aplicado."
            };

            if (context.View.ViewTemplateId == ElementId.InvalidElementId)
            {
                issue.Details.Add("Modelo de vista aplicado: nenhum.");
                return issue;
            }

            RevitView template = context.Document.GetElement(context.View.ViewTemplateId) as RevitView;
            if (template == null)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "A vista aponta para um modelo de vista que não foi encontrado.";
                issue.Details.Add($"Modelo de vista ID: {RevitText.Id(context.View.ViewTemplateId)}");
                return issue;
            }

            issue.Summary = "A vista usa modelo de vista; configurações gráficas podem vir dele.";
            issue.Details.Add($"Modelo de vista aplicado: {template.Name} ({RevitText.Id(template.Id)})");
            List<string> directRisks = new List<string>();
            List<string> warnings = new List<string>();

            issue.Details.Add($"Categoria do elemento no modelo de vista: {CategoryVisibilityText(template, context.Element.Category, directRisks)}");
            AddTemplateSubcategories(template, context.Element.Category, issue, warnings);
            AddTemplateFilters(context, template, issue, directRisks, warnings);
            AddControlledParameters(context, template, issue);
            AddEffectiveGraphics(context, issue);

            if (directRisks.Count > 0)
            {
                issue.Severity = IssueSeverity.Blocked;
                issue.Summary = "O modelo de vista contém configuração que oculta este elemento.";
                issue.Details.Add("Bloqueios diretos: " + string.Join("; ", directRisks));
            }
            else if (warnings.Count > 0)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "O modelo de vista contém configurações suspeitas que podem afetar a geometria da família.";
                issue.Details.Add("Pontos suspeitos: " + string.Join("; ", warnings));
            }
            else
            {
                issue.Summary = "Modelo de vista aplicado, sem bloqueio direto detectado para este elemento.";
            }

            return issue;
        }

        private static string CategoryVisibilityText(RevitView template, Category category, List<string> directRisks)
        {
            if (category == null)
                return "sem categoria";

            try
            {
                bool hidden = template.GetCategoryHidden(category.Id);
                if (hidden)
                    directRisks.Add($"categoria {category.Name} oculta pelo modelo de vista");

                return hidden ? $"{category.Name}: oculta" : $"{category.Name}: ligada";
            }
            catch (Exception ex)
            {
                return $"{category.Name}: não foi possível avaliar ({ex.Message})";
            }
        }

        private static void AddTemplateSubcategories(RevitView template, Category category, VisibilityIssue issue, List<string> warnings)
        {
            if (category?.SubCategories == null || category.SubCategories.Size == 0)
            {
                issue.Details.Add("Subcategorias no modelo de vista: nenhuma subcategoria disponível.");
                return;
            }

            List<string> hidden = new List<string>();
            try
            {
                foreach (Category subcategory in category.SubCategories)
                {
                    if (template.GetCategoryHidden(subcategory.Id))
                        hidden.Add(subcategory.Name);
                }

                issue.Details.Add(hidden.Count == 0
                    ? "Subcategorias ocultas pelo modelo de vista: nenhuma."
                    : "Subcategorias ocultas pelo modelo de vista: " + string.Join(", ", hidden));
                if (hidden.Count > 0)
                    warnings.Add("subcategorias ocultas pelo modelo de vista: " + string.Join(", ", hidden));
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Subcategorias no modelo de vista: não foi possível avaliar ({ex.Message}).");
            }
        }

        private static void AddTemplateFilters(VisibilityAnalysisContext context, RevitView template, VisibilityIssue issue, List<string> directRisks, List<string> warnings)
        {
            try
            {
                ICollection<ElementId> filters = GetOrderedFilters(template);
                issue.Details.Add($"Filtros no modelo de vista: {filters.Count}");
                foreach (ElementId filterId in filters)
                {
                    ParameterFilterElement filter = context.Document.GetElement(filterId) as ParameterFilterElement;
                    if (filter == null)
                        continue;

                    bool affectsElement = false;
                    try
                    {
                        ICollection<ElementId> categories = filter.GetCategories();
                        ElementFilter elementFilter = filter.GetElementFilter();
                        affectsElement = context.Element.Category != null &&
                                         categories.Contains(context.Element.Category.Id) &&
                                         elementFilter != null &&
                                         elementFilter.PassesFilter(context.Element);
                    }
                    catch { }

                    bool hidden = false;
                    try { hidden = !template.GetFilterVisibility(filterId); }
                    catch { }

                    if (affectsElement || hidden)
                    {
                        issue.Details.Add($"Filtro do modelo: {filter.Name} - afeta elemento: {RevitText.YesNo(affectsElement)}, visibilidade: {(hidden ? "oculta" : "ligada")}");
                        if (affectsElement && hidden)
                            directRisks.Add($"filtro {filter.Name} oculta o elemento");
                        else if (hidden)
                            warnings.Add($"filtro {filter.Name} fica oculto no modelo, mas a regra não pegou este elemento");
                    }
                }
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Filtros no modelo de vista: não foi possível avaliar ({ex.Message}).");
            }
        }

        private static ICollection<ElementId> GetOrderedFilters(RevitView view)
        {
            try { return view.GetOrderedFilters(); }
            catch { return view.GetFilters(); }
        }

        private static void AddControlledParameters(VisibilityAnalysisContext context, RevitView template, VisibilityIssue issue)
        {
            try
            {
                ICollection<ElementId> ids = template.GetTemplateParameterIds();
                List<string> risky = ids
                    .Select(id => DescribeParameter(context.Document, template, id))
                    .Where(IsVisibilityRelated)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (risky.Count == 0)
                {
                    issue.Details.Add("Parâmetros de visibilidade controlados pelo modelo: nenhum identificado pela API.");
                    return;
                }

                issue.Details.Add("Parâmetros que o modelo de vista controla e podem afetar visibilidade: " + string.Join(", ", risky));
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Parâmetros controlados pelo modelo de vista: não foi possível avaliar ({ex.Message}).");
            }
        }

        private static string DescribeParameter(Document document, Element element, ElementId id)
        {
            try
            {
                if (id.IntegerValue < 0)
                {
                    BuiltInParameter builtIn = (BuiltInParameter)id.IntegerValue;
                    Parameter parameter = element.get_Parameter(builtIn);
                    string name = parameter?.Definition?.Name;
                    return string.IsNullOrWhiteSpace(name) ? builtIn.ToString() : name;
                }

                Element parameterElement = document.GetElement(id);
                return parameterElement?.Name ?? RevitText.Id(id);
            }
            catch
            {
                return RevitText.Id(id);
            }
        }

        private static bool IsVisibilityRelated(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.IndexOf("visibility", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("visibilidade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("graphics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("gráfico", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("grafico", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("filtro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("detail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("detalhe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("discipline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("disciplina", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("phase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("fase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("parts", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("categor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddEffectiveGraphics(VisibilityAnalysisContext context, VisibilityIssue issue)
        {
            try
            {
                Parameter viewPhase = context.View.get_Parameter(BuiltInParameter.VIEW_PHASE);
                Parameter phaseFilter = context.View.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER);
                issue.Details.Add($"Valores efetivos da vista - Detail Level: {context.View.DetailLevel}, Discipline: {context.View.Discipline}, Phase: {RevitText.ParameterValue(context.Document, viewPhase)}, Phase Filter: {RevitText.ParameterValue(context.Document, phaseFilter)}");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Valores efetivos da vista: não foi possível avaliar ({ex.Message}).");
            }
        }
    }
}
