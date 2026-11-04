using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace RevitFamilyImporter.UI
{
    public partial class SheetTitleBlockSelectionWindow : Window
    {
        private readonly ObservableCollection<SheetTitleBlockRow> _rows;
        private readonly ICollectionView _view;
        private bool _suspendSummary;

        public SheetTitleBlockSelectionWindow(UIApplication uiApp, IEnumerable<SheetTitleBlockRow> rows)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);

            _rows = new ObservableCollection<SheetTitleBlockRow>(rows ?? Enumerable.Empty<SheetTitleBlockRow>());
            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = FilterRow;

            GridSheets.ItemsSource = _view;
            UpdateSummary();

            foreach (SheetTitleBlockRow row in _rows)
                row.PropertyChanged += (_, __) =>
                {
                    if (!_suspendSummary)
                        UpdateSummary();
                };
        }

        private bool FilterRow(object item)
        {
            if (item is not SheetTitleBlockRow row) return false;
            string text = TxtSearch.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return true;
            return row.SearchText.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Search_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _view.Refresh();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetVisibleRowsSelected(true);
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            SetVisibleRowsSelected(false);
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!_rows.Any(row => row.Selected))
            {
                System.Windows.MessageBox.Show(this, "Selecione pelo menos uma folha.", "Gerador de Folhas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void UpdateSummary()
        {
            TxtSummary.Text = $"{_rows.Count(row => row.Selected)} de {_rows.Count} folha(s) selecionada(s).";
        }

        private void SetVisibleRowsSelected(bool selected)
        {
            _suspendSummary = true;
            try
            {
                foreach (SheetTitleBlockRow row in _view.Cast<SheetTitleBlockRow>())
                    row.Selected = selected;
            }
            finally
            {
                _suspendSummary = false;
                UpdateSummary();
            }
        }
    }
}
