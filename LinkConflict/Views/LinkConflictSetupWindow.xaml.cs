using System.Collections.Generic;
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

        public LinkConflictSetupWindow(IReadOnlyList<LinkedModelOption> links, LinkConflictExternalEventHandler handler)
        {
            _handler = handler;
            InitializeComponent();
            DataContext = new LinkConflictSetupData(links);
            if (LinkComboBox.Items.Count > 0)
                LinkComboBox.SelectedIndex = 0;
        }

        public LinkedModelOption SelectedLink { get; private set; }
        public LinkedCategoryOption SelectedCategory { get; private set; }

        private void LinkComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedLink = LinkComboBox.SelectedItem as LinkedModelOption;
            CategoryComboBox.ItemsSource = SelectedLink?.Categories;
            CategoryComboBox.SelectedIndex = CategoryComboBox.Items.Count > 0 ? 0 : -1;
        }

        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            SelectedLink = LinkComboBox.SelectedItem as LinkedModelOption;
            SelectedCategory = CategoryComboBox.SelectedItem as LinkedCategoryOption;
            if (SelectedLink == null)
            {
                WpfMessageBox.Show("Selecione um link carregado.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedCategory == null)
            {
                WpfMessageBox.Show("Selecione uma categoria do link.", "Conflitos em Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Hide();
            _handler.RequestAnalyze(SelectedLink.LinkInstance, SelectedCategory);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
            _handler.Close();
        }

        private sealed class LinkConflictSetupData
        {
            public LinkConflictSetupData(IReadOnlyList<LinkedModelOption> links)
            {
                Links = links;
            }

            public IReadOnlyList<LinkedModelOption> Links { get; }
        }
    }
}
