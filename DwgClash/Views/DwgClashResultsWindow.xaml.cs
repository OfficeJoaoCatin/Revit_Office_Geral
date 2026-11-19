using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitFamilyImporter.DwgClash.Commands;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;
using RevitFamilyImporter.LinkConflict.Services;

namespace RevitFamilyImporter.DwgClash.Views
{
    public partial class DwgClashResultsWindow : Window
    {
        private readonly DwgClashExternalEventHandler _eventHandler;
        private LinkConflictCompletionStore _completionStore;
        private bool _restoringCompletion;
        private Dictionary<string, List<DwgClashResult>> _completionGroups;

        public DwgClashResultsWindow(DwgClashSettings settings, DwgClashReport report,
            DwgClashExternalEventHandler eventHandler)
        {
            InitializeComponent();
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            Settings = settings;
            Report = report;
            _eventHandler = eventHandler;
            ResultsGrid.ItemsSource = report.Results;
            _completionGroups = report.Results.Where(item => !string.IsNullOrWhiteSpace(item.CompletionKey))
                .GroupBy(item => item.CompletionKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            InitializeCompletionMemory();
            foreach (DwgClashResult result in report.Results) result.PropertyChanged += Result_PropertyChanged;
            UpdateSummary();
            StatusTextBlock.Text = report.Results.Count == 0
                ? "Nenhum conflito encontrado com os filtros selecionados."
                : "Selecione uma linha para navegar ou crie avisos em uma cópia da planta.";
        }

        private void InitializeCompletionMemory()
        {
            try
            {
                Document document = Settings.View?.Document;
                if (document == null || Settings.ImportInstance == null)
                    throw new InvalidOperationException("O projeto, a vista ou o DWG não está disponível.");
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OfficeGeral", "Clash2DDWG", "v1");
                _completionStore = new LinkConflictCompletionStore(
                    GetModelIdentity(document), Settings.View.UniqueId, Settings.ImportInstance.UniqueId, root);
                foreach (DwgClashResult result in Report.Results)
                    result.IsCompleted = DwgCompletionMigration.IsCompleted(_completionStore, result);
            }
            catch (Exception ex)
            {
                _completionStore = null;
                MemoryStatusText.Text = "Memória local indisponível; marcações valerão apenas nesta janela: " + ex.Message;
                MemoryStatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
            }
        }

        private static string GetModelIdentity(Document document)
        {
            string path = document.PathName ?? string.Empty;
            try
            {
                if (document.IsModelInCloud)
                    path = ModelPathUtils.ConvertModelPathToUserVisiblePath(document.GetCloudModelPath());
                else if (document.IsWorkshared)
                    path = ModelPathUtils.ConvertModelPathToUserVisiblePath(document.GetWorksharingCentralModelPath());
            }
            catch { /* Mantém o caminho local quando não for possível obter o central. */ }
            string projectId = document.ProjectInformation?.UniqueId ?? string.Empty;
            if (document.CreationGUID == Guid.Empty && string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("O projeto não possui identificação disponível para a memória local.");
            return document.CreationGUID.ToString("D") + "|" + projectId + "|" + (path ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void Result_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DwgClashResult.IsCompleted) || _restoringCompletion) return;
            var result = (DwgClashResult)sender;
            if (_completionStore != null)
            {
                try
                {
                    _completionStore.SetCompleted(result.CompletionKey, result.IsCompleted);
                    MemoryStatusText.Text = "As marcações são pessoais e salvas neste computador; uma nova análise recalcula os conflitos.";
                    MemoryStatusText.Foreground = System.Windows.Media.Brushes.SlateGray;
                }
                catch (Exception ex)
                {
                    _restoringCompletion = true;
                    try { result.IsCompleted = !result.IsCompleted; }
                    finally { _restoringCompletion = false; }
                    MemoryStatusText.Text = "Não foi possível salvar a marcação: " + ex.Message;
                    MemoryStatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
                }
            }
            if (_completionGroups.TryGetValue(result.CompletionKey, out List<DwgClashResult> sameConflict))
            {
                _restoringCompletion = true;
                try { foreach (DwgClashResult duplicate in sameConflict) duplicate.IsCompleted = result.IsCompleted; }
                finally { _restoringCompletion = false; }
            }
            UpdateSummary();
            var view = CollectionViewSource.GetDefaultView(ResultsGrid.ItemsSource);
            if (view != null && view.SortDescriptions.Any(sort => sort.PropertyName == nameof(DwgClashResult.IsCompleted)))
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (IsLoaded) view.Refresh(); }));
        }

        private void UpdateSummary()
        {
            SummaryTextBlock.Text = string.Format(
                "Vista: {0} | DWG: {1} | Layer: {2} | Modo: {3} | {4} | Elementos analisados: {5} | Conflitos: {6} | Concluídos: {7}",
                Report.ViewName, Report.ImportName, Report.Layer, Report.ModeText, Report.ClosedRegionText,
                Report.ElementsAnalyzed, Report.Results.Count, Report.Results.Count(item => item.IsCompleted));
        }

        private void Completed_DoubleClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

        public DwgClashSettings Settings { get; }
        public DwgClashReport Report { get; }

        private DwgClashResult SelectedResult => ResultsGrid.SelectedItem as DwgClashResult;

        public DwgClashResult SelectedResultForExternalEvent => SelectedResult;

        private void ResultsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SelectedResult != null)
                StatusTextBlock.Text = "Conflito " + SelectedResult.Number + " selecionado. Use Selecionar / enquadrar para navegar até o elemento.";
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => MoveSelection(-1);

        private void NextButton_Click(object sender, RoutedEventArgs e) => MoveSelection(1);

        private void MoveSelection(int direction)
        {
            if (Report.Results.Count == 0)
                return;
            int index = ResultsGrid.SelectedIndex < 0 ? 0 : ResultsGrid.SelectedIndex + direction;
            if (index < 0)
                index = Report.Results.Count - 1;
            if (index >= Report.Results.Count)
                index = 0;
            ResultsGrid.SelectedIndex = index;
            ResultsGrid.ScrollIntoView(ResultsGrid.SelectedItem);
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            DwgClashResult result = SelectedResult;
            if (result == null)
                return;
            _eventHandler.Request(DwgClashRequest.Select);
        }

        private void ResultsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectButton_Click(sender, e);
        }

        private void CreateMarkersButton_Click(object sender, RoutedEventArgs e)
        {
            _eventHandler.Request(DwgClashRequest.CreateViewMarkers);
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV UTF-8 (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
                FileName = "clash_2d_dwg_revit.csv"
            };
            if (dialog.ShowDialog(this) != true)
                return;

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Numero;Categoria;Familia;Tipo;Id Revit;Layer;Regiao;Distancia;Status;Concluido");
            foreach (DwgClashResult result in Report.Results)
            {
                csv.AppendLine(string.Join(";", new[]
                {
                    result.Number.ToString(), Escape(result.Category), Escape(result.Family), Escape(result.Type),
                    result.ElementId.ToString(), Escape(result.Layer), Escape(result.RegionName),
                    Escape(result.DistanceText), Escape(result.Status), result.IsCompleted ? "Sim" : "Não"
                }));
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            StatusTextBlock.Text = "CSV exportado: " + dialog.FileName;
        }

        private void ExportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*",
                FileName = "clash_2d_dwg_revit.json"
            };
            if (dialog.ShowDialog(this) != true)
                return;

            List<object> rows = Report.Results.Select(result => new
            {
                numero = result.Number,
                categoria = result.Category,
                familia = result.Family,
                tipo = result.Type,
                idRevit = result.ElementId,
                layer = result.Layer,
                regiao = result.RegionName,
                distancia = result.DistanceText,
                status = result.Status,
                concluido = result.IsCompleted
            }).Cast<object>().ToList();
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new
            {
                vista = Report.ViewName,
                dwg = Report.ImportName,
                layer = Report.Layer,
                modo = Report.ModeText,
                elementosAnalisados = Report.ElementsAnalyzed,
                conflitos = rows
            }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));
            StatusTextBlock.Text = "JSON exportado: " + dialog.FileName;
        }

        private static string Escape(string value)
        {
            string text = value ?? string.Empty;
            return text.Contains(";") || text.Contains("\"") || text.Contains("\r") || text.Contains("\n")
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
        }

        public void SetStatus(string message)
        {
            Dispatcher.BeginInvoke(new Action(() => StatusTextBlock.Text = message));
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (DwgClashResult result in Report.Results) result.PropertyChanged -= Result_PropertyChanged;
            base.OnClosed(e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
