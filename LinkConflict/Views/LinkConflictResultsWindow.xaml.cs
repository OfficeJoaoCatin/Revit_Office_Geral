using System;
using System.Collections.Generic;
using System.Globalization;
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

        public LinkConflictResultsWindow(UIApplication uiApp, RevitLinkInstance linkInstance, LinkConflictReport report, LinkConflictExternalEventHandler handler)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _linkInstance = linkInstance ?? throw new ArgumentNullException(nameof(linkInstance));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            InitializeComponent();
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
            SummaryText.Text = $"Link: {_report.LinkName} | Categoria: {_report.CategoryName} | " +
                               $"Elementos analisados: {_report.LinkedElementCount:N0} | " +
                               $"Avisos encontrados: {_report.Conflicts.Count:N0}";

            if (_report.Conflicts.Count > 0)
                ConflictsGrid.SelectedIndex = 0;
            else
                DetailsText.Text = "Nenhuma interseção geométrica foi encontrada para a categoria selecionada.";
        }

        private LinkConflictItem SelectedConflict => ConflictsGrid.SelectedItem as LinkConflictItem;

        public void SetMarkerStatus(string message)
        {
            DetailsText.Text = message;
        }

        private void Conflict_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LinkConflictItem conflict = SelectedConflict;
            if (conflict == null)
                return;

            DetailsText.Text =
                $"Aviso {conflict.Number}: elemento do link #{conflict.LinkedElementId} ({conflict.LinkedCategory}) " +
                $"{conflict.LinkedFamily} / {conflict.LinkedType} intersecta o elemento host #{conflict.HostElementId} " +
                $"({conflict.HostCategory}) {conflict.HostFamily} / {conflict.HostType}. " +
                $"Volume estimado: {conflict.IntersectionText}.";
        }

        private void Conflict_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectHost_Click(sender, e);
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
                FileName = "avisos_conflitos_link.csv"
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Numero;IdLink;CategoriaLink;FamiliaLink;TipoLink;IdHost;CategoriaHost;FamiliaHost;TipoHost;VolumeIntersecaoM3");
                foreach (LinkConflictItem item in _report.Conflicts)
                {
                    builder.AppendLine(string.Join(";", new[]
                    {
                        item.Number.ToString(CultureInfo.InvariantCulture),
                        item.LinkedElementId.ToString(CultureInfo.InvariantCulture),
                        Csv(item.LinkedCategory), Csv(item.LinkedFamily), Csv(item.LinkedType),
                        item.HostElementId.ToString(CultureInfo.InvariantCulture),
                        Csv(item.HostCategory), Csv(item.HostFamily), Csv(item.HostType),
                        item.IntersectionVolumeCubicMeters.ToString("G", CultureInfo.InvariantCulture)
                    }));
                }

                File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
                WpfMessageBox.Show("Avisos exportados com sucesso.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _handler.Close();
            Close();
        }
    }
}
