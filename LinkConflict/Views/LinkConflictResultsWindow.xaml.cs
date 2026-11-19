using System;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Commands;
using RevitFamilyImporter.LinkConflict.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace RevitFamilyImporter.LinkConflict.Views
{
    public partial class LinkConflictResultsWindow : Window
    {
        private readonly UIApplication _uiApp;
        private readonly RevitLinkInstance _linkInstance;
        private readonly LinkConflictReport _report;
        private readonly LinkConflictExternalEventHandler _handler;
        private LinkConflictCompletionStore _completionStore;
        private bool _restoringCompletion;

        public LinkConflictResultsWindow(UIApplication uiApp, RevitLinkInstance linkInstance, LinkConflictReport report, LinkConflictExternalEventHandler handler)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _linkInstance = linkInstance ?? throw new ArgumentNullException(nameof(linkInstance));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            InitializeComponent();
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            ConflictsGrid.ItemsSource = _report.Conflicts;
            HostCategoryComboBox.Items.Add("Todas as categorias");
            foreach (string category in _report.Conflicts
                .Select(item => item.HostCategory)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
            {
                HostCategoryComboBox.Items.Add(category);
            }
            HostCategoryComboBox.SelectedIndex = 0;
            InitializeCompletionMemory();
            foreach (LinkConflictItem conflict in _report.Conflicts)
                conflict.PropertyChanged += Conflict_PropertyChanged;
            UpdateSummary();
            SummaryText.ToolTip = _report.CategoryName;
            if (_report.HasUnverified)
            {
                UnverifiedStatusText.Text = "A análise está incompleta: há elementos ou pares sem geometria verificável. Veja os itens não verificados.";
                UnverifiedStatusText.Visibility = System.Windows.Visibility.Visible;
                UnverifiedButton.Visibility = System.Windows.Visibility.Visible;
            }

            PerformanceText.Text = $"Análise: {_report.AnalysisDuration.TotalSeconds:N1} s | " +
                $"Pares verificados: {_report.ComparedPairCount:N0} | " +
                $"Sem candidatos próximos: {_report.PrefilteredLinkedElementCount:N0} | " +
                $"Regiões vazias reutilizadas: {_report.EmptyRegionReuses:N0}";
            if (_report.UnboundedLinkedElementCount > 0 || _report.UnboundedHostPairCount > 0)
            {
                PerformanceText.Text += $" | Sem caixa confiável: {_report.UnboundedLinkedElementCount:N0} no link; {_report.UnboundedHostPairCount:N0} pares no hospedeiro";
                PerformanceText.Foreground = System.Windows.Media.Brushes.DarkRed;
            }
            PerformanceText.ToolTip = $"Busca de candidatos: {_report.SearchDuration.TotalSeconds:N1} s; " +
                $"leitura de geometria: {_report.GeometryDuration.TotalSeconds:N1} s; " +
                $"preparação das linhas/curvas: {_report.CurvePreparationDuration.TotalSeconds:N1} s; " +
                $"comparações e resultados: {_report.ComparisonDuration.TotalSeconds:N1} s. " +
                "O tempo total inclui também outras etapas da análise.";

            if (_report.Conflicts.Count > 0)
                ConflictsGrid.SelectedIndex = 0;
            else
                DetailsText.Text = _report.HasUnverified
                    ? "Nenhum conflito físico foi confirmado. Há itens não verificados; a ausência de conflitos não está comprovada."
                    : "Nenhum conflito físico foi confirmado para as categorias selecionadas.";
        }

        private LinkConflictItem SelectedConflict => ConflictsGrid.SelectedItem as LinkConflictItem;

        private void InitializeCompletionMemory()
        {
            try
            {
                Document host = _uiApp.ActiveUIDocument?.Document;
                Document linked = _linkInstance.GetLinkDocument();
                if (host == null || linked == null)
                    throw new InvalidOperationException("O projeto ou o link não está disponível.");
                _completionStore = new LinkConflictCompletionStore(
                    GetModelIdentity(host), GetModelIdentity(linked), _linkInstance.UniqueId);
                foreach (LinkConflictItem item in _report.Conflicts)
                    item.IsCompleted = _completionStore.IsCompleted(item.CompletionKey);
            }
            catch (Exception ex)
            {
                _completionStore = null;
                MemoryStatusText.Text = "Memória local indisponível; marcações valerão apenas nesta janela: " + ex.Message;
                MemoryStatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
            }
        }

        internal static string GetModelIdentity(Document document)
        {
            string path = document.PathName ?? string.Empty;
            try
            {
                if (document.IsModelInCloud)
                    path = ModelPathUtils.ConvertModelPathToUserVisiblePath(document.GetCloudModelPath());
                else if (document.IsWorkshared)
                    path = ModelPathUtils.ConvertModelPathToUserVisiblePath(document.GetWorksharingCentralModelPath());
            }
            catch { /* O caminho local e o GUID ainda distinguem o projeto quando disponíveis. */ }
            string projectId = document.ProjectInformation?.UniqueId ?? string.Empty;
            if (document.CreationGUID == Guid.Empty && string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("O projeto não possui identificação disponível para a memória local.");
            return document.CreationGUID.ToString("D") + "|" + projectId + "|" + (path ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void Conflict_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LinkConflictItem.IsCompleted) || _restoringCompletion) return;
            var conflict = (LinkConflictItem)sender;
            if (_completionStore != null)
            {
                try
                {
                    _completionStore.SetCompleted(conflict.CompletionKey, conflict.IsCompleted);
                    MemoryStatusText.Text = "As marcações são pessoais e salvas neste computador; uma nova análise recalcula os conflitos.";
                    MemoryStatusText.Foreground = System.Windows.Media.Brushes.SlateGray;
                }
                catch (Exception ex)
                {
                    _restoringCompletion = true;
                    try { conflict.IsCompleted = !conflict.IsCompleted; }
                    finally { _restoringCompletion = false; }
                    MemoryStatusText.Text = "Não foi possível salvar a marcação: " + ex.Message;
                    MemoryStatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
                }
            }
            UpdateSummary();
            var view = CollectionViewSource.GetDefaultView(ConflictsGrid.ItemsSource);
            if (view != null && view.SortDescriptions.Any(sort => sort.PropertyName == nameof(LinkConflictItem.IsCompleted)))
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (IsLoaded) view.Refresh();
                }));
        }

        private void UpdateSummary()
        {
            int completed = _report.Conflicts.Count(conflict => conflict.IsCompleted);
            SummaryText.Text = $"Link: {_report.LinkName} | Categorias: {_report.CategoryName} | " +
                               $"Elementos analisados: {_report.LinkedElementCount:N0} | " +
                               $"Conflitos confirmados: {_report.Conflicts.Count:N0} | Concluídos: {completed:N0}";
        }

        private void ShowUnverified_Click(object sender, RoutedEventArgs e)
        {
            var window = new LinkConflictDiagnosticWindow(_report.UnverifiedSummary,
                "Itens não verificados — Conflitos em Links",
                "Estes itens não são conflitos confirmados; eles precisam de revisão porque não puderam ser verificados.");
            window.Owner = this;
            window.Show();
        }

        private void Completed_DoubleClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

        public void SetMarkerStatus(string message)
        {
            DetailsText.Text = message;
        }

        private void Conflict_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;

            string relation = conflict.IsApproximate ? "pode ter conflito com" : "intersecta";
            string resultLabel = conflict.IsApproximate ? "Verificação" : "Volume de interseção";
            DetailsText.Text =
                $"Aviso {conflict.Number}: elemento do link #{conflict.LinkedElementId} ({conflict.LinkedCategory}) " +
                $"{conflict.LinkedFamily} / {conflict.LinkedType} {relation} o elemento host #{conflict.HostElementId} " +
                $"({conflict.HostCategory}) {conflict.HostFamily} / {conflict.HostType}. " +
                $"{resultLabel}: {conflict.IntersectionText}.";
        }

        private void Conflict_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            var row = source == null ? null : System.Windows.Controls.ItemsControl.ContainerFromElement(ConflictsGrid, source) as System.Windows.Controls.DataGridRow;
            if (row?.Item is LinkConflictItem conflict)
                _handler.Request(LinkConflictRequest.ShowHost, conflict);
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_report.Conflicts.Count == 0)
                return;
            ConflictsGrid.SelectedIndex = (ConflictsGrid.SelectedIndex - 1 + _report.Conflicts.Count) % _report.Conflicts.Count;
            ConflictsGrid.ScrollIntoView(ConflictsGrid.SelectedItem);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_report.Conflicts.Count == 0)
                return;
            ConflictsGrid.SelectedIndex = (ConflictsGrid.SelectedIndex + 1) % _report.Conflicts.Count;
            ConflictsGrid.ScrollIntoView(ConflictsGrid.SelectedItem);
        }

        private void SelectHost_Click(object sender, RoutedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;
            _handler.Request(LinkConflictRequest.SelectHost, conflict);
        }

        private void ShowHost_Click(object sender, RoutedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;

            _handler.Request(LinkConflictRequest.ShowHost, conflict);
        }

        private void SelectLinked_Click(object sender, RoutedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;

            _handler.Request(LinkConflictRequest.SelectLinked, conflict);
        }

        private void ShowLinked_Click(object sender, RoutedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;

            _handler.Request(LinkConflictRequest.ShowLinked, conflict);
        }

        private void CreateMarkers_Click(object sender, RoutedEventArgs e)
        {
            string selectedCategory = HostCategoryComboBox.SelectedItem as string;
            string filter = string.Equals(selectedCategory, "Todas as categorias", StringComparison.OrdinalIgnoreCase)
                ? null
                : selectedCategory;
            _handler.Request(LinkConflictRequest.CreateViewMarkers, filter);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            WpfSaveFileDialog dialog = new WpfSaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
                FileName = "conflitos_fisicos_link.csv"
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Numero;IdLink;CategoriaLink;FamiliaLink;TipoLink;IdHost;CategoriaHost;FamiliaHost;TipoHost;VolumeIntersecaoM3;Concluido;VolumeParcial");
                foreach (LinkConflictItem item in _report.Conflicts)
                {
                    builder.AppendLine(string.Join(";", new[]
                    {
                        item.Number.ToString(CultureInfo.InvariantCulture),
                        item.LinkedElementId.ToString(CultureInfo.InvariantCulture),
                        Csv(item.LinkedCategory), Csv(item.LinkedFamily), Csv(item.LinkedType),
                        item.HostElementId.ToString(CultureInfo.InvariantCulture),
                        Csv(item.HostCategory), Csv(item.HostFamily), Csv(item.HostType),
                        item.IntersectionVolumeCubicMeters.ToString("G", CultureInfo.InvariantCulture),
                        item.IsCompleted ? "Sim" : "Não",
                        item.IsPartialVolume ? "Sim" : "Não"
                    }));
                }

                File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
                WpfMessageBox.Show("Conflitos confirmados exportados com sucesso.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (LinkConflictItem conflict in _report.Conflicts)
                conflict.PropertyChanged -= Conflict_PropertyChanged;
            _handler.Close();
            base.OnClosed(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
