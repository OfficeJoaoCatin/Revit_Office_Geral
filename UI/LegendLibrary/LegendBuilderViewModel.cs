using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitFamilyImporter.Import.LegendLibrary;
using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using RevitFamilyImporter.UI.DetailImport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RevitFamilyImporter.UI.LegendLibrary
{
    public class LegendBuilderViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;
        private readonly ILogger _logger = new Logger("LegendLibrary");
        private readonly LegendSettingsService _settingsService;
        private readonly LegendPreviewGenerator _previewGenerator = new LegendPreviewGenerator();
        private DwgDocumentModel _dwgDocument;
        private LegendSettings _settings;
        private LegendItemViewModel _selectedLibraryItem;
        private LegendSectionViewModel _selectedSection;
        private ImageSource _finalPreview;
        private string _dwgPath;
        private string _searchText = string.Empty;
        private string _statusText = "Selecione um DWG de biblioteca.";
        private string _reportText;
        private bool _isBusy;
        private bool _isGridLayout;
        private double _horizontalSpacing;
        private double _verticalSpacing;
        private int _itemsPerColumn;
        private double _importScale;
        private string _boundaryLayer = LegendItemDetector.DefaultBoundaryLayer;
        private string _nameLayer = DetailNamingService.OfficialNameLayer;

        public LegendBuilderViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _doc = uiApp.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Documento Revit não encontrado.");
            _settingsService = new LegendSettingsService(_logger);
            _settings = _settingsService.Load();

            _dwgPath = _settings.LastDwgPath ?? string.Empty;
            _isGridLayout = _settings.LayoutMode == LegendLayoutMode.Grid;
            _horizontalSpacing = _settings.HorizontalSpacing;
            _verticalSpacing = _settings.VerticalSpacing;
            _itemsPerColumn = _settings.ItemsPerColumn;
            _importScale = _settings.ImportScale;
            _boundaryLayer = _settings.BoundaryLayer;
            _nameLayer = _settings.NameLayer;

            foreach (string title in _settings.SectionTitles.DefaultIfEmpty("FORCA"))
                AddSection(new LegendSectionViewModel(title));

            SelectedSection = Sections.FirstOrDefault();

            SelectFileCommand = new DetailImportRelayCommand(SelectFile, () => !IsBusy);
            NewGroupCommand = new DetailImportRelayCommand(NewGroup, () => !IsBusy);
            DeleteGroupCommand = new DetailImportRelayCommand(DeleteGroup, () => !IsBusy && SelectedSection != null && Sections.Count > 1);
            AddSelectedToGroupCommand = new DetailImportRelayCommand(AddSelectedToGroup, () => !IsBusy && SelectedSection != null && LibraryItems.Any(i => i.IsSelected));
            RemoveSelectedGroupItemCommand = new DetailImportRelayCommand(RemoveSelectedGroupItem, () => !IsBusy && SelectedSection?.SelectedItem != null);
            MoveGroupUpCommand = new DetailImportRelayCommand(() => MoveSection(-1), () => !IsBusy && CanMoveSection(-1));
            MoveGroupDownCommand = new DetailImportRelayCommand(() => MoveSection(1), () => !IsBusy && CanMoveSection(1));
            MoveItemUpCommand = new DetailImportRelayCommand(() => MoveItem(-1), () => !IsBusy && CanMoveItem(-1));
            MoveItemDownCommand = new DetailImportRelayCommand(() => MoveItem(1), () => !IsBusy && CanMoveItem(1));
            GenerateCommand = new DetailImportRelayCommand(GenerateLegend, CanGenerate);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler RequestClose;

        public ObservableCollection<LegendItemViewModel> LibraryItems { get; } =
            new ObservableCollection<LegendItemViewModel>();

        public ObservableCollection<LegendItemViewModel> FilteredLibraryItems { get; } =
            new ObservableCollection<LegendItemViewModel>();

        public ObservableCollection<LegendSectionViewModel> Sections { get; } =
            new ObservableCollection<LegendSectionViewModel>();

        public DetailImportRelayCommand SelectFileCommand { get; }
        public DetailImportRelayCommand NewGroupCommand { get; }
        public DetailImportRelayCommand DeleteGroupCommand { get; }
        public DetailImportRelayCommand AddSelectedToGroupCommand { get; }
        public DetailImportRelayCommand RemoveSelectedGroupItemCommand { get; }
        public DetailImportRelayCommand MoveGroupUpCommand { get; }
        public DetailImportRelayCommand MoveGroupDownCommand { get; }
        public DetailImportRelayCommand MoveItemUpCommand { get; }
        public DetailImportRelayCommand MoveItemDownCommand { get; }
        public DetailImportRelayCommand GenerateCommand { get; }

        public string DwgPath
        {
            get => _dwgPath;
            set => SetProperty(ref _dwgPath, value ?? string.Empty);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                    ApplyLibraryFilter();
            }
        }

        public LegendItemViewModel SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set => SetProperty(ref _selectedLibraryItem, value);
        }

        public LegendSectionViewModel SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (SetProperty(ref _selectedSection, value))
                    RefreshCommands();
            }
        }

        public ImageSource FinalPreview
        {
            get => _finalPreview;
            set => SetProperty(ref _finalPreview, value);
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

        public bool IsGridLayout
        {
            get => _isGridLayout;
            set
            {
                if (SetProperty(ref _isGridLayout, value))
                {
                    OnPropertyChanged(nameof(IsVerticalLayout));
                    UpdatePreview();
                }
            }
        }

        public bool IsVerticalLayout
        {
            get => !_isGridLayout;
            set => IsGridLayout = !value;
        }

        public double HorizontalSpacing
        {
            get => _horizontalSpacing;
            set
            {
                if (SetProperty(ref _horizontalSpacing, value))
                    UpdatePreview();
            }
        }

        public double VerticalSpacing
        {
            get => _verticalSpacing;
            set
            {
                if (SetProperty(ref _verticalSpacing, value))
                    UpdatePreview();
            }
        }

        public int ItemsPerColumn
        {
            get => _itemsPerColumn;
            set
            {
                if (SetProperty(ref _itemsPerColumn, value))
                    UpdatePreview();
            }
        }

        public double ImportScale
        {
            get => _importScale;
            set => SetProperty(ref _importScale, value);
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

        public string SelectionSummary
        {
            get
            {
                int selected = LibraryItems.Count(i => i.IsSelected);
                int visible = FilteredLibraryItems.Count;
                string filtered = visible == LibraryItems.Count
                    ? string.Empty
                    : $" | {visible} exibido(s)";
                return $"{selected} de {LibraryItems.Count} selecionado(s){filtered}";
            }
        }

        public string DestinationText => _doc.ActiveView is ViewSheet sheet
            ? $"Destino: folha atual ({sheet.SheetNumber} - {sheet.Name})"
            : "Destino: folha atual. Abra uma folha antes de gerar.";

        private void SelectFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecionar DWG de biblioteca de legenda",
                Filter = "Arquivos DWG (*.dwg)|*.dwg",
                CheckFileExists = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(DwgPath))
                dialog.FileName = DwgPath;

            if (dialog.ShowDialog() != true)
                return;

            DwgPath = dialog.FileName;
            _ = LoadDwgAsync(dialog.FileName);
        }

        private async Task LoadDwgAsync(string path)
        {
            IsBusy = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            StatusText = "Lendo DWG e detectando itens...";
            ReportText = string.Empty;
            LibraryItems.Clear();
            FilteredLibraryItems.Clear();
            FinalPreview = null;

            try
            {
                var total = Stopwatch.StartNew();
                List<LegendItem> items = null;
                DwgDocumentModel document = null;

                await Task.Run(() =>
                {
                    var importer = new LegendLibraryImporter(_logger);
                    importer.Load(path, LegendItemDetector.NormalizeLayerName(BoundaryLayer), DetailNamingService.NormalizeLayerName(NameLayer, DetailNamingService.OfficialNameLayer));
                    document = importer.Document;
                    items = importer.Items;
                });

                _dwgDocument = document;

                StatusText = "Gerando miniaturas...";
                var previewWatch = Stopwatch.StartNew();
                foreach (LegendItem item in items)
                {
                    var viewModel = new LegendItemViewModel(item, _previewGenerator.GenerateItemPreview(item));
                    viewModel.PropertyChanged += LibraryItemChanged;
                    LibraryItems.Add(viewModel);
                }
                ApplyLibraryFilter();
                previewWatch.Stop();

                total.Stop();
                StatusText = $"{LibraryItems.Count} item(ns) detectado(s).";
                ReportText = $"Leitura/deteccao: {total.ElapsedMilliseconds - previewWatch.ElapsedMilliseconds} ms | Preview: {previewWatch.ElapsedMilliseconds} ms | Total: {total.ElapsedMilliseconds} ms";
                SelectedLibraryItem = LibraryItems.FirstOrDefault();
                UpdatePreview();
            }
            catch (Exception ex)
            {
                StatusText = "Falha ao carregar biblioteca CAD.";
                ReportText = ex.Message;
                System.Windows.MessageBox.Show(ex.Message, "Legenda por Biblioteca CAD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                OnPropertyChanged(nameof(SelectionSummary));
                RefreshCommands();
            }
        }

        private void ApplyLibraryFilter()
        {
            string filter = (SearchText ?? string.Empty).Trim();
            List<LegendItemViewModel> visible = string.IsNullOrWhiteSpace(filter)
                ? LibraryItems.ToList()
                : LibraryItems
                    .Where(item => (item.Name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            FilteredLibraryItems.Clear();
            foreach (LegendItemViewModel item in visible)
                FilteredLibraryItems.Add(item);

            if (SelectedLibraryItem != null && !FilteredLibraryItems.Contains(SelectedLibraryItem))
                SelectedLibraryItem = FilteredLibraryItems.FirstOrDefault();
            else if (SelectedLibraryItem == null)
                SelectedLibraryItem = FilteredLibraryItems.FirstOrDefault();

            OnPropertyChanged(nameof(SelectionSummary));
            RefreshCommands();
        }

        private void LibraryItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LegendItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectionSummary));
                RefreshCommands();
            }
        }

        private void NewGroup()
        {
            var group = new LegendSectionViewModel($"GRUPO {Sections.Count + 1}");
            AddSection(group);
            SelectedSection = group;
            UpdatePreview();
        }

        private void DeleteGroup()
        {
            if (SelectedSection == null || Sections.Count <= 1)
                return;

            Sections.Remove(SelectedSection);
            SelectedSection = Sections.FirstOrDefault();
            UpdatePreview();
        }

        private void AddSection(LegendSectionViewModel section)
        {
            if (section == null)
                return;

            section.PropertyChanged += SectionChanged;
            section.Items.CollectionChanged += SectionItemsChanged;
            Sections.Add(section);
        }

        private void SectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LegendSectionViewModel.Title))
                UpdatePreview();
        }

        private void SectionItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<LegendItemViewModel> items)
            {
                LegendSectionViewModel section = Sections.FirstOrDefault(s => ReferenceEquals(s.Items, items));
                section?.RefreshHeader();
            }

            UpdatePreview();
        }

        private void AddSelectedToGroup()
        {
            if (SelectedSection == null)
                return;

            foreach (LegendItemViewModel item in LibraryItems.Where(i => i.IsSelected).ToList())
            {
                RemoveFromAllGroups(item);
                SelectedSection.Items.Add(item);
                item.IsSelected = false;
            }

            SelectedSection.RefreshHeader();
            OnPropertyChanged(nameof(SelectionSummary));
            UpdatePreview();
            RefreshCommands();
        }

        private void RemoveSelectedGroupItem()
        {
            if (SelectedSection?.SelectedItem == null)
                return;

            SelectedSection.Items.Remove(SelectedSection.SelectedItem);
            SelectedSection.SelectedItem = null;
            SelectedSection.RefreshHeader();
            UpdatePreview();
            RefreshCommands();
        }

        private void MoveSection(int delta)
        {
            if (!CanMoveSection(delta))
                return;

            int index = Sections.IndexOf(SelectedSection);
            Sections.Move(index, index + delta);
            UpdatePreview();
            RefreshCommands();
        }

        private bool CanMoveSection(int delta)
        {
            if (SelectedSection == null)
                return false;

            int index = Sections.IndexOf(SelectedSection);
            int target = index + delta;
            return index >= 0 && target >= 0 && target < Sections.Count;
        }

        private void MoveItem(int delta)
        {
            if (!CanMoveItem(delta))
                return;

            int index = SelectedSection.Items.IndexOf(SelectedSection.SelectedItem);
            SelectedSection.Items.Move(index, index + delta);
            UpdatePreview();
            RefreshCommands();
        }

        private bool CanMoveItem(int delta)
        {
            if (SelectedSection?.SelectedItem == null)
                return false;

            int index = SelectedSection.Items.IndexOf(SelectedSection.SelectedItem);
            int target = index + delta;
            return index >= 0 && target >= 0 && target < SelectedSection.Items.Count;
        }

        private void RemoveFromAllGroups(LegendItemViewModel item)
        {
            foreach (LegendSectionViewModel section in Sections)
            {
                if (section.Items.Remove(item))
                    section.RefreshHeader();
            }
        }

        private void GenerateLegend()
        {
            if (!CanGenerate())
                return;

            IsBusy = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                LegendSettings settings = BuildSettings();
                LegendComposition composition = BuildComposition(settings);
                GeneratedLegend generated = new LegendExporter(_doc, _logger)
                    .ExportToActiveSheet(_dwgDocument, composition, settings);

                SaveSettings(settings);
                StatusText = $"Legenda gerada. ImportId: {generated.ImportId.IntegerValue}";
                System.Windows.MessageBox.Show("Legenda CAD gerada na folha atual.", "Legenda por Biblioteca CAD", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusText = "Falha ao gerar legenda.";
                ReportText = ex.Message;
                System.Windows.MessageBox.Show(ex.Message, "Legenda por Biblioteca CAD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                RefreshCommands();
            }
        }

        private bool CanGenerate()
        {
            return !IsBusy &&
                   _dwgDocument != null &&
                   Sections.Any(section => section.Items.Count > 0);
        }

        private void UpdatePreview()
        {
            try
            {
                LegendComposition composition = BuildComposition(BuildSettings());
                FinalPreview = _previewGenerator.GenerateCompositionPreview(composition);
            }
            catch
            {
                FinalPreview = null;
            }

            RefreshCommands();
        }

        private LegendComposition BuildComposition(LegendSettings settings)
        {
            List<LegendSection> sections = Sections
                .Select(section => section.ToModel())
                .Where(section => section.Items.Count > 0)
                .ToList();

            return new LegendComposer().Compose(sections, settings);
        }

        private LegendSettings BuildSettings()
        {
            var settings = new LegendSettings
            {
                LastDwgPath = DwgPath,
                LayoutMode = IsGridLayout ? LegendLayoutMode.Grid : LegendLayoutMode.Vertical,
                HorizontalSpacing = HorizontalSpacing,
                VerticalSpacing = VerticalSpacing,
                ItemsPerColumn = ItemsPerColumn,
                ImportScale = ImportScale,
                BoundaryLayer = BoundaryLayer,
                NameLayer = NameLayer
            };

            foreach (LegendSectionViewModel section in Sections)
                settings.SectionTitles.Add(section.Title);

            settings.Normalize();
            return settings;
        }

        private void SaveSettings(LegendSettings settings)
        {
            _settingsService.Save(settings);
        }

        private void RefreshCommands()
        {
            SelectFileCommand?.RaiseCanExecuteChanged();
            NewGroupCommand?.RaiseCanExecuteChanged();
            DeleteGroupCommand?.RaiseCanExecuteChanged();
            AddSelectedToGroupCommand?.RaiseCanExecuteChanged();
            RemoveSelectedGroupItemCommand?.RaiseCanExecuteChanged();
            MoveGroupUpCommand?.RaiseCanExecuteChanged();
            MoveGroupDownCommand?.RaiseCanExecuteChanged();
            MoveItemUpCommand?.RaiseCanExecuteChanged();
            MoveItemDownCommand?.RaiseCanExecuteChanged();
            GenerateCommand?.RaiseCanExecuteChanged();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}





