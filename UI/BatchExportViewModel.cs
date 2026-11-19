using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Export;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Profiles;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.UI
{
    public class BatchExportViewModel : ObservableObject
    {
        private readonly SheetSelectionService _sheetSelectionService = new SheetSelectionService();
        private readonly ExportSettingsService _exportSettingsService = new ExportSettingsService();
        private readonly PreviewService _previewService = new PreviewService();
        private readonly ProfileService _profileService = new ProfileService();
        private readonly Dictionary<ExportDocumentInfo, List<SheetInfo>> _sheetsByDocument = new Dictionary<ExportDocumentInfo, List<SheetInfo>>();
        private CheckpointData? _resumeCheckpoint;
        private bool _cancelRequested;

        private ExportDocumentInfo? _selectedDocument;
        private PdfExportSettingsInfo? _selectedPdfSettings;
        private DwgExportSettingsInfo? _selectedDwgSettings;
        private BatchExportProfile? _selectedProfile;
        private string _destinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RevitBatchExport");
        private string _namingTemplate = "{SheetNumber} - {SheetName}";
        private bool _exportPdf = true;
        private bool _exportDwg;
        private bool _useDocumentPdfSettings = true;
        private bool _forceGlobalDwgSettings = true;
        private bool _copyDwgSetupToProjects;
        private SheetSelectionMode _selectedSheetSelectionMode = SheetSelectionMode.AllSheets;
        private string _profileName = "Novo perfil";
        private int _timeoutSeconds = 120;
        private long _memoryLimitMb = 8192;
        private bool _isExporting;
        private int _activeTabIndex;
        private double _progressValue;
        private bool _isProgressIndeterminate;
        private string _progressText = "0/0";
        private string _currentStatus = "Pronto";
        private ExportReport? _report;

        public BatchExportViewModel(IEnumerable<ExportDocumentInfo> documents, CheckpointData? resumeCheckpoint)
        {
            Documents = new ObservableCollection<ExportDocumentInfo>(documents);
            Sheets = new ObservableCollection<SheetInfo>();
            PdfSettings = new ObservableCollection<PdfExportSettingsInfo>();
            DwgSettings = new ObservableCollection<DwgExportSettingsInfo>();
            Profiles = new ObservableCollection<BatchExportProfile>();
            PreviewItems = new ObservableCollection<string>();
            SheetModes = new ObservableCollection<SheetSelectionMode>((SheetSelectionMode[])Enum.GetValues(typeof(SheetSelectionMode)));
            DwgVersions = new ObservableCollection<string>(new[] { "Default", "R2018", "R2013", "R2010", "R2007" });
            DpiOptions = new ObservableCollection<int>(new[] { 72, 144, 300, 600, 1200, 2400, 3600, 4000 });

            BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsExporting);
            RefreshPreviewCommand = new RelayCommand(RefreshPreview, () => !IsExporting);
            SelectAllDocumentsCommand = new RelayCommand(() => SetAllDocuments(true), () => !IsExporting);
            DeselectAllDocumentsCommand = new RelayCommand(() => SetAllDocuments(false), () => !IsExporting);
            SelectAllSheetsCommand = new RelayCommand(() => SetAllVisibleSheets(true), () => !IsExporting);
            DeselectAllSheetsCommand = new RelayCommand(() => SetAllVisibleSheets(false), () => !IsExporting);
            SaveProfileCommand = new RelayCommand(SaveProfile, () => !IsExporting);
            LoadProfileCommand = new RelayCommand(LoadSelectedProfile, () => !IsExporting && SelectedProfile != null);
            DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile, () => !IsExporting && SelectedProfile != null);
            ExportCommand = new RelayCommand(ExecuteExport, CanExport);
            CancelCommand = new RelayCommand(RequestCancel, () => IsExporting);
            OpenFolderCommand = new RelayCommand(OpenOutputFolder, () => Report != null && Directory.Exists(Report.OutputFolder));
            OpenLogCommand = new RelayCommand(OpenLog, () => Report != null && File.Exists(Report.LogPath));

            LoadProfiles();
            LoadDocumentsAndSheets();
            SelectedDocument = Documents.FirstOrDefault();
            LoadSettingsFromSelectedDocument();

            if (resumeCheckpoint != null)
                ApplyCheckpoint(resumeCheckpoint);

            RefreshCurrentSheets();
            RefreshPreview();
        }

        public ObservableCollection<ExportDocumentInfo> Documents { get; }

        public ObservableCollection<SheetInfo> Sheets { get; }

        public ObservableCollection<PdfExportSettingsInfo> PdfSettings { get; }

        public ObservableCollection<DwgExportSettingsInfo> DwgSettings { get; }

        public ObservableCollection<BatchExportProfile> Profiles { get; }

        public ObservableCollection<string> PreviewItems { get; }

        public ObservableCollection<SheetSelectionMode> SheetModes { get; }

        public ObservableCollection<string> DwgVersions { get; }

        public ObservableCollection<int> DpiOptions { get; }

        public RelayCommand BrowseFolderCommand { get; }

        public RelayCommand RefreshPreviewCommand { get; }

        public RelayCommand SelectAllDocumentsCommand { get; }

        public RelayCommand DeselectAllDocumentsCommand { get; }

        public RelayCommand SelectAllSheetsCommand { get; }

        public RelayCommand DeselectAllSheetsCommand { get; }

        public RelayCommand SaveProfileCommand { get; }

        public RelayCommand LoadProfileCommand { get; }

        public RelayCommand DeleteProfileCommand { get; }

        public RelayCommand ExportCommand { get; }

        public RelayCommand CancelCommand { get; }

        public RelayCommand OpenFolderCommand { get; }

        public RelayCommand OpenLogCommand { get; }

        public ExportDocumentInfo? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (SetProperty(ref _selectedDocument, value))
                {
                    LoadSettingsFromSelectedDocument();
                }
            }
        }

        public PdfExportSettingsInfo? SelectedPdfSettings
        {
            get => _selectedPdfSettings;
            set
            {
                if (_selectedPdfSettings != null)
                    _selectedPdfSettings.PropertyChanged -= Settings_PropertyChanged;

                if (SetProperty(ref _selectedPdfSettings, value))
                {
                    if (_selectedPdfSettings != null)
                        _selectedPdfSettings.PropertyChanged += Settings_PropertyChanged;

                    RefreshPreview();
                }
            }
        }

        public DwgExportSettingsInfo? SelectedDwgSettings
        {
            get => _selectedDwgSettings;
            set
            {
                if (_selectedDwgSettings != null)
                    _selectedDwgSettings.PropertyChanged -= Settings_PropertyChanged;

                if (SetProperty(ref _selectedDwgSettings, value))
                {
                    if (_selectedDwgSettings != null)
                        _selectedDwgSettings.PropertyChanged += Settings_PropertyChanged;

                    RefreshPreview();
                }
            }
        }

        public BatchExportProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                    RaiseCommandStates();
            }
        }

        public string DestinationFolder
        {
            get => _destinationFolder;
            set
            {
                if (SetProperty(ref _destinationFolder, value))
                {
                    RefreshPreview();
                    RaiseCommandStates();
                }
            }
        }

        public string NamingTemplate
        {
            get => _namingTemplate;
            set
            {
                if (SetProperty(ref _namingTemplate, value))
                    RefreshPreview();
            }
        }

        public bool ExportPdf
        {
            get => _exportPdf;
            set
            {
                if (SetProperty(ref _exportPdf, value))
                {
                    RefreshPreview();
                    RaiseCommandStates();
                }
            }
        }

        public bool ExportDwg
        {
            get => _exportDwg;
            set
            {
                if (SetProperty(ref _exportDwg, value))
                {
                    RefreshPreview();
                    RaiseCommandStates();
                }
            }
        }

        public bool UseDocumentPdfSettings
        {
            get => _useDocumentPdfSettings;
            set => SetProperty(ref _useDocumentPdfSettings, value);
        }

        public bool ForceGlobalDwgSettings
        {
            get => _forceGlobalDwgSettings;
            set => SetProperty(ref _forceGlobalDwgSettings, value);
        }

        public bool CopyDwgSetupToProjects
        {
            get => _copyDwgSetupToProjects;
            set
            {
                if (SetProperty(ref _copyDwgSetupToProjects, value))
                {
                    OnPropertyChanged(nameof(UseIndependentDwgSettings));
                }
            }
        }

        public bool UseIndependentDwgSettings => !CopyDwgSetupToProjects;

        public SheetSelectionMode SelectedSheetSelectionMode
        {
            get => _selectedSheetSelectionMode;
            set
            {
                if (SetProperty(ref _selectedSheetSelectionMode, value))
                {
                    foreach (List<SheetInfo> sheets in _sheetsByDocument.Values)
                        _sheetSelectionService.ApplyMode(sheets, value);

                    RefreshCurrentSheets();
                    RefreshPreview();
                }
            }
        }

        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }

        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set => SetProperty(ref _timeoutSeconds, Math.Max(1, value));
        }

        public long MemoryLimitMb
        {
            get => _memoryLimitMb;
            set => SetProperty(ref _memoryLimitMb, Math.Max(512, value));
        }

        public bool IsExporting
        {
            get => _isExporting;
            set
            {
                if (SetProperty(ref _isExporting, value))
                    RaiseCommandStates();
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set => SetProperty(ref _activeTabIndex, value);
        }

        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        public ExportReport? Report
        {
            get => _report;
            set
            {
                if (SetProperty(ref _report, value))
                {
                    OnPropertyChanged(nameof(HasReport));
                    RaiseCommandStates();
                }
            }
        }

        public bool HasReport => Report != null;

        private void LoadDocumentsAndSheets()
        {
            foreach (ExportDocumentInfo document in Documents)
            {
                document.PropertyChanged += Document_PropertyChanged;
                List<SheetInfo> sheets = _sheetSelectionService.GetSheets(document);
                foreach (SheetInfo sheet in sheets)
                    sheet.PropertyChanged += Sheet_PropertyChanged;

                _sheetsByDocument[document] = sheets;
            }
        }

        private void LoadSettingsFromSelectedDocument()
        {
            if (SelectedDocument?.Document == null)
                return;

            string? selectedPdfName = SelectedPdfSettings?.Name;
            string? selectedDwgName = SelectedDwgSettings?.Name;

            ReplaceCollection(PdfSettings, _exportSettingsService.LoadPdfSettings(SelectedDocument.Document));
            ReplaceCollection(DwgSettings, _exportSettingsService.LoadDwgSettings(SelectedDocument.Document));

            SelectedPdfSettings = PdfSettings.FirstOrDefault(settings => settings.Name == selectedPdfName) ?? PdfSettings.FirstOrDefault();
            SelectedDwgSettings = DwgSettings.FirstOrDefault(settings => settings.Name == selectedDwgName) ?? DwgSettings.FirstOrDefault();
        }

        private void LoadProfiles()
        {
            ReplaceCollection(Profiles, _profileService.List());
        }

        private void RefreshCurrentSheets()
        {
            Sheets.Clear();

            foreach (ExportDocumentInfo document in Documents.Where(document => document.Selected)
                         .OrderBy(document => document.ProjectName, StringComparer.OrdinalIgnoreCase))
            {
                if (!_sheetsByDocument.TryGetValue(document, out List<SheetInfo>? sheets))
                    continue;

                foreach (SheetInfo sheet in sheets)
                    Sheets.Add(sheet);
            }
        }

        private void RefreshPreview()
        {
            PreviewItems.Clear();
            IReadOnlyList<string> preview = _previewService.BuildPreview(
                DestinationFolder,
                Documents,
                _sheetsByDocument.ToDictionary(pair => pair.Key, pair => pair.Value.AsEnumerable()),
                ExportPdf,
                ExportDwg,
                SelectedPdfSettings?.Combine == true,
                NamingTemplate);

            foreach (string item in preview)
                PreviewItems.Add(item);
        }

        private void BrowseFolder()
        {
#if NET48
            using System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Selecionar pasta de destino",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(DestinationFolder) && Directory.Exists(DestinationFolder))
                dialog.SelectedPath = DestinationFolder;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                DestinationFolder = dialog.SelectedPath;
#else
            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Selecionar pasta de destino",
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(DestinationFolder) && Directory.Exists(DestinationFolder))
                dialog.InitialDirectory = DestinationFolder;

            if (dialog.ShowDialog() == true)
                DestinationFolder = dialog.FolderName;
#endif
        }

        private bool CanExport()
        {
            if (IsExporting || string.IsNullOrWhiteSpace(DestinationFolder))
                return false;

            if (!ExportPdf && !ExportDwg)
                return false;

            return Documents.Where(document => document.Selected)
                .Any(document => _sheetsByDocument.TryGetValue(document, out List<SheetInfo>? sheets)
                    && sheets.Any(sheet => sheet.Selected && sheet.CanBePrinted));
        }

        private void ExecuteExport()
        {
            if (!CanExport())
            {
                TaskDialog.Show("Exportacao em Lote", "Selecione ao menos um formato, projeto, folha e pasta de destino.");
                return;
            }

            _cancelRequested = false;
            Report = null;
            IsExporting = true;
            ActiveTabIndex = 4;
            IsProgressIndeterminate = true;
            CurrentStatus = "Iniciando exportacao";
            ProgressValue = 0;
            ProgressText = "0/0";

            try
            {
                BatchExportRequest request = BuildRequest();
                ExportCoordinator coordinator = new ExportCoordinator();
                ExportReport report = coordinator.Run(request, OnProgress, () => _cancelRequested);
                Report = report;
                CurrentStatus = BuildReportStatus(report);
                _resumeCheckpoint = _cancelRequested ? request.ResumeCheckpoint : null;
            }
            catch (Exception exception)
            {
                CurrentStatus = "Falha na exportacao";
                TaskDialog.Show("Erro na Exportacao em Lote", $"{exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                IsProgressIndeterminate = false;
                IsExporting = false;
                RaiseCommandStates();
            }
        }

        private BatchExportRequest BuildRequest()
        {
            if (ExportDwg && CopyDwgSetupToProjects)
            {
                Autodesk.Revit.DB.Document? source = SelectedDocument?.Document;
                if (source == null || !source.IsValidObject)
                    throw new InvalidOperationException("Selecione na aba Projetos o projeto de origem do setup DWG.");
                if (SelectedDwgSettings == null || string.IsNullOrWhiteSpace(SelectedDwgSettings.Name) ||
                    Autodesk.Revit.DB.ExportDWGSettings.FindByName(source, SelectedDwgSettings.Name) == null)
                    throw new InvalidOperationException("Selecione um setup DWG existente no projeto de origem. O setup 'Padrao' da ferramenta não pode ser copiado.");
            }
            List<ExportDocumentInfo> selectedDocuments = Documents
                .Where(document => document.Selected && document.Document != null)
                .ToList();

            Dictionary<Autodesk.Revit.DB.Document, List<SheetInfo>> sheetsByDocument =
                selectedDocuments.ToDictionary(
                    document => document.Document!,
                    document => _sheetsByDocument[document]
                        .Where(sheet => sheet.Selected && sheet.CanBePrinted)
                        .ToList());

            return new BatchExportRequest
            {
                DestinationFolder = DestinationFolder,
                ExportPdf = ExportPdf,
                ExportDwg = ExportDwg,
                UseDocumentPdfSettings = UseDocumentPdfSettings,
                ForceGlobalDwgSettings = ForceGlobalDwgSettings,
                CopyDwgSetupToProjects = CopyDwgSetupToProjects,
                DwgSetupSourceDocument = SelectedDocument?.Document,
                DwgSetupSourceKey = SelectedDocument?.StableKey ?? string.Empty,
                NamingTemplate = NamingTemplate,
                PdfSettings = ClonePdfSettings(SelectedPdfSettings),
                DwgSettings = CloneDwgSettings(SelectedDwgSettings),
                Documents = selectedDocuments,
                SheetsByDocument = sheetsByDocument,
                TimeoutSeconds = TimeoutSeconds,
                MemoryLimitMb = MemoryLimitMb,
                ResumeCheckpoint = _resumeCheckpoint
            };
        }

        private void RequestCancel()
        {
            _cancelRequested = true;
            CurrentStatus = "Cancelamento solicitado; o item atual sera finalizado.";
        }

        private void OnProgress(ExportProgressInfo info)
        {
            ProgressValue = info.Percent;
            IsProgressIndeterminate = IsExporting
                && info.Message.StartsWith("Exportando", StringComparison.OrdinalIgnoreCase);
            ProgressText = $"{info.Completed}/{info.Total} ({info.Percent:F0}%)";
            CurrentStatus = info.Message;
            PumpUi();
        }

        private void ApplyCheckpoint(CheckpointData checkpoint)
        {
            _resumeCheckpoint = checkpoint;
            ApplyProfile(checkpoint.Profile);

            HashSet<string> selectedDocumentKeys = new HashSet<string>(checkpoint.SelectedDocumentKeys, StringComparer.OrdinalIgnoreCase);
            foreach (ExportDocumentInfo document in Documents)
            {
                document.Selected = selectedDocumentKeys.Count == 0 || selectedDocumentKeys.Contains(document.StableKey);

                if (_sheetsByDocument.TryGetValue(document, out List<SheetInfo>? sheets)
                    && checkpoint.SelectedSheetIdsByDocument.TryGetValue(document.StableKey, out List<long>? selectedSheetIds))
                {
                    HashSet<long> selectedIds = new HashSet<long>(selectedSheetIds);
                    foreach (SheetInfo sheet in sheets)
                        sheet.Selected = selectedIds.Contains(sheet.IdValue);
                }
            }

            SelectedDocument = checkpoint.Profile.CopyDwgSetupToProjects
                ? Documents.FirstOrDefault(document => string.Equals(document.StableKey, checkpoint.Profile.DwgSetupSourceKey, StringComparison.OrdinalIgnoreCase))
                : Documents.FirstOrDefault(document => document.Selected) ?? Documents.FirstOrDefault();
            if (checkpoint.Profile.CopyDwgSetupToProjects && SelectedDocument != null)
                SelectedDwgSettings = EnsureDwgSetting(checkpoint.Profile.DwgSetupName);
            CurrentStatus = $"Checkpoint carregado: {checkpoint.CurrentProjectPath} / {checkpoint.CurrentSheetNumber}";
        }

        private void SaveProfile()
        {
            BatchExportProfile profile = BuildProfileSnapshot();
            _profileService.Save(profile);
            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(item => item.Name == profile.Name);
            CurrentStatus = "Perfil salvo";
        }

        private void LoadSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            BatchExportProfile? profile = _profileService.Load(SelectedProfile.Name) ?? SelectedProfile;
            ApplyProfile(profile);
            CurrentStatus = "Perfil carregado";
        }

        private void DeleteSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            _profileService.Delete(SelectedProfile.Name);
            LoadProfiles();
            SelectedProfile = null;
            CurrentStatus = "Perfil excluido";
        }

        private void ApplyProfile(BatchExportProfile profile)
        {
            if (profile.CopyDwgSetupToProjects)
                SelectedDocument = Documents.FirstOrDefault(document => string.Equals(document.StableKey, profile.DwgSetupSourceKey, StringComparison.OrdinalIgnoreCase));
            ProfileName = profile.Name == "Checkpoint" ? "Novo perfil" : profile.Name;
            DestinationFolder = profile.DestinationFolder;
            ExportPdf = profile.ExportPdf;
            ExportDwg = profile.ExportDwg;
            UseDocumentPdfSettings = profile.UseDocumentPdfSettings;
            ForceGlobalDwgSettings = profile.ForceGlobalDwgSettings;
            CopyDwgSetupToProjects = profile.CopyDwgSetupToProjects;
            NamingTemplate = profile.NamingTemplate;
            TimeoutSeconds = profile.TimeoutSeconds;
            MemoryLimitMb = profile.MemoryLimitMb;
            SelectedSheetSelectionMode = profile.SheetSelectionMode;

            PdfExportSettingsInfo pdf = EnsurePdfSetting(profile.PdfSetupName);
            pdf.Combine = profile.PdfCombine;
            pdf.DPI = profile.PdfDpi;
            pdf.Center = profile.PdfCenter;
            pdf.VectorMode = profile.PdfVectorMode;
            SelectedPdfSettings = pdf;

            DwgExportSettingsInfo dwg = EnsureDwgSetting(profile.DwgSetupName);
            dwg.Version = profile.DwgVersion;
            dwg.ExportLinks = profile.DwgExportLinks;
            dwg.MergeViews = profile.DwgMergeViews;
            SelectedDwgSettings = dwg;

            RefreshPreview();
        }

        private BatchExportProfile BuildProfileSnapshot()
        {
            return new BatchExportProfile
            {
                Name = string.IsNullOrWhiteSpace(ProfileName) ? "Novo perfil" : ProfileName.Trim(),
                DestinationFolder = DestinationFolder,
                ExportPdf = ExportPdf,
                ExportDwg = ExportDwg,
                UseDocumentPdfSettings = UseDocumentPdfSettings,
                ForceGlobalDwgSettings = ForceGlobalDwgSettings,
                CopyDwgSetupToProjects = CopyDwgSetupToProjects,
                DwgSetupSourceKey = SelectedDocument?.StableKey ?? string.Empty,
                PdfSetupName = SelectedPdfSettings?.Name ?? string.Empty,
                DwgSetupName = SelectedDwgSettings?.Name ?? string.Empty,
                PdfCombine = SelectedPdfSettings?.Combine == true,
                PdfDpi = SelectedPdfSettings?.DPI ?? 300,
                PdfCenter = SelectedPdfSettings?.Center != false,
                PdfVectorMode = SelectedPdfSettings?.VectorMode != false,
                DwgVersion = SelectedDwgSettings?.Version ?? "Default",
                DwgExportLinks = SelectedDwgSettings?.ExportLinks == true,
                DwgMergeViews = SelectedDwgSettings?.MergeViews != false,
                SheetSelectionMode = SelectedSheetSelectionMode,
                NamingTemplate = NamingTemplate,
                TimeoutSeconds = TimeoutSeconds,
                MemoryLimitMb = MemoryLimitMb
            };
        }

        private void SetAllDocuments(bool selected)
        {
            foreach (ExportDocumentInfo document in Documents)
                document.Selected = selected;

            RefreshCurrentSheets();
            RefreshPreview();
            RaiseCommandStates();
        }

        private void SetAllVisibleSheets(bool selected)
        {
            foreach (SheetInfo sheet in Sheets)
            {
                if (sheet.CanBePrinted)
                    sheet.Selected = selected;
            }

            RefreshPreview();
            RaiseCommandStates();
        }

        private void OpenOutputFolder()
        {
            if (Report == null || !Directory.Exists(Report.OutputFolder))
                return;

            Process.Start(new ProcessStartInfo(Report.OutputFolder) { UseShellExecute = true });
        }

        private void OpenLog()
        {
            if (Report == null || !File.Exists(Report.LogPath))
                return;

            Process.Start(new ProcessStartInfo(Report.LogPath) { UseShellExecute = true });
        }

        private void Document_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExportDocumentInfo.Selected))
            {
                RefreshCurrentSheets();
                RefreshPreview();
                RaiseCommandStates();
            }
        }

        private void Sheet_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetInfo.Selected))
            {
                RefreshPreview();
                RaiseCommandStates();
            }
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshPreview();
        }

        private void RaiseCommandStates()
        {
            BrowseFolderCommand.RaiseCanExecuteChanged();
            RefreshPreviewCommand.RaiseCanExecuteChanged();
            SelectAllDocumentsCommand.RaiseCanExecuteChanged();
            DeselectAllDocumentsCommand.RaiseCanExecuteChanged();
            SelectAllSheetsCommand.RaiseCanExecuteChanged();
            DeselectAllSheetsCommand.RaiseCanExecuteChanged();
            SaveProfileCommand.RaiseCanExecuteChanged();
            LoadProfileCommand.RaiseCanExecuteChanged();
            DeleteProfileCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            OpenFolderCommand.RaiseCanExecuteChanged();
            OpenLogCommand.RaiseCanExecuteChanged();
        }

        private PdfExportSettingsInfo EnsurePdfSetting(string name)
        {
            PdfExportSettingsInfo? existing = PdfSettings.FirstOrDefault(settings => settings.Name == name);
            if (existing != null)
                return existing;

            PdfExportSettingsInfo created = new PdfExportSettingsInfo { Name = string.IsNullOrWhiteSpace(name) ? "Padrao" : name };
            PdfSettings.Add(created);
            return created;
        }

        private DwgExportSettingsInfo EnsureDwgSetting(string name)
        {
            DwgExportSettingsInfo? existing = DwgSettings.FirstOrDefault(settings => settings.Name == name);
            if (existing != null)
                return existing;

            DwgExportSettingsInfo created = new DwgExportSettingsInfo { Name = string.IsNullOrWhiteSpace(name) ? "Padrao" : name };
            DwgSettings.Add(created);
            return created;
        }

        private static PdfExportSettingsInfo ClonePdfSettings(PdfExportSettingsInfo? source)
        {
            return new PdfExportSettingsInfo
            {
                Name = source?.Name ?? "Padrao",
                Combine = source?.Combine == true,
                DPI = source?.DPI ?? 300,
                Center = source?.Center != false,
                VectorMode = source?.VectorMode != false
            };
        }

        private static DwgExportSettingsInfo CloneDwgSettings(DwgExportSettingsInfo? source)
        {
            return new DwgExportSettingsInfo
            {
                Name = source?.Name ?? "Padrao",
                Version = source?.Version ?? "Default",
                ExportLinks = source?.ExportLinks == true,
                MergeViews = source?.MergeViews != false
            };
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }

        private static string BuildReportStatus(ExportReport report)
        {
            return $"Concluido: {report.Succeeded} sucesso(s), {report.Failed} falha(s), {report.Cancelled} cancelado(s), {report.Elapsed:mm\\:ss}.";
        }

        private static void PumpUi()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(delegate { }));
        }
    }
}

