using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RevitFamilyImporter.LinkConflict.Commands;
using RevitFamilyImporter.LinkConflict.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace RevitFamilyImporter.LinkConflict.Views
{
    public partial class LinkConflictSetupWindow : Window
    {
        private readonly LinkConflictExternalEventHandler _handler;
        private bool _ready;
        private bool _transferred;

        public LinkConflictSetupWindow(IReadOnlyList<LinkedModelOption> links, LinkConflictExternalEventHandler handler)
        {
            _handler = handler;
            InitializeComponent();
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            DataContext = new LinkConflictSetupData(links);
            SetLoading(false);
            Loaded += (sender, args) =>
            {
                _ready = true;
                StatusText.Text = "Selecione um link para carregar suas categorias.";
            };
        }

        private void LinkComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            var link = LinkComboBox.SelectedItem as LinkedModelOption;
            CategoriesList.ItemsSource = null;
            if (link == null) return;
            SetLoading(true);
            StatusText.Text = "Carregando categorias do link selecionado...";
            try { _handler.RequestCategories(link); }
            catch (Exception ex) { CategoriesLoadFailed(ex.Message); }
        }

        public void CategoriesLoaded(LinkedModelOption link, IReadOnlyList<LinkedCategoryOption> categories)
        {
            if (!IsLoaded || !ReferenceEquals(LinkComboBox.SelectedItem, link)) return;
            CategoriesList.ItemsSource = categories;
            SetLoading(false);
            StatusText.Text = categories.Count == 0
                ? "Nenhuma categoria de modelo encontrada neste link."
                : $"{categories.Count} categoria(s) encontrada(s). Marque as que deseja analisar.";
        }

        public void CategoriesLoadFailed(string message)
        {
            SetLoading(false);
            LinkComboBox.SelectedIndex = -1;
            StatusText.Text = "Não foi possível carregar as categorias: " + message;
        }

        private void SetLoading(bool loading)
        {
            LinkComboBox.IsEnabled = !loading;
            CategoriesList.IsEnabled = !loading;
            AnalyzeButton.IsEnabled = !loading;
            SelectAllButton.IsEnabled = !loading;
            ClearButton.IsEnabled = !loading;
        }

        private void SetCategorySelection(bool selected)
        {
            if (CategoriesList.ItemsSource is IEnumerable<LinkedCategoryOption> categories)
                foreach (LinkedCategoryOption category in categories) category.IsSelected = selected;
        }
        private void SelectAll_Click(object sender, RoutedEventArgs e) => SetCategorySelection(true);
        private void Clear_Click(object sender, RoutedEventArgs e) => SetCategorySelection(false);

        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            var link = LinkComboBox.SelectedItem as LinkedModelOption;
            var categories = (CategoriesList.ItemsSource as IEnumerable<LinkedCategoryOption> ?? Enumerable.Empty<LinkedCategoryOption>())
                .Where(category => category.IsSelected).ToList();
            if (link == null)
            {
                WpfMessageBox.Show("Selecione um link carregado.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (categories.Count == 0)
            {
                WpfMessageBox.Show("Selecione pelo menos uma categoria do link.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                _handler.RequestAnalyze(link.LinkInstance, categories);
                Hide();
            }
            catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        public void SetAnalysisStatus(string message) => StatusText.Text = message;

        public void CloseForResults() { _transferred = true; Close(); }
        protected override void OnClosed(EventArgs e)
        {
            if (!_transferred) _handler.Close();
            base.OnClosed(e);
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class LinkConflictSetupData
        {
            public LinkConflictSetupData(IReadOnlyList<LinkedModelOption> links) { Links = links; }
            public IReadOnlyList<LinkedModelOption> Links { get; }
        }
    }
}
