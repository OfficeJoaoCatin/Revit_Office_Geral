using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace RevitFamilyImporter.UI.LegendLibrary
{
    public partial class LegendBuilderWindow : Window
    {
        public LegendBuilderWindow(UIApplication uiApp)
        {
            InitializeComponent();
            var viewModel = new LegendBuilderViewModel(uiApp);
            viewModel.RequestClose += ViewModel_RequestClose;
            DataContext = viewModel;

            IntPtr handle = uiApp?.MainWindowHandle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
                new WindowInteropHelper(this).Owner = handle;
        }

        private void ViewModel_RequestClose(object sender, EventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}


