using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class LinkAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Links";
        public int Priority => 2;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "Links",
                Title = "Links",
                Priority = Priority,
                Severity = IssueSeverity.Ok
            };

            RevitLinkInstance link = context.IsLinkedElement ? context.LinkInstance : context.Element as RevitLinkInstance;
            if (link == null)
            {
                issue.Summary = "Elemento analisado não é uma instância de link.";
                issue.Details.Add("Elemento do modelo ativo; análise de link não se aplica.");
                return issue;
            }

            try
            {
                Document linkedDocument = link.GetLinkDocument();
                bool hidden = link.IsHidden(context.View);
                issue.Details.Add($"Link carregado: {RevitText.YesNo(linkedDocument != null)}");
                issue.Details.Add($"Instância do link: {RevitText.ElementName(link)} ({RevitText.Id(link.Id)})");
                if (context.IsLinkedElement)
                {
                    issue.Details.Add($"Documento vinculado: {linkedDocument?.Title ?? "N/A"}");
                    issue.Details.Add($"Elemento interno selecionado: {RevitText.Id(context.Element.Id)} - {RevitText.ElementName(context.Element)}");
                    issue.Details.Add($"Categoria do elemento interno: {context.Element.Category?.Name ?? "Sem categoria"}");
                }
                issue.Details.Add($"Instância do link oculta na vista: {RevitText.YesNo(hidden)}");
                issue.Details.Add($"Categoria do link oculta: {RevitText.YesNo(link.Category != null && context.View.GetCategoryHidden(link.Category.Id))}");
                issue.Severity = linkedDocument == null || hidden ? IssueSeverity.Blocked : IssueSeverity.Ok;
                issue.Summary = linkedDocument == null
                    ? "Link descarregado."
                    : hidden
                        ? "Instância do link oculta."
                        : context.IsLinkedElement
                            ? "Link carregado; elemento interno capturado para análise."
                            : "Link carregado e instância visível.";
            }
            catch (Exception ex)
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "Não foi possível avaliar link.";
                issue.Details.Add(ex.Message);
            }

            return issue;
        }
    }
}
