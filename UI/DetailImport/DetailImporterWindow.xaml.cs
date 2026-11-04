using Autodesk.Revit.UI;
using System.Windows;

namespace RevitFamilyImporter.UI.DetailImport
{
    public partial class DetailImporterWindow : Window
    {
        public DetailImporterWindow(UIApplication uiApp)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);
            DataContext = new DetailImporterViewModel(uiApp);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}


