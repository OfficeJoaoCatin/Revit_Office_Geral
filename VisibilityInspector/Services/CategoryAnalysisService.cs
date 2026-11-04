using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class CategoryAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Categoria";
        public int Priority => 4;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = NewIssue();
            Category category = context.Element.Category;
            if (category == null)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Elemento sem categoria.";
                return issue;
            }

            bool hidden = IsCategoryHidden(context.View, category.Id);
            issue.Details.Add($"Categoria: {category.Name}");
            issue.Details.Add($"Categoria ligada na vista: {RevitText.YesNo(!hidden)}");
            issue.Details.Add($"Tipo de categoria: {category.CategoryType}");
            issue.Details.Add($"Categoria importada: {RevitText.YesNo(IsImportedCategory(category))}");

            List<string> hiddenSubcategories = AddSubcategoryDetails(context.View, category, issue);

            try
            {
                OverrideGraphicSettings overrides = context.View.GetCategoryOverrides(category.Id);
                if (GraphicOverrideFormatter.HasAnyOverride(overrides))
                    issue.Details.Add($"Override por categoria: {GraphicOverrideFormatter.Summary(overrides)}");
                else
                    issue.Details.Add("Override por categoria: nenhum override explícito.");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Override por categoria: não disponível ({ex.Message}).");
            }

            if (hidden)
            {
                issue.Severity = IssueSeverity.Blocked;
                issue.Summary = "Categoria principal oculta na vista.";
            }
            else if (hiddenSubcategories.Count > 0)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Existem subcategorias ocultas na vista/modelo de vista que podem esconder a geometria da família.";
            }
            else
            {
                issue.Severity = IssueSeverity.Ok;
                issue.Summary = "Categoria e subcategorias aparentam estar ligadas.";
            }

            return issue;
        }

        private static VisibilityIssue NewIssue()
        {
            return new VisibilityIssue
            {
                Key = "Category",
                Title = "Categoria",
                Priority = 4
            };
        }

        private static bool IsCategoryHidden(RevitView view, ElementId categoryId)
        {
            try { return view.GetCategoryHidden(categoryId); }
            catch { return false; }
        }

        private static List<string> AddSubcategoryDetails(RevitView view, Category category, VisibilityIssue issue)
        {
            List<string> hiddenSubcategories = new List<string>();

            try
            {
                issue.Details.Add($"Subcategorias disponíveis: {category.SubCategories?.Size ?? 0}");
                if (category.SubCategories == null || category.SubCategories.Size == 0)
                {
                    issue.Details.Add("Subcategorias ocultas na vista: nenhuma subcategoria disponível.");
                    return hiddenSubcategories;
                }

                foreach (Category subcategory in category.SubCategories)
                {
                    bool subcategoryHidden = IsCategoryHidden(view, subcategory.Id);
                    if (subcategoryHidden)
                        hiddenSubcategories.Add(subcategory.Name);
                }

                issue.Details.Add(hiddenSubcategories.Count == 0
                    ? "Subcategorias ocultas na vista: nenhuma."
                    : "Subcategorias ocultas na vista: " + string.Join(", ", hiddenSubcategories));
                issue.Details.Add("Se a geometria da família estiver desenhada em uma dessas subcategorias, o elemento pode continuar selecionável, mas sem sólido visível.");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Subcategoria: não foi possível analisar ({ex.Message}).");
            }

            return hiddenSubcategories;
        }

        private static bool IsImportedCategory(Category category)
        {
            if (category == null)
                return false;

            return (category.Name ?? string.Empty).IndexOf("Imports", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (category.Name ?? string.Empty).IndexOf("Import", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (category.Name ?? string.Empty).IndexOf("DWG", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
