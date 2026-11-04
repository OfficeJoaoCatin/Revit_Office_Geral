using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitFamilyImporter.DwgClash.Commands;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;

namespace RevitFamilyImporter.DwgClash.Views
{
    public partial class DwgClashResultsWindow : Window
    {
        private readonly DwgClashExternalEventHandler _eventHandler;

        public DwgClashResultsWindow(DwgClashSettings settings, DwgClashReport report,
            HighlightService highlightService, DwgClashExternalEventHandler eventHandler)
        {
            InitializeComponent();
            Settings = settings;
            Report = report;
            HighlightService = highlightService;
            _eventHandler = eventHandler;
            ResultsGrid.ItemsSource = report.Results;
            SummaryTextBlock.Text = string.Format(
                "Vista: {0} | DWG: {1} | Layer: {2} | Modo: {3} | {4} | Elementos analisados: {5} | Conflitos: {6}",
                report.ViewName, report.ImportName, report.Layer, report.ModeText, report.ClosedRegionText,
                report.ElementsAnalyzed, report.Results.Count);
            StatusTextBlock.Text = report.Results.Count == 0
                ? "Nenhum conflito encontrado com os filtros selecionados."
                : "Selecione uma linha para navegar, selecionar ou destacar o conflito.";
        }

        public DwgClashSettings Settings { get; }
        public DwgClashReport Report { get; }
        public HighlightService HighlightService { get; }

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

        private void HighlightButton_Click(object sender, RoutedEventArgs e)
        {
            _eventHandler.Request(DwgClashRequest.Highlight);
        }

        private void ClearHighlightButton_Click(object sender, RoutedEventArgs e)
        {
            _eventHandler.Request(DwgClashRequest.ClearHighlights);
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
            csv.AppendLine("Numero;Categoria;Familia;Tipo;Id Revit;Layer;Regiao;Distancia;Status");
            foreach (DwgClashResult result in Report.Results)
            {
                csv.AppendLine(string.Join(";", new[]
                {
                    result.Number.ToString(), Escape(result.Category), Escape(result.Family), Escape(result.Type),
                    result.ElementId.ToString(), Escape(result.Layer), Escape(result.RegionName),
                    Escape(result.DistanceText), Escape(result.Status)
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
                status = result.Status
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

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
