using Autodesk.Revit.DB;
using RevitView = Autodesk.Revit.DB.View;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace RevitFamilyImporter.UI.DetailImport
{
    public class DetailImporterViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ILogger _logger = new Logger("DetailImport");
        private readonly DetailImportSettingsService _settingsService;
        private DwgDocumentModel _dwgDocument;
        private DetailRegionViewModel _selectedDetail;
        private string _dwgPath = string.Empty;
        private string _statusText = "Selecione um DWG para detectar os detalhes.";
        private string _reportText = string.Empty;
        private bool _isBusy;
        private CancellationTokenSource _importCancellation;
        private double _margin;
        private double _spacing;
        private double _scale;
        private string _boundaryLayer = BoundaryDetector.DefaultBoundaryLayer;
        private string _nameLayer = DetailNamingService.OfficialNameLayer;
        private int _columns;
        private bool _automaticInsertion;
        private bool _adjustSpacing;
        private bool _uniformScale;
        private bool _isGridLayout = true;
        private bool _useGstarCadHighFidelity;
        private int _maxEntitiesPerDetail;

        public DetailImporterViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc?.Document ?? throw new InvalidOperationException("Documento Revit não encontrado.");
            _settingsService = new DetailImportSettingsService(_logger);

            ImportSettings settings = _settingsService.Load();
            _margin = settings.Margin;
            _spacing = settings.Spacing;
            _scale = settings.Scale;
            _boundaryLayer = settings.BoundaryLayer;
            _nameLayer = settings.NameLayer;
            _columns = settings.Columns;
            _automaticInsertion = settings.AutomaticInsertion;
            _adjustSpacing = settings.AdjustSpacing;
            _uniformScale = settings.UniformScale;
            _isGridLayout = settings.LayoutMode == ImportLayoutMode.Grid;
            _useGstarCadHighFidelity = settings.UseGstarCadHighFidelity;
            _maxEntitiesPerDetail = settings.MaxEntitiesPerDetail;

            SelectFileCommand = new DetailImportRelayCommand(SelectFile, () => !IsBusy);
            InsertCommand = new DetailImportRelayCommand(InsertSelected, CanInsert);
            SelectAllCommand = new DetailImportRelayCommand(() => SetSelection(true), () => Details.Count > 0 && !IsBusy);
            ClearSelectionCommand = new DetailImportRelayCommand(() => SetSelection(false), () => Details.Count > 0 && !IsBusy);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DetailRegionViewModel> Details { get; } =
            new ObservableCollection<DetailRegionViewModel>();

        public DetailImportRelayCommand SelectFileCommand { get; }

        public DetailImportRelayCommand InsertCommand { get; }

        public DetailImportRelayCommand SelectAllCommand { get; }

        public DetailImportRelayCommand ClearSelectionCommand { get; }

        public string DwgPath
        {
            get => _dwgPath;
            set => SetProperty(ref _dwgPath, value ?? string.Empty);
        }

        public DetailRegionViewModel SelectedDetail
        {
            get => _selectedDetail;
            set => SetProperty(ref _selectedDetail, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value ?? string.Empty);
        }

        public string ReportText
        {
            get => _reportText;
            set => SetProperty(ref _reportText, value ?? string.Empty);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCommands();
            }
        }

        public double Margin
        {
            get => _margin;
            set => SetProperty(ref _margin, value);
        }

        public double Spacing
        {
            get => _spacing;
            set => SetProperty(ref _spacing, value);
        }

        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public string BoundaryLayer
        {
            get => _boundaryLayer;
            set => SetProperty(ref _boundaryLayer, value ?? string.Empty);
        }

        public string NameLayer
        {
            get => _nameLayer;
            set => SetProperty(ref _nameLayer, value ?? string.Empty);
        }

        public int Columns
        {
            get => _columns;
            set => SetProperty(ref _columns, value);
        }

        public bool AutomaticInsertion
        {
            get => _automaticInsertion;
            set => SetProperty(ref _automaticInsertion, value);
        }

        public bool AdjustSpacing
        {
            get => _adjustSpacing;
            set => SetProperty(ref _adjustSpacing, value);
        }

        public bool UniformScale
        {
            get => _uniformScale;
            set => SetProperty(ref _uniformScale, value);
        }

        public bool UseGstarCadHighFidelity
        {
            get => _useGstarCadHighFidelity;
            set => SetProperty(ref _useGstarCadHighFidelity, value);
        }

        public int MaxEntitiesPerDetail
        {
            get => _maxEntitiesPerDetail;
            set => SetProperty(ref _maxEntitiesPerDetail, value);
        }

        public bool IsGridLayout
        {
            get => _isGridLayout;
            set
            {
                if (SetProperty(ref _isGridLayout, value))
                    OnPropertyChanged(nameof(IsVerticalLayout));
            }
        }

        public bool IsVerticalLayout
        {
            get => !_isGridLayout;
            set => IsGridLayout = !value;
        }

        public string DestinationText
        {
            get
            {
                return "Serão criadas vistas de legenda para os detalhes selecionados.";
            }
        }

        public string SelectionSummary
        {
            get
            {
                int selected = Details.Count(d => d.IsSelected);
                return $"{selected} de {Details.Count} selecionado(s)";
            }
        }

        private void SelectFile()
        {
            var dialog = new WpfOpenFileDialog
            {
                Title = "Selecionar DWG de detalhes",
                Filter = "Arquivos DWG (*.dwg)|*.dwg",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            DwgPath = dialog.FileName;
            LoadDwg(dialog.FileName);
        }

        private void LoadDwg(string path)
        {
            IsBusy = true;
            Mouse.OverrideCursor = WpfCursors.Wait;

            try
            {
                Details.Clear();
                SelectedDetail = null;
                ReportText = string.Empty;
                StatusText = "Lendo DWG...";

                var result = new DetailImportResult();
                var total = Stopwatch.StartNew();
                var reader = new DwgReader(_logger);
                _dwgDocument = reader.Read(path, result);

                StatusText = "Detectando limites...";
                var detector = new BoundaryDetector(_logger);
                string boundaryLayer = BoundaryDetector.NormalizeLayerName(BoundaryLayer);
                string nameLayer = DetailNamingService.NormalizeLayerName(NameLayer, DetailNamingService.OfficialNameLayer);
                List<DetailRegion> regions = detector.Detect(_dwgDocument, boundaryLayer, result);

                StatusText = "Extraindo entidades...";
                new DetailExtractor(_logger).Extract(_dwgDocument, regions, boundaryLayer);
                new DetailNamingService(_logger).ApplyNames(regions, nameLayer);

                StatusText = "Gerando previews...";
                var previewWatch = Stopwatch.StartNew();
                var previewGenerator = new PreviewGenerator();
                foreach (DetailRegion region in regions)
                {
                    var item = new DetailRegionViewModel(region, previewGenerator.Generate(region));
                    item.PropertyChanged += DetailSelectionChanged;
                    Details.Add(item);
                }

                previewWatch.Stop();
                result.PreviewMilliseconds = previewWatch.ElapsedMilliseconds;
                total.Stop();
                result.TotalMilliseconds = total.ElapsedMilliseconds;

                SelectedDetail = Details.FirstOrDefault();
                StatusText = $"{Details.Count} detalhe(s) detectado(s). {SelectionSummary}.";
                ReportText = BuildDetectionReport(result, Details.Count);
            }
            catch (Exception ex)
            {
                StatusText = "Falha ao carregar DWG.";
                ReportText = ex.Message;
                WpfMessageBox.Show(ex.Message, "Importar Detalhes DWG", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                RefreshCommands();
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }

        private async void InsertSelected()
        {
            List<DetailRegion> selected = Details
                .Where(d => d.IsSelected)
                .Select(d => d.Region)
                .ToList();

            if (selected.Count == 0)
            {
                WpfMessageBox.Show("Selecione pelo menos um detalhe.", "Importar Detalhes DWG", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            _importCancellation = new CancellationTokenSource();
            Mouse.OverrideCursor = WpfCursors.Wait;

            try
            {
                ImportSettings settings = BuildSettings();
                if (settings.UseGstarCadHighFidelity &&
                    GstarCadDetailExporter.TryGetRunningSessions(out string sessions))
                {
                    bool continueWithInternal = GstarCadSessionChoiceWindow.Confirm(_uiApp, sessions);
                    if (!continueWithInternal)
                    {
                        StatusText = "Importação cancelada. A alta fidelidade continua selecionada.";
                        return;
                    }

                    // A escolha vale apenas para esta importação; preserve a preferência na janela.
                    settings.UseGstarCadHighFidelity = false;
                }

                _settingsService.Save(BuildSettings());
                var importer = new SheetImporter(_doc, _logger);
                DetailImportResult result = await importer.ImportAsync(
                    _dwgDocument,
                    selected,
                    settings,
                    progress => StatusText = progress,
                    _importCancellation.Token);
                OpenFirstCreatedView(result);
                StatusText = $"{result.Imported.Count} vista(s) criada(s), {result.Failures.Count} falha(s).";
                ReportText = BuildImportReport(result);

                WpfMessageBox.Show(ReportText, "Importar Detalhes DWG", MessageBoxButton.OK,
                    result.Failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusText = "Falha ao importar detalhes.";
                ReportText = ex.Message;
                WpfMessageBox.Show(ex.Message, "Importar Detalhes DWG", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _importCancellation?.Dispose();
                _importCancellation = null;
                Mouse.OverrideCursor = null;
                IsBusy = false;
                RefreshCommands();
            }
        }

        public void CancelImport()
        {
            if (_importCancellation != null && !_importCancellation.IsCancellationRequested)
            {
                StatusText = "Cancelando importação...";
                _importCancellation.Cancel();
            }
        }

        private ImportSettings BuildSettings()
        {
            var settings = new ImportSettings
            {
                Margin = Margin,
                Spacing = Spacing,
                Scale = Scale,
                BoundaryLayer = BoundaryLayer,
                NameLayer = NameLayer,
                Columns = Columns,
                AutomaticInsertion = AutomaticInsertion,
                AdjustSpacing = AdjustSpacing,
                UniformScale = UniformScale,
                UseGstarCadHighFidelity = UseGstarCadHighFidelity,
                MaxEntitiesPerDetail = MaxEntitiesPerDetail,
                LayoutMode = IsGridLayout ? ImportLayoutMode.Grid : ImportLayoutMode.Vertical
            };
            settings.Normalize();
            return settings;
        }

        private bool CanInsert()
        {
            return !IsBusy &&
                   _dwgDocument != null &&
                   Details.Any(d => d.IsSelected);
        }

        private void SetSelection(bool selected)
        {
            foreach (DetailRegionViewModel detail in Details)
                detail.IsSelected = selected;

            OnPropertyChanged(nameof(SelectionSummary));
            RefreshCommands();
        }

        private void DetailSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DetailRegionViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectionSummary));
                StatusText = $"{Details.Count} detalhe(s) detectado(s). {SelectionSummary}.";
                RefreshCommands();
            }
        }

        private static string BuildDetectionReport(DetailImportResult result, int detailCount)
        {
            return
                $"Total detectado: {detailCount}\n" +
                $"Tempo leitura: {result.ReadMilliseconds} ms\n" +
                $"Tempo deteccao: {result.DetectionMilliseconds} ms\n" +
                $"Tempo preview: {result.PreviewMilliseconds} ms\n" +
                $"Tempo total: {result.TotalMilliseconds} ms";
        }

        private static string BuildImportReport(DetailImportResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Total solicitado: {result.TotalRequested}");
            builder.AppendLine($"Vistas criadas: {result.Imported.Count}");
            builder.AppendLine($"Falhas: {result.Failures.Count}");
            builder.AppendLine($"Tempo geracao: {result.ImportMilliseconds} ms");

            int legends = result.Imported.Count(item => item.CreatedAsLegend);
            if (result.Imported.Count > 0)
                builder.AppendLine($"Legends: {legends}; Drafting Views: {result.Imported.Count - legends}");

            int gstarExports = result.Imported.Count(item =>
                !string.IsNullOrWhiteSpace(item.SourceFile) &&
                item.SourceFile.IndexOf("_gstar", StringComparison.OrdinalIgnoreCase) >= 0);
            if (gstarExports > 0)
                builder.AppendLine($"Alta fidelidade GstarCAD: {gstarExports}");

            if (result.Imported.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Vistas:");
                foreach (ImportedDetail item in result.Imported.Take(10))
                    builder.AppendLine("- " + item.ViewName);

                if (result.Imported.Count > 10)
                    builder.AppendLine($"- Mais {result.Imported.Count - 10} vista(s).");
            }

            if (result.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Avisos:");
                foreach (string warning in result.Warnings.Distinct().Take(5))
                    builder.AppendLine("- " + warning);
            }

            if (result.Failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Falhas:");
                foreach (string failure in result.Failures.Take(10))
                    builder.AppendLine("- " + failure);

                if (result.Failures.Count > 10)
                    builder.AppendLine($"- Mais {result.Failures.Count - 10} falha(s).");
            }

            return builder.ToString();
        }

        private void OpenFirstCreatedView(DetailImportResult result)
        {
            try
            {
                ElementId viewId = result?.Imported?.FirstOrDefault()?.ViewId ?? ElementId.InvalidElementId;
                RevitView view = _doc.GetElement(viewId) as RevitView;
                if (view != null && !view.IsTemplate)
                    _uiDoc.ActiveView = view;
            }
            catch { }
        }

        private void RefreshCommands()
        {
            SelectFileCommand.RaiseCanExecuteChanged();
            InsertCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
        }

        protected bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}



