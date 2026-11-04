using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.UI;
using WpfMessageBox = System.Windows.MessageBox;

namespace RevitFamilyImporter.VisibilityInspector.Views
{
    public partial class ElementIdInputWindow : Window
    {
        public ElementIdInputWindow(UIApplication uiApp)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);
            Loaded += (s, e) => ElementIdTextBox.Focus();
        }

        public ElementId SelectedElementId { get; private set; } = ElementId.InvalidElementId;

        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            string value = ElementIdTextBox.Text?.Trim() ?? string.Empty;
            if (!int.TryParse(value, out int idValue))
            {
                WpfMessageBox.Show(this, "Informe um ID numérico válido.", "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedElementId = new ElementId(idValue);
            DialogResult = true;
        }
    }
}
