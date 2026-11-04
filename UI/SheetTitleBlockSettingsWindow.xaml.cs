using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RevitFamilyImporter.UI
{
    public partial class SheetTitleBlockSettingsWindow : Window
    {
        public string ExcelPath => TxtExcelPath.Text?.Trim() ?? string.Empty;
        public string Discipline => (CmbDiscipline.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Eletrica";
        public bool UpdateExistingOnly => ChkUpdateExistingOnly.IsChecked == true;
        public bool SelectSpecificSheets => ChkSelectSheets.IsChecked == true;
        public FamilySymbol? SelectedTitleBlock => (CmbTitleBlock.SelectedItem as TitleBlockChoice)?.Symbol;

        public SheetTitleBlockSettingsWindow(UIApplication uiApp, IEnumerable<FamilySymbol> titleBlockTypes)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);

            CmbTitleBlock.ItemsSource = (titleBlockTypes ?? Enumerable.Empty<FamilySymbol>())
                .Select(symbol => new TitleBlockChoice(symbol))
                .ToList();

            if (CmbTitleBlock.Items.Count > 0)
                CmbTitleBlock.SelectedIndex = 0;

            UpdateExistingOnly_Changed(this, new RoutedEventArgs());
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecione a Lista Mestra",
                Filter = "Planilhas Excel (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|Todos os arquivos (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
                TxtExcelPath.Text = dialog.FileName;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExcelPath))
            {
                System.Windows.MessageBox.Show(this, "Selecione a Lista Mestra.", "Gerador de Folhas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!UpdateExistingOnly && SelectedTitleBlock == null)
            {
                System.Windows.MessageBox.Show(this, "Selecione a familia/tipo do carimbo.", "Gerador de Folhas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void UpdateExistingOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (CmbTitleBlock == null || ChkSelectSheets == null || TxtTitleBlockLabel == null)
                return;

            bool updateOnly = UpdateExistingOnly;
            CmbTitleBlock.IsEnabled = !updateOnly;
            TxtTitleBlockLabel.Opacity = updateOnly ? 0.45 : 1.0;
            CmbTitleBlock.Opacity = updateOnly ? 0.45 : 1.0;

            if (updateOnly)
                ChkSelectSheets.IsChecked = false;

            ChkSelectSheets.IsEnabled = !updateOnly;
            ChkSelectSheets.Opacity = updateOnly ? 0.45 : 1.0;
        }

        private class TitleBlockChoice
        {
            public TitleBlockChoice(FamilySymbol symbol)
            {
                Symbol = symbol;
                DisplayName = $"{symbol.FamilyName} - {symbol.Name}";
            }

            public FamilySymbol Symbol { get; }
            public string DisplayName { get; }
        }
    }
}
