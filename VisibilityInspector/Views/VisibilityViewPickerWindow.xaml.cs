using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace RevitFamilyImporter.VisibilityInspector.Views
{
    public partial class VisibilityViewPickerWindow : Window
    {
        public VisibilityViewPickerWindow(IEnumerable<ViewOption> views, string currentViewId)
        {
            InitializeComponent();
            int currentId;
            int.TryParse(currentViewId, out currentId);
            ViewsList.ItemsSource = (views ?? Enumerable.Empty<ViewOption>())
                .Where(view => view.Id.IntegerValue != currentId)
                .ToList();
        }

        public ElementId SelectedViewId { get; private set; } = ElementId.InvalidElementId;

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            if (!(ViewsList.SelectedItem is ViewOption option))
            {
                WpfMessageBox.Show(this, "Selecione uma vista.", "Visibility Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedViewId = option.Id;
            DialogResult = true;
        }
    }
}
