using System;
using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace RevitFamilyImporter.LinkConflict.Views
{
    internal sealed class LinkConflictDiagnosticWindow : Window
    {
        public LinkConflictDiagnosticWindow(string summary, string title = null, string headingText = null)
        {
            Title = title ?? "Diagnóstico parcial — Conflitos em Links";
            Width = 850; Height = 650; MinWidth = 500; MinHeight = 360;
            MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new WpfTextBlock { Text = headingText ?? "Amostra parcial para diagnóstico. Este não é um relatório completo de conflitos.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold };
            grid.Children.Add(heading);
            var output = new WpfTextBox { Text = summary ?? string.Empty, IsReadOnly = true, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(output, 1); grid.Children.Add(output);
            var footer = new WpfStackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0) };
            var copy = new WpfButton { Content = "Copiar resumo", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
            copy.Click += (sender, args) =>
            {
                try { System.Windows.Clipboard.SetText(output.Text); }
                catch (Exception ex) { System.Windows.MessageBox.Show("Não foi possível copiar: " + ex.Message, "Diagnóstico", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            var close = new WpfButton { Content = "Fechar", Padding = new Thickness(12, 6, 12, 6) };
            close.Click += (sender, args) => Close();
            footer.Children.Add(copy); footer.Children.Add(close);
            Grid.SetRow(footer, 2); grid.Children.Add(footer);
            Content = grid;
        }
    }
}
