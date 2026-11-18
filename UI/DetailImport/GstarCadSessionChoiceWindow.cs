using Autodesk.Revit.UI;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RevitFamilyImporter.UI.DetailImport
{
    internal sealed class GstarCadSessionChoiceWindow : Window
    {
        private GstarCadSessionChoiceWindow(string sessions)
        {
            Title = "Importar Detalhes DWG";
            Width = 540;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(20) };
            Content = panel;
            panel.Children.Add(new TextBlock
            {
                Text = "Há uma sessão do GstarCAD em execução (" + sessions + ").\n\n" +
                    "Para usar a alta fidelidade, cancele a importação, salve seu trabalho e feche o GstarCAD.\n\n" +
                    "Você também pode continuar com o motor interno, sujeito a diferenças gráficas.",
                TextWrapping = TextWrapping.Wrap
            });

            var buttons = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(buttons);
            var cancel = new System.Windows.Controls.Button
            {
                Content = "Cancelar importação",
                IsCancel = true,
                IsDefault = true,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancel.Click += (sender, args) => DialogResult = false;
            buttons.Children.Add(cancel);
            var proceed = new System.Windows.Controls.Button
            {
                Content = "Continuar com motor interno",
                Padding = new Thickness(12, 6, 12, 6)
            };
            proceed.Click += (sender, args) => DialogResult = true;
            buttons.Children.Add(proceed);
        }

        public static bool Confirm(UIApplication uiApp, string sessions)
        {
            var dialog = new GstarCadSessionChoiceWindow(sessions);
            Window? owner = System.Windows.Application.Current?.Windows.OfType<DetailImporterWindow>().FirstOrDefault();
            if (owner != null)
                dialog.Owner = owner;
            else
                RevitWindowOwner.Attach(dialog, uiApp);
            return dialog.ShowDialog() == true;
        }
    }
}
