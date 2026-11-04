using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitFamilyImporter.Utils;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Services;
using RevitView = Autodesk.Revit.DB.View;
using WpfApplication = System.Windows.Application;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfMessageBox = System.Windows.MessageBox;
using WpfPrintDialog = System.Windows.Controls.PrintDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfSize = System.Windows.Size;

namespace RevitFamilyImporter.VisibilityInspector.ViewModels
{
    public class VisibilityInspectorVM : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;
        private readonly Element _element;
        private readonly Document _elementDocument;
        private readonly RevitLinkInstance _linkInstance;
        private readonly IVisibilityAnalysisService _analysisService;
        private readonly ReportExportService _exportService = new ReportExportService();
        private VisibilityReport _report;
        private string _filterSearch = string.Empty;
        private bool _isBusy;
        private string _statusText = "Pronto";
        private ViewVisibilitySummary? _selectedViewResult;

        public VisibilityInspectorVM(UIApplication uiApp, Element element, VisibilityReport report, IVisibilityAnalysisService analysisService, RevitLinkInstance linkInstance = null, Document elementDocument = null)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _doc = uiApp.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Documento ativo não encontrado.");
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _elementDocument = elementDocument ?? element.Document;
            _linkInstance = linkInstance;
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));

            RefreshCollections();
            ExportCommand = new RelayCommand(_ => Export());
            ResolveProblemsCommand = new RelayCommand(_ => ResolveProblems());
            PrintPdfCommand = new RelayCommand(PrintPdf);
            CompareViewCommand = new RelayCommand(_ => CompareView());
            FindViewsCommand = new RelayCommand(_ => FindViews());
            OpenViewCommand = new RelayCommand(OpenView, parameter => parameter is ViewVisibilitySummary);
        }

        public VisibilityReport Report
        {
            get => _report;
            private set
            {
                if (SetProperty(ref _report, value))
                    RefreshCollections();
            }
        }

        public ObservableCollection<VisibilityIssue> Issues { get; } = new ObservableCollection<VisibilityIssue>();
        public ObservableCollection<FilterResult> Filters { get; } = new ObservableCollection<FilterResult>();
        public ObservableCollection<OverrideRecord> Overrides { get; } = new ObservableCollection<OverrideRecord>();
        public ObservableCollection<ParameterSnapshot> Parameters { get; } = new ObservableCollection<ParameterSnapshot>();
        public ObservableCollection<VisibilityIssue> Flow { get; } = new ObservableCollection<VisibilityIssue>();
        public ObservableCollection<ViewVisibilitySummary> ViewResults { get; } = new ObservableCollection<ViewVisibilitySummary>();
        public ObservableCollection<string> ComparisonDifferences { get; } = new ObservableCollection<string>();

        public ViewVisibilitySummary? SelectedViewResult
        {
            get => _selectedViewResult;
            set => SetProperty(ref _selectedViewResult, value);
        }

        public string FilterSearch
        {
            get => _filterSearch;
            set
            {
                if (SetProperty(ref _filterSearch, value ?? string.Empty))
                    RefreshFilters();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value ?? string.Empty);
        }

        public ICommand ExportCommand { get; }
        public ICommand ResolveProblemsCommand { get; }
        public ICommand PrintPdfCommand { get; }
        public ICommand CompareViewCommand { get; }
        public ICommand FindViewsCommand { get; }
        public ICommand OpenViewCommand { get; }

        private void ResolveProblems()
        {
            try
            {
                RevitView view = ResolveReportView();
                bool shouldOfferTemplateFix = ShouldOfferTemplateFix(view);

                MessageBoxResult confirmation = WpfMessageBox.Show(
                    "A ferramenta vai tentar corrigir automaticamente causas simples na vista analisada: reexibir elemento/categoria/subcategorias, desligar Temporary Hide/Isolate, ligar filtros que ocultam o elemento e reexibir workset oculto." +
                    (shouldOfferTemplateFix ? "\n\nA vista usa modelo de vista. Se a correção precisar alterar o modelo de vista, a ferramenta pedirá uma confirmação separada porque isso pode afetar outras vistas." : string.Empty) +
                    "\n\nEssas ações alteram a configuração da vista. Deseja continuar?",
                    "Visibility Inspector - Resolver problemas",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                    return;

                bool allowTemplateChanges = shouldOfferTemplateFix && ConfirmTemplateChanges(view);
                int fixedCount = 0;
                System.Collections.Generic.List<string> unresolved = new System.Collections.Generic.List<string>();

                using (Transaction transaction = new Transaction(_doc, "Visibility Inspector - Resolver visibilidade"))
                {
                    transaction.Start();

                    fixedCount += TryDisableTemporaryHide(view, unresolved);
                    fixedCount += TryUnhideElement(view, unresolved);
                    fixedCount += TryUnhideCategory(view, unresolved);
                    fixedCount += TryUnhideTemplateCategory(view, unresolved, allowTemplateChanges);
                    fixedCount += TryUnhideSubcategories(view, unresolved);
                    fixedCount += TryUnhideTemplateSubcategories(view, unresolved, allowTemplateChanges);
                    fixedCount += TryEnableHidingFilters(view, unresolved, allowTemplateChanges);
                    fixedCount += TryShowWorkset(view, unresolved);
                    AddGeometryVisibilityGuidance(unresolved);

                    transaction.Commit();
                }

                Report = AnalyzeCurrentTarget(view);
                StatusText = fixedCount == 0
                    ? "Nenhuma correção automática segura foi aplicada."
                    : $"{fixedCount} correção(ões) aplicada(s). Relatório reanalisado.";

                if (unresolved.Count > 0)
                {
                    WpfMessageBox.Show(
                        string.Join(Environment.NewLine, unresolved),
                        "Visibility Inspector - Correções não automáticas",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private RevitView ResolveReportView()
        {
            if (int.TryParse(Report.ViewId, out int viewId) && _doc.GetElement(new ElementId(viewId)) is RevitView reportView)
                return reportView;

            return _uiApp.ActiveUIDocument.ActiveView;
        }

        private bool ShouldOfferTemplateFix(RevitView view)
        {
            if (view.ViewTemplateId == ElementId.InvalidElementId)
                return false;

            return Report.Issues.Any(issue =>
                (string.Equals(issue.Key, "ViewTemplate", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Key, "Category", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Key, "ViewFilter", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Key, "GeometryVisibility", StringComparison.OrdinalIgnoreCase)) &&
                issue.Severity != IssueSeverity.Ok);
        }

        private bool ConfirmTemplateChanges(RevitView view)
        {
            string templateName = "modelo de vista";
            try
            {
                if (_doc.GetElement(view.ViewTemplateId) is RevitView template)
                    templateName = template.Name;
            }
            catch { }

            MessageBoxResult result = WpfMessageBox.Show(
                $"A vista usa o modelo de vista \"{templateName}\".\n\nPermitir que a ferramenta altere categoria/subcategorias/filtros nesse modelo de vista? Isso pode afetar outras vistas que usam o mesmo modelo.",
                "Visibility Inspector - Modelo de Vista",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes;
        }

        private int TryDisableTemporaryHide(RevitView view, System.Collections.Generic.ICollection<string> unresolved)
        {
            try
            {
                if (!view.IsTemporaryHideIsolateActive())
                    return 0;

                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                return 1;
            }
            catch (Exception ex)
            {
                unresolved.Add($"Temporary Hide/Isolate não foi desativado: {ex.Message}");
                return 0;
            }
        }

        private int TryUnhideElement(RevitView view, System.Collections.Generic.ICollection<string> unresolved)
        {
            try
            {
                Element hostElement = HostVisibilityElement;
                if (!hostElement.IsHidden(view))
                    return 0;

                view.UnhideElements(new[] { hostElement.Id });
                return 1;
            }
            catch (Exception ex)
            {
                unresolved.Add(_linkInstance != null
                    ? $"Instância do link não foi reexibida manualmente: {ex.Message}"
                    : $"Elemento não foi reexibido manualmente: {ex.Message}");
                return 0;
            }
        }

        private int TryUnhideCategory(RevitView view, System.Collections.Generic.ICollection<string> unresolved)
        {
            try
            {
                int fixedCount = 0;
                if (_element.Category != null && view.GetCategoryHidden(_element.Category.Id))
                {
                    view.SetCategoryHidden(_element.Category.Id, false);
                    fixedCount++;
                }

                if (_linkInstance?.Category != null && view.GetCategoryHidden(_linkInstance.Category.Id))
                {
                    view.SetCategoryHidden(_linkInstance.Category.Id, false);
                    fixedCount++;
                }

                return fixedCount;
            }
            catch (Exception ex)
            {
                unresolved.Add($"Categoria não foi reexibida: {ex.Message}");
                return 0;
            }
        }

        private int TryUnhideTemplateCategory(RevitView view, System.Collections.Generic.ICollection<string> unresolved, bool allowTemplateChanges)
        {
            try
            {
                if (view.ViewTemplateId == ElementId.InvalidElementId)
                    return 0;

                if (!(_doc.GetElement(view.ViewTemplateId) is RevitView template))
                    return 0;

                bool elementCategoryHidden = _element.Category != null && template.GetCategoryHidden(_element.Category.Id);
                bool linkCategoryHidden = _linkInstance?.Category != null && template.GetCategoryHidden(_linkInstance.Category.Id);
                if (!elementCategoryHidden && !linkCategoryHidden)
                    return 0;

                if (!allowTemplateChanges)
                {
                    unresolved.Add($"Categoria do alvo está oculta no modelo de vista {template.Name}, mas o modelo de vista não foi alterado.");
                    return 0;
                }

                int fixedCount = 0;
                if (elementCategoryHidden)
                {
                    template.SetCategoryHidden(_element.Category.Id, false);
                    fixedCount++;
                }

                if (linkCategoryHidden)
                {
                    template.SetCategoryHidden(_linkInstance.Category.Id, false);
                    fixedCount++;
                }

                return fixedCount;
            }
            catch (Exception ex)
            {
                unresolved.Add($"Categoria no modelo de vista não foi reexibida: {ex.Message}");
                return 0;
            }
        }

        private int TryUnhideSubcategories(RevitView view, System.Collections.Generic.ICollection<string> unresolved)
        {
            return TryUnhideSubcategoriesOnView(view, "vista analisada", unresolved);
        }

        private int TryUnhideTemplateSubcategories(RevitView view, System.Collections.Generic.ICollection<string> unresolved, bool allowTemplateChanges)
        {
            try
            {
                if (view.ViewTemplateId == ElementId.InvalidElementId)
                    return 0;

                if (!(_doc.GetElement(view.ViewTemplateId) is RevitView template))
                    return 0;

                if (!allowTemplateChanges)
                {
                    System.Collections.Generic.List<string> hidden = HiddenSubcategoryNames(template);
                    if (hidden.Count > 0)
                        unresolved.Add($"Subcategorias ocultas no modelo de vista {template.Name}: {string.Join(", ", hidden)}. O modelo de vista não foi alterado.");
                    return 0;
                }

                return TryUnhideSubcategoriesOnView(template, $"modelo de vista {template.Name}", unresolved);
            }
            catch (Exception ex)
            {
                unresolved.Add($"Subcategorias no modelo de vista não foram reexibidas: {ex.Message}");
                return 0;
            }
        }

        private int TryUnhideSubcategoriesOnView(RevitView targetView, string targetName, System.Collections.Generic.ICollection<string> unresolved)
        {
            int fixedCount = 0;
            try
            {
                Category category = _element.Category;
                if (category?.SubCategories == null || category.SubCategories.Size == 0)
                    return 0;

                foreach (Category subcategory in category.SubCategories)
                {
                    try
                    {
                        if (!targetView.GetCategoryHidden(subcategory.Id))
                            continue;

                        targetView.SetCategoryHidden(subcategory.Id, false);
                        fixedCount++;
                    }
                    catch (Exception ex)
                    {
                        unresolved.Add($"Subcategoria {subcategory.Name} em {targetName} não foi reexibida: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                unresolved.Add($"Subcategorias em {targetName} não foram avaliadas: {ex.Message}");
            }

            return fixedCount;
        }

        private System.Collections.Generic.List<string> HiddenSubcategoryNames(RevitView targetView)
        {
            System.Collections.Generic.List<string> hidden = new System.Collections.Generic.List<string>();
            try
            {
                Category category = _element.Category;
                if (category?.SubCategories == null || category.SubCategories.Size == 0)
                    return hidden;

                foreach (Category subcategory in category.SubCategories)
                {
                    try
                    {
                        if (targetView.GetCategoryHidden(subcategory.Id))
                            hidden.Add(subcategory.Name);
                    }
                    catch { }
                }
            }
            catch { }

            return hidden;
        }

        private int TryEnableHidingFilters(RevitView view, System.Collections.Generic.ICollection<string> unresolved, bool allowTemplateChanges)
        {
            int fixedCount = 0;
            foreach (FilterResult filter in Report.Filters.Where(filter => filter.Applied && filter.Hidden && filter.ElementPasses == true))
            {
                try
                {
                    if (!int.TryParse(filter.Id, out int filterId))
                    {
                        unresolved.Add($"Filtro {filter.Name}: ID inválido.");
                        continue;
                    }

                    view.SetFilterVisibility(new ElementId(filterId), true);
                    fixedCount++;
                }
                catch (Exception ex)
                {
                    fixedCount += TryEnableFilterOnTemplate(view, filter, unresolved, allowTemplateChanges, ex.Message);
                }
            }

            return fixedCount;
        }

        private int TryEnableFilterOnTemplate(RevitView view, FilterResult filter, System.Collections.Generic.ICollection<string> unresolved, bool allowTemplateChanges, string viewError)
        {
            try
            {
                if (view.ViewTemplateId == ElementId.InvalidElementId || !int.TryParse(filter.Id, out int filterId))
                {
                    unresolved.Add($"Filtro {filter.Name} não foi reexibido: {viewError}");
                    return 0;
                }

                if (!(_doc.GetElement(view.ViewTemplateId) is RevitView template))
                {
                    unresolved.Add($"Filtro {filter.Name} não foi reexibido na vista, e o modelo de vista não foi encontrado: {viewError}");
                    return 0;
                }

                if (!allowTemplateChanges)
                {
                    unresolved.Add($"Filtro {filter.Name} parece controlado pelo modelo de vista {template.Name}, mas o modelo de vista não foi alterado.");
                    return 0;
                }

                template.SetFilterVisibility(new ElementId(filterId), true);
                return 1;
            }
            catch (Exception ex)
            {
                unresolved.Add($"Filtro {filter.Name} não foi reexibido no modelo de vista: {ex.Message}");
                return 0;
            }
        }

        private int TryShowWorkset(RevitView view, System.Collections.Generic.ICollection<string> unresolved)
        {
            if (!_doc.IsWorkshared)
                return 0;

            try
            {
                Element hostElement = HostVisibilityElement;
                if (view.GetWorksetVisibility(hostElement.WorksetId) != WorksetVisibility.Hidden)
                    return 0;

                view.SetWorksetVisibility(hostElement.WorksetId, WorksetVisibility.Visible);
                return 1;
            }
            catch (Exception ex)
            {
                unresolved.Add($"Workset não foi reexibido na vista: {ex.Message}");
                return 0;
            }
        }

        private void AddGeometryVisibilityGuidance(System.Collections.Generic.ICollection<string> unresolved)
        {
            if (!Report.Issues.Any(issue =>
                    string.Equals(issue.Key, "GeometryVisibility", StringComparison.OrdinalIgnoreCase) &&
                    issue.Severity == IssueSeverity.Blocked))
                return;

            unresolved.Add(
                "Geometria visível na vista: a ferramenta detectou que o elemento existe, mas a geometria 3D não aparece na vista. " +
                "Isso normalmente precisa ser corrigido no tipo/família, no parâmetro de visibilidade, na subcategoria da família, no nível de detalhe ou no template aplicado à vista.");
        }

        private void Export()
        {
            WpfSaveFileDialog dialog = new WpfSaveFileDialog
            {
                Title = "Exportar relatório do Visibility Inspector",
                FileName = $"visibility_inspector_{Report.ElementId}_{DateTime.Now:yyyyMMdd_HHmmss}.html",
                Filter = "HTML (*.html)|*.html|Texto (*.txt)|*.txt|CSV (*.csv)|*.csv|JSON (*.json)|*.json|PDF imprimível (*.pdf)|*.pdf",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _exportService.Export(Report, dialog.FileName);
                StatusText = $"Relatório exportado: {dialog.FileName}";
                WpfMessageBox.Show("Relatório exportado.", "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintPdf(object? visualParameter)
        {
            try
            {
                WpfPrintDialog dialog = new WpfPrintDialog();
                if (dialog.ShowDialog() != true)
                    return;

                if (visualParameter is Visual visual)
                    dialog.PrintVisual(visual, "Visibility Inspector");
                else
                {
                    TextBlock block = new TextBlock
                    {
                        Text = _exportService.BuildText(Report),
                        FontFamily = new WpfFontFamily("Consolas"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Width = dialog.PrintableAreaWidth
                    };
                    block.Measure(new WpfSize(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight));
                    block.Arrange(new Rect(new System.Windows.Point(0, 0), block.DesiredSize));
                    dialog.PrintVisual(block, "Visibility Inspector");
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompareView()
        {
            try
            {
                if (!(_analysisService is VisibilityAnalysisService concrete))
                    return;

                var picker = new RevitFamilyImporter.VisibilityInspector.Views.VisibilityViewPickerWindow(concrete.GetInspectableViewOptions(_doc), Report.ViewId);
                picker.Owner = WpfApplication.Current?.MainWindow;
                if (picker.ShowDialog() != true || picker.SelectedViewId == ElementId.InvalidElementId)
                    return;

                if (!(_doc.GetElement(picker.SelectedViewId) is RevitView comparedView))
                    return;

                IsBusy = true;
                StatusText = "Comparando vistas...";
                RevitView baseView = _doc.GetElement(new ElementId(int.Parse(Report.ViewId))) as RevitView ?? _uiApp.ActiveUIDocument.ActiveView;
                VisibilityComparison comparison = _linkInstance != null && _analysisService is VisibilityAnalysisService linkedConcrete
                    ? linkedConcrete.CompareLinkedWithView(_uiApp, _doc, baseView, comparedView, _linkInstance, _elementDocument, _element)
                    : _analysisService.CompareWithView(_uiApp, _doc, baseView, comparedView, _element);
                Report.Comparison = comparison;
                ComparisonDifferences.Clear();
                foreach (string difference in comparison.Differences)
                    ComparisonDifferences.Add(difference);
                OnPropertyChanged(nameof(Report));
                StatusText = "Comparação concluída.";
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void FindViews()
        {
            try
            {
                IsBusy = true;
                StatusText = "Percorrendo vistas válidas...";
                ViewResults.Clear();
                System.Collections.Generic.IEnumerable<ViewVisibilitySummary> results = _linkInstance != null && _analysisService is VisibilityAnalysisService concrete
                    ? concrete.FindVisibleViewsForLinkedElement(_uiApp, _doc, _linkInstance, _elementDocument, _element)
                    : _analysisService.FindVisibleViews(_uiApp, _doc, _element);

                foreach (ViewVisibilitySummary item in results)
                    ViewResults.Add(item);
                StatusText = $"{ViewResults.Count} vista(s) analisada(s).";
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenView(object? parameter)
        {
            if (!(parameter is ViewVisibilitySummary summary))
                return;

            if (!int.TryParse(summary.ViewId, out int idValue))
                return;

            try
            {
                ElementId id = new ElementId(idValue);
                if (_doc.GetElement(id) is RevitView view)
                    _uiApp.ActiveUIDocument.ActiveView = view;
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshCollections()
        {
            Issues.Clear();
            foreach (VisibilityIssue issue in Report.Issues.OrderBy(i => i.Priority))
                Issues.Add(issue);

            Overrides.Clear();
            foreach (OverrideRecord record in Report.Overrides)
                Overrides.Add(record);

            Parameters.Clear();
            foreach (ParameterSnapshot parameter in Report.Parameters)
                Parameters.Add(parameter);

            Flow.Clear();
            foreach (VisibilityIssue item in Report.Flow)
                Flow.Add(item);

            RefreshFilters();
        }

        private void RefreshFilters()
        {
            Filters.Clear();
            string filter = (FilterSearch ?? string.Empty).Trim();
            foreach (FilterResult result in Report.Filters
                .Where(item => string.IsNullOrWhiteSpace(filter) || item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(item => item.Applied)
                .ThenBy(item => item.Applied ? item.Priority : int.MaxValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                Filters.Add(result);
            }
        }

        private Element HostVisibilityElement => _linkInstance ?? _element;

        private VisibilityReport AnalyzeCurrentTarget(RevitView view)
        {
            if (_linkInstance != null && _analysisService is VisibilityAnalysisService concrete)
                return concrete.AnalyzeLinkedElement(_uiApp, _doc, view, _linkInstance, _elementDocument, _element);

            return _analysisService.Analyze(_uiApp, _doc, view, _element);
        }
    }
}
