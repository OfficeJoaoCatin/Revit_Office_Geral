using Autodesk.Revit.UI;
using RevitFamilyImporter.UI;
using RevitFamilyImporter.VisibilityInspector.ViewModels;
using System.Windows;

namespace RevitFamilyImporter.VisibilityInspector.Views
{
    public partial class VisibilityInspectorWindow : Window
    {
        public VisibilityInspectorWindow(UIApplication uiApp, VisibilityInspectorVM viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            RevitWindowOwner.Attach(this, uiApp);
        }
    }
}
