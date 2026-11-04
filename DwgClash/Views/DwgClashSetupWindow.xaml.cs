using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Commands;

namespace RevitFamilyImporter.DwgClash.Views
{
    public partial class DwgClashSetupWindow : Window
    {
        private readonly Autodesk.Revit.DB.View _view;
        private readonly List<DwgImportOption> _imports;
        private readonly DwgClashExternalEventHandler _eventHandler;

        public DwgClashSetupWindow(Autodesk.Revit.DB.View view, IList<DwgImportOption> imports,
            IList<DwgCategoryOption> categoryOptions, DwgClashExternalEventHandler eventHandler)
        {
            InitializeComponent();
            _view = view;
            _imports = imports.ToList();
            _eventHandler = eventHandler;
            CategoryOptions = categoryOptions;
            DataContext = this;
            DwgComboBox.ItemsSource = _imports;
            if (_imports.Count > 0)
                DwgComboBox.SelectedIndex = 0;
        }

        public IList<DwgCategoryOption> CategoryOptions { get; }

        public ImportInstance SelectedImport =>
            (DwgComboBox.SelectedItem as DwgImportOption)?.ImportInstance;

        public DwgClashSettings BuildSettings()
        {
            if (!(DwgComboBox.SelectedItem is DwgImportOption importOption) ||
                LayerComboBox.SelectedItem is not DwgLayerOption layer)
                throw new InvalidOperationException("Selecione um DWG e um layer antes de analisar.");

            if (!double.TryParse(ToleranceTextBox.Text.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double toleranceMm) || toleranceMm < 0)
                throw new InvalidOperationException("A tolerância deve ser um número maior ou igual a zero.");

            List<ElementId> categoryIds = CategoryOptions.Where(option => option.IsSelected)
                .Select(option => option.CategoryId)
                .ToList();
            if (categoryIds.Count == 0)
                throw new InvalidOperationException("Marque pelo menos uma categoria Revit.");

            return new DwgClashSettings
            {
                View = _view,
                ImportInstance = importOption.ImportInstance,
                Layer = layer.Name,
                ToleranceFeet = toleranceMm / 304.8,
                Mode = CenterRadio.IsChecked == true ? DwgClashAnalysisMode.Center : DwgClashAnalysisMode.Intersection,
                ClosedRegionMode = ClosedRegionAreaCheckBox.IsChecked == true
                    ? DwgClashClosedRegionMode.AreaOverlap
                    : DwgClashClosedRegionMode.BoundaryOnly,
                CategoryIds = categoryIds
            };
        }

        private void DwgComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SelectedImport != null)
                _eventHandler.Request(DwgClashRequest.LoadLayers);
        }

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BuildSettings();
                _eventHandler.Request(DwgClashRequest.Analyze);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Configuração incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void SetLayers(IList<DwgLayerOption> layers)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LayerComboBox.ItemsSource = layers;
                LayerComboBox.SelectedIndex = layers.Count > 0 ? 0 : -1;
                if (layers.Count == 0)
                    ShowValidationError("Nenhum layer foi encontrado na geometria do DWG selecionado.");
            }));
        }

        public void ShowValidationError(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                System.Windows.MessageBox.Show(this, message, "Clash 2D DWG x Revit",
                    MessageBoxButton.OK, MessageBoxImage.Warning)));
        }
    }
}
