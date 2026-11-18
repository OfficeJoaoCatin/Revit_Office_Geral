using Autodesk.Revit.UI;
using System.Windows;
using System.ComponentModel;

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

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is DetailImporterViewModel vm && vm.IsBusy)
            {
                vm.CancelImport();
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DetailImporterViewModel vm && vm.IsBusy)
            {
                vm.CancelImport();
                return;
            }
            Close();
        }
    }
}


