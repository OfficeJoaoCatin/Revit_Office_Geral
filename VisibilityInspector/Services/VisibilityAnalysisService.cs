using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class VisibilityAnalysisService : IVisibilityAnalysisService
    {
        private readonly List<IVisibilityAnalyzer> _analyzers;

        public VisibilityAnalysisService()
        {
            _analyzers = new List<IVisibilityAnalyzer>
            {
                new TemporaryHideService(),
                new FilterAnalysisService(),
                new CategoryAnalysisService(),
                new WorksetAnalysisService(),
                new CropRegionService(),
                new SectionBoxService(),
                new ViewRangeService(),
                new PhaseAnalysisService(),
                new DisciplineAnalysisService(),
                new DetailLevelAnalysisService(),
                new DesignOptionService(),
                new ViewTemplateAnalysisService(),
                new LinkAnalysisService(),
                new OwnerViewAnalysisService(),
                new GeometryVisibilityAnalysisService(),
                new OverrideGraphicService()
            };
        }

        public VisibilityReport Analyze(UIApplication uiApp, Document document, RevitView view, Element element)
        {
            VisibilityReport report = CreateBaseReport(document, view, element, document, null);
            VisibilityAnalysisContext context = new VisibilityAnalysisContext(uiApp, document, view, element, report);

            AnalyzeIntoReport(context);
            return report;
        }

        public VisibilityReport AnalyzeLinkedElement(UIApplication uiApp, Document hostDocument, RevitView hostView, RevitLinkInstance linkInstance, Document linkedDocument, Element linkedElement)
        {
            VisibilityReport report = CreateBaseReport(linkedDocument, hostView, linkedElement, hostDocument, linkInstance);
            VisibilityAnalysisContext context = new VisibilityAnalysisContext(uiApp, hostDocument, hostView, linkedElement, report, linkedDocument, linkInstance);

            AnalyzeIntoReport(context);
            return report;
        }

        private void AnalyzeIntoReport(VisibilityAnalysisContext context)
        {
            VisibilityReport report = context.Report;

            foreach (IVisibilityAnalyzer analyzer in _analyzers.OrderBy(a => a.Priority))
            {
                try
                {
                    report.Issues.Add(analyzer.Analyze(context));
                }
                catch (Exception ex)
                {
                    report.Issues.Add(new VisibilityIssue
                    {
                        Key = analyzer.Name,
                        Title = analyzer.Name,
                        Priority = analyzer.Priority,
                        Severity = IssueSeverity.Warning,
                        Summary = "Análise falhou.",
                        Details = { ex.Message }
                    });
                }
            }

            AddParameters(context.ElementDocument, context.Element, report);
            BuildSummary(report);
            BuildFlow(report);
        }

        public VisibilityComparison CompareWithView(UIApplication uiApp, Document document, RevitView baseView, RevitView comparedView, Element element)
        {
            VisibilityReport baseReport = Analyze(uiApp, document, baseView, element);
            VisibilityReport comparedReport = Analyze(uiApp, document, comparedView, element);
            return BuildComparison(document, baseView, comparedView, baseReport, comparedReport);
        }

        public VisibilityComparison CompareLinkedWithView(UIApplication uiApp, Document hostDocument, RevitView baseView, RevitView comparedView, RevitLinkInstance linkInstance, Document linkedDocument, Element linkedElement)
        {
            VisibilityReport baseReport = AnalyzeLinkedElement(uiApp, hostDocument, baseView, linkInstance, linkedDocument, linkedElement);
            VisibilityReport comparedReport = AnalyzeLinkedElement(uiApp, hostDocument, comparedView, linkInstance, linkedDocument, linkedElement);
            return BuildComparison(hostDocument, baseView, comparedView, baseReport, comparedReport);
        }

        private VisibilityComparison BuildComparison(Document document, RevitView baseView, RevitView comparedView, VisibilityReport baseReport, VisibilityReport comparedReport)
        {
            VisibilityComparison comparison = new VisibilityComparison
            {
                BaseView = baseView.Name,
                ComparedView = comparedView.Name,
                BaseStatus = baseReport.OverallStatus,
                ComparedStatus = comparedReport.OverallStatus
            };

            AddViewSettingDifferences(document, baseView, comparedView, comparison);

            Dictionary<string, VisibilityIssue> baseIssues = baseReport.Issues.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
            foreach (VisibilityIssue compared in comparedReport.Issues)
            {
                if (!baseIssues.TryGetValue(compared.Key, out VisibilityIssue original))
                {
                    comparison.Differences.Add($"{compared.Title}: existe apenas em {comparedView.Name} ({compared.StatusText}).");
                    continue;
                }

                if (original.Severity != compared.Severity || !string.Equals(original.Summary, compared.Summary, StringComparison.OrdinalIgnoreCase))
                {
                    comparison.Differences.Add($"{compared.Title}: {baseView.Name} = {original.StatusText} ({original.Summary}) | {comparedView.Name} = {compared.StatusText} ({compared.Summary})");
                    continue;
                }

                if (IsImportantComparisonKey(compared.Key) && HasDifferentDetails(original, compared))
                {
                    comparison.Differences.Add($"{compared.Title}: detalhes diferentes. {BuildDetailDifference(baseView.Name, comparedView.Name, original, compared)}");
                }
            }

            if (comparison.Differences.Count == 0)
                comparison.Differences.Add("Nenhuma diferença relevante encontrada nas verificações automatizadas.");

            return comparison;
        }

        private static void AddViewSettingDifferences(Document document, RevitView baseView, RevitView comparedView, VisibilityComparison comparison)
        {
            AddDifferenceIfChanged(comparison, "Modelo de Vista", TemplateName(document, baseView), TemplateName(document, comparedView), baseView.Name, comparedView.Name);
            AddDifferenceIfChanged(comparison, "Detail Level", baseView.DetailLevel.ToString(), comparedView.DetailLevel.ToString(), baseView.Name, comparedView.Name);
            AddDifferenceIfChanged(comparison, "Discipline", baseView.Discipline.ToString(), comparedView.Discipline.ToString(), baseView.Name, comparedView.Name);
            AddDifferenceIfChanged(comparison, "Phase", ParameterText(document, baseView, BuiltInParameter.VIEW_PHASE), ParameterText(document, comparedView, BuiltInParameter.VIEW_PHASE), baseView.Name, comparedView.Name);
            AddDifferenceIfChanged(comparison, "Phase Filter", ParameterText(document, baseView, BuiltInParameter.VIEW_PHASE_FILTER), ParameterText(document, comparedView, BuiltInParameter.VIEW_PHASE_FILTER), baseView.Name, comparedView.Name);
        }

        private static void AddDifferenceIfChanged(VisibilityComparison comparison, string label, string baseValue, string comparedValue, string baseName, string comparedName)
        {
            if (string.Equals(baseValue, comparedValue, StringComparison.OrdinalIgnoreCase))
                return;

            comparison.Differences.Add($"{label}: {baseName} = {baseValue} | {comparedName} = {comparedValue}");
        }

        private static string TemplateName(Document document, RevitView view)
        {
            try
            {
                if (view.ViewTemplateId == ElementId.InvalidElementId)
                    return "nenhum";

                return document.GetElement(view.ViewTemplateId)?.Name ?? RevitText.Id(view.ViewTemplateId);
            }
            catch
            {
                return "não disponível";
            }
        }

        private static string ParameterText(Document document, RevitView view, BuiltInParameter builtInParameter)
        {
            try { return RevitText.ParameterValue(document, view.get_Parameter(builtInParameter)); }
            catch { return "não disponível"; }
        }

        private static bool IsImportantComparisonKey(string key)
        {
            return string.Equals(key, "Category", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "ViewTemplate", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "ViewFilter", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "GeometryVisibility", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "DetailLevel", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "Phase", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "Discipline", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "Crop", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "SectionBox", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasDifferentDetails(VisibilityIssue left, VisibilityIssue right)
        {
            return !string.Equals(string.Join("|", left.Details), string.Join("|", right.Details), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDetailDifference(string baseName, string comparedName, VisibilityIssue original, VisibilityIssue compared)
        {
            string baseOnly = original.Details.FirstOrDefault(detail => !compared.Details.Contains(detail, StringComparer.OrdinalIgnoreCase)) ?? "sem detalhe exclusivo";
            string comparedOnly = compared.Details.FirstOrDefault(detail => !original.Details.Contains(detail, StringComparer.OrdinalIgnoreCase)) ?? "sem detalhe exclusivo";
            return $"{baseName}: {baseOnly} | {comparedName}: {comparedOnly}";
        }

        public List<ViewVisibilitySummary> FindVisibleViews(UIApplication uiApp, Document document, Element element)
        {
            List<ViewVisibilitySummary> result = new List<ViewVisibilitySummary>();
            List<RevitView> views = GetInspectableViews(document);

            foreach (RevitView view in views)
            {
                try
                {
                    VisibilityReport report = Analyze(uiApp, document, view, element);
                    result.Add(new ViewVisibilitySummary
                    {
                        ViewName = view.Name,
                        ViewType = view.ViewType.ToString(),
                        ViewId = RevitText.Id(view.Id),
                        Status = report.OverallStatus,
                        Reason = BuildViewSearchReason(report)
                    });
                }
                catch (Exception ex)
                {
                    result.Add(new ViewVisibilitySummary
                    {
                        ViewName = view.Name,
                        ViewType = view.ViewType.ToString(),
                        ViewId = RevitText.Id(view.Id),
                        Status = "Indefinido",
                        Reason = ex.Message
                    });
                }
            }

            return result
                .OrderBy(item => StatusRank(item.Status))
                .ThenBy(item => item.ViewName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<ViewVisibilitySummary> FindVisibleViewsForLinkedElement(UIApplication uiApp, Document hostDocument, RevitLinkInstance linkInstance, Document linkedDocument, Element linkedElement)
        {
            List<ViewVisibilitySummary> result = new List<ViewVisibilitySummary>();
            List<RevitView> views = GetInspectableViews(hostDocument);

            foreach (RevitView view in views)
            {
                try
                {
                    VisibilityReport report = AnalyzeLinkedElement(uiApp, hostDocument, view, linkInstance, linkedDocument, linkedElement);
                    result.Add(new ViewVisibilitySummary
                    {
                        ViewName = view.Name,
                        ViewType = view.ViewType.ToString(),
                        ViewId = RevitText.Id(view.Id),
                        Status = report.OverallStatus,
                        Reason = BuildViewSearchReason(report)
                    });
                }
                catch (Exception ex)
                {
                    result.Add(new ViewVisibilitySummary
                    {
                        ViewName = view.Name,
                        ViewType = view.ViewType.ToString(),
                        ViewId = RevitText.Id(view.Id),
                        Status = "Indefinido",
                        Reason = ex.Message
                    });
                }
            }

            return result
                .OrderBy(item => StatusRank(item.Status))
                .ThenBy(item => item.ViewName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildViewSearchReason(VisibilityReport report)
        {
            VisibilityIssue issue = report.Problems.FirstOrDefault();
            if (issue == null)
                return "Sem bloqueio detectado";

            return string.IsNullOrWhiteSpace(issue.Summary)
                ? issue.Title
                : $"{issue.Title}: {issue.Summary}";
        }

        private static int StatusRank(string status)
        {
            if (string.Equals(status, "VISÍVEL", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(status, "ATENÇÃO", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(status, "OCULTO", StringComparison.OrdinalIgnoreCase))
                return 2;
            return 3;
        }

        public List<ViewOption> GetInspectableViewOptions(Document document)
        {
            return GetInspectableViews(document)
                .OrderBy(view => view.Name, StringComparer.OrdinalIgnoreCase)
                .Select(view => new ViewOption
                {
                    Id = view.Id,
                    Name = view.Name,
                    ViewType = view.ViewType.ToString()
                })
                .ToList();
        }

        private static List<RevitView> GetInspectableViews(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(RevitView))
                .Cast<RevitView>()
                .Where(view => !view.IsTemplate)
                .Where(view => view.ViewType != ViewType.ProjectBrowser &&
                               view.ViewType != ViewType.SystemBrowser &&
                               view.ViewType != ViewType.Internal &&
                               view.ViewType != ViewType.Undefined)
                .ToList();
        }

        private static VisibilityReport CreateBaseReport(Document elementDocument, RevitView view, Element element, Document hostDocument, RevitLinkInstance linkInstance)
        {
            return new VisibilityReport
            {
                ElementId = RevitText.Id(element.Id),
                ElementUniqueId = element.UniqueId ?? string.Empty,
                ElementName = RevitText.ElementName(element),
                ElementSource = linkInstance == null ? "Modelo ativo" : "Modelo vinculado",
                IsLinkedElement = linkInstance != null,
                HostDocumentTitle = hostDocument?.Title ?? string.Empty,
                LinkedDocumentTitle = linkInstance == null ? string.Empty : elementDocument?.Title ?? string.Empty,
                LinkInstanceName = linkInstance == null ? string.Empty : RevitText.ElementName(linkInstance),
                LinkInstanceId = linkInstance == null ? string.Empty : RevitText.Id(linkInstance.Id),
                Category = element.Category?.Name ?? "Sem categoria",
                Family = RevitText.FamilyName(elementDocument, element),
                Type = RevitText.ElementTypeName(elementDocument, element),
                ViewName = view.Name,
                ViewId = RevitText.Id(view.Id)
            };
        }

        private static void AddParameters(Document document, Element element, VisibilityReport report)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                string name = parameter?.Definition?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                report.Parameters.Add(new ParameterSnapshot
                {
                    Name = name,
                    Value = RevitText.ParameterValue(document, parameter),
                    StorageType = parameter.StorageType.ToString(),
                    ReadOnly = parameter.IsReadOnly
                });
            }

            report.Parameters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void BuildSummary(VisibilityReport report)
        {
            List<VisibilityIssue> blocking = report.Issues
                .Where(issue => issue.Severity == IssueSeverity.Blocked)
                .OrderBy(issue => issue.Priority)
                .ToList();

            if (blocking.Count > 0)
            {
                report.OverallStatus = "OCULTO";
                report.Summary = "Elemento invisível ou potencialmente invisível. Motivos: " +
                                 string.Join("; ", blocking.Select(issue => issue.Title));
                return;
            }

            List<VisibilityIssue> warnings = report.Issues
                .Where(issue => issue.Severity == IssueSeverity.Warning)
                .OrderBy(issue => issue.Priority)
                .ToList();

            if (warnings.Count > 0)
            {
                report.OverallStatus = "ATENÇÃO";
                report.Summary = "Nenhum bloqueio definitivo detectado, mas há pontos que podem afetar a visibilidade: " +
                                 string.Join("; ", warnings.Select(issue => issue.Title));
            }
            else
            {
                report.OverallStatus = "VISÍVEL";
                report.Summary = "Nenhum bloqueio de visibilidade detectado pelas verificações automatizadas.";
            }
        }

        private static void BuildFlow(VisibilityReport report)
        {
            string[] keys =
            {
                "Category",
                "Links",
                "Workset",
                "Phase",
                "ViewTemplate",
                "ViewFilter",
                "Overrides",
                "Crop",
                "ViewRange",
                "SectionBox",
                "GeometryVisibility"
            };

            foreach (string key in keys)
            {
                VisibilityIssue issue = report.Issues.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
                if (issue != null)
                    report.Flow.Add(issue);
            }
        }
    }
}
