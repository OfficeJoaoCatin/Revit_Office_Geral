using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class TemporaryHideService : IVisibilityAnalyzer
    {
        public string Name => "Hide in View";
        public int Priority => 1;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "HideInView",
                Title = "Hide in View",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            bool elementHidden = false;
            try
            {
                elementHidden = context.HostVisibilityElement.IsHidden(context.View);
                issue.Details.Add(context.IsLinkedElement
                    ? $"Instância do link ocultada manualmente: {RevitText.YesNo(elementHidden)}"
                    : $"Elemento oculto manualmente: {RevitText.YesNo(elementHidden)}");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Elemento oculto manualmente: não foi possível avaliar ({ex.Message}).");
            }

            bool categoryHidden = false;
            bool linkCategoryHidden = false;
            try
            {
                if (context.Element.Category != null)
                    categoryHidden = context.View.GetCategoryHidden(context.Element.Category.Id);
                issue.Details.Add($"Categoria oculta na vista: {RevitText.YesNo(categoryHidden)}");
                if (context.IsLinkedElement && context.LinkInstance.Category != null)
                {
                    linkCategoryHidden = context.View.GetCategoryHidden(context.LinkInstance.Category.Id);
                    issue.Details.Add($"Categoria da instância do link oculta na vista: {RevitText.YesNo(linkCategoryHidden)}");
                }
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Categoria oculta na vista: não foi possível avaliar ({ex.Message}).");
            }

            bool temporaryHide = false;
            try
            {
                temporaryHide = context.View.IsTemporaryHideIsolateActive();
                issue.Details.Add($"Temporary Hide/Isolate ativo: {RevitText.YesNo(temporaryHide)}");
                issue.Details.Add($"Modo TemporaryHideIsolate: {RevitText.YesNo(context.View.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))}");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Temporary Hide/Isolate: não foi possível avaliar ({ex.Message}).");
            }

            try
            {
                issue.Details.Add($"Reveal Hidden ativo: {RevitText.YesNo(context.View.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements))}");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Reveal Hidden: não foi possível avaliar ({ex.Message}).");
            }

            if (elementHidden)
            {
                issue.Severity = IssueSeverity.Blocked;
                issue.Summary = context.IsLinkedElement ? "Instância do link oculta manualmente nesta vista." : "Elemento oculto manualmente nesta vista.";
            }
            else if (temporaryHide || categoryHidden || linkCategoryHidden)
            {
                issue.Severity = categoryHidden || linkCategoryHidden ? IssueSeverity.Blocked : IssueSeverity.Warning;
                issue.Summary = categoryHidden
                    ? "Categoria oculta nesta vista."
                    : linkCategoryHidden
                        ? "Categoria da instância do link oculta nesta vista."
                        : "Temporary Hide/Isolate ativo na vista.";
            }
            else
            {
                issue.Summary = "Sem ocultação manual detectada para o elemento.";
            }

            return issue;
        }
    }
}
