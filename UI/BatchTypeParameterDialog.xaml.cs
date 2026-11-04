using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RevitFamilyImporter.UI
{
    public partial class BatchTypeParameterDialog : Window
    {
        public string ParameterName => CmbParameterName.SelectedItem as string ?? string.Empty;
        public string ParameterValue => TxtValue.Text?.Trim();

        public BatchTypeParameterDialog(UIApplication uiApp, IEnumerable<string> parameterNames)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);

            List<string> items = (parameterNames ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            CmbParameterName.ItemsSource = items;
            if (items.Count > 0)
                CmbParameterName.SelectedIndex = 0;

            TxtParameterSummary.Text = $"{items.Count} parâmetro(s) editável(is) encontrados na seleção.";
            Loaded += (s, e) => CmbParameterName.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ParameterName))
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Selecione um parâmetro.",
                    "Editar Parâmetros",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}
