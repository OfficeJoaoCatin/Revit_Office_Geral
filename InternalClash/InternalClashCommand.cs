using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;
using RevitFamilyImporter.LinkConflict.Views;
using RevitFamilyImporter.UI;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using Binding = System.Windows.Data.Binding;
using TextBox = System.Windows.Controls.TextBox;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCell = System.Windows.Controls.DataGridCell;
using CheckBox = System.Windows.Controls.CheckBox;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace RevitFamilyImporter.InternalClash
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class InternalClashCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument ui = data.Application.ActiveUIDocument;
            if (ui?.Document == null) return Result.Cancelled;
            try
            {
                List<LinkedCategoryOption> categories;
                using (var progress = new ClashProgress("Conflitos no Projeto"))
                    categories = new LinkConflictAnalysisService().GetCategories(ui.Document, progress.Report, progress.Token);
                var setup = new InternalClashSetup(categories);
                RevitWindowOwner.Attach(setup, data.Application);
                if (setup.ShowDialog() != true) return Result.Cancelled;
                var selected = categories.Where(c => c.IsSelected).ToList();
                InternalClashReport report;
                using (var progress = new ClashProgress("Conflitos no Projeto"))
                {
                    report = InternalClashService.Analyze(ui.Document, selected, progress.Report, progress.Token);
                    progress.BeginFinishing();
                }
                var handler = new InternalClashNavigation(ui.Document, report);
                var externalEvent = ExternalEvent.Create(handler);
                var results = new InternalClashResults(report, ui.Document,
                    item => { handler.MarkersRequested = false; handler.Item = item; externalEvent.Raise(); },
                    () => { handler.MarkersRequested = true; externalEvent.Raise(); });
                results.Closed += (s, e) => externalEvent.Dispose();
                RevitWindowOwner.Attach(results, data.Application);
                results.Show();
                return Result.Succeeded;
            }
            catch (OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex) { message = ex.Message; TaskDialog.Show("Conflitos no Projeto", ex.Message); return Result.Failed; }
        }
    }

    internal sealed class InternalClashNavigation : IExternalEventHandler
    {
        private readonly Document _document;
        private readonly InternalClashReport _report;
        internal InternalClashItem Item;
        internal bool MarkersRequested;
        internal InternalClashNavigation(Document document, InternalClashReport report) { _document = document; _report = report; }
        public string GetName() => "Selecionar conflito no projeto";
        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument ui = app.ActiveUIDocument;
                if (!_document.IsValidObject) { TaskDialog.Show("Conflitos no Projeto", "O projeto analisado foi fechado. Execute uma nova análise."); return; }
                if (ui?.Document == null || !ui.Document.Equals(_document)) { TaskDialog.Show("Conflitos no Projeto", "Ative o projeto usado na análise para selecionar os elementos."); return; }
                if (MarkersRequested)
                {
                    MarkersRequested = false;
                    if (!(ui.ActiveView is ViewPlan view)) { TaskDialog.Show("Conflitos no Projeto", "Ative uma vista de planta para criar os avisos visuais."); return; }
                    if (_report.Conflicts.Count == 0) { TaskDialog.Show("Conflitos no Projeto", "Não há conflitos para marcar."); return; }
                    var markers = _report.Conflicts.Select(item => new LinkConflictItem { HostElementId = item.FirstId, MarkerPoint = item.MarkerPoint }).ToList();
                    ViewPlan copied = new LinkConflictMarkerService().CreateInCopiedView(_document, view, markers, out int created, "Conflitos no Projeto");
                    ui.ActiveView = copied;
                    TaskDialog.Show("Conflitos no Projeto", $"{created} aviso(s) criado(s) na cópia '{copied.Name}'. A vista original não foi alterada.");
                    return;
                }
                if (Item == null) { TaskDialog.Show("Conflitos no Projeto", "Selecione um conflito na lista."); return; }
                var ids = new List<ElementId> { new ElementId(Item.FirstId), new ElementId(Item.SecondId) };
                if (ids.Any(id => _document.GetElement(id) == null)) { TaskDialog.Show("Conflitos no Projeto", "Um dos elementos não existe mais. Execute uma nova análise."); return; }
                ui.Selection.SetElementIds(ids);
                ui.ShowElements(ids);
            }
            catch (Exception ex) { TaskDialog.Show("Conflitos no Projeto", "Não foi possível selecionar e enquadrar: " + ex.Message); }
        }
    }

    internal sealed class InternalClashSetup : Window
    {
        private readonly IList<LinkedCategoryOption> _categories;
        public InternalClashSetup(IList<LinkedCategoryOption> categories)
        {
            _categories = categories;
            Title = "Conflitos no Projeto"; Width = 570; Height = 620; MinWidth = 450; MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(18) };
            var intro = new TextBlock { Text = "Selecione as categorias do projeto que serão comparadas entre si em todo o modelo, inclusive fora da vista ativa. Pares conectados fisicamente pelos conectores do Revit serão ignorados.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var all = new WpfButton { Content = "Selecionar todas", Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 6, 0) };
            all.Click += (s, e) => { foreach (var c in _categories) c.IsSelected = true; };
            var clear = new WpfButton { Content = "Limpar", Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 6, 0) };
            clear.Click += (s, e) => { foreach (var c in _categories) c.IsSelected = false; };
            var cancel = new WpfButton { Content = "Cancelar", IsCancel = true, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 6, 0) };
            var analyze = new WpfButton { Content = "Analisar", IsDefault = true, Padding = new Thickness(12, 5, 12, 5) };
            analyze.Click += (s, e) => { if (!_categories.Any(c => c.IsSelected)) { WpfMessageBox.Show(this, "Selecione pelo menos uma categoria."); return; } DialogResult = true; };
            bar.Children.Add(all); bar.Children.Add(clear); bar.Children.Add(cancel); bar.Children.Add(analyze);
            DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
            var list = new ListBox { ItemsSource = categories };
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(ContentControl.ContentProperty, new Binding("DisplayName"));
            factory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            list.ItemTemplate = new DataTemplate { VisualTree = factory };
            root.Children.Add(list); Content = root;
        }
    }

    internal sealed class InternalClashResults : Window
    {
        public InternalClashResults(InternalClashReport report, Document document, Action<InternalClashItem> navigate, Action createMarkers)
        {
            Title = "Conflitos no Projeto"; Width = 1100; Height = 680; MinWidth = 650; MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(16) };
            var summary = new TextBlock { Text = $"Elementos: {report.ElementCount:N0} | Pares: {report.PairCount:N0} | Conflitos físicos: {report.Conflicts.Count:N0} | Pares conectados ignorados: {report.ConnectedPairs:N0} | Não verificados: {report.UnverifiedCount:N0}" + (report.UnverifiedCount > 0 ? "\nA análise tem itens não verificados; a ausência de conflitos não está comprovada." : ""), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(summary, Dock.Top); root.Children.Add(summary);
            var memoryStatus = new TextBlock { Text = "Marcações pessoais salvas neste computador; a análise recalcula os conflitos.", Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(memoryStatus, Dock.Top); root.Children.Add(memoryStatus);
            LinkConflictCompletionStore completionStore = null;
            try
            {
                string identity = LinkConflictResultsWindow.GetModelIdentity(document);
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OfficeGeral", "ConflitosNoProjeto", "v1");
                completionStore = new LinkConflictCompletionStore(identity, "interno", "interno", folder);
                foreach (var item in report.Conflicts) item.IsCompleted = completionStore.IsCompleted(item.CompletionKey);
            }
            catch (Exception ex) { completionStore = null; memoryStatus.Text = "Memória local indisponível; marcações valerão somente nesta janela: " + ex.Message; memoryStatus.Foreground = Brushes.DarkRed; }
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var grid = new DataGrid { ItemsSource = report.Conflicts, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow };
            AddColumn(grid, "ID 1", "FirstId"); AddColumn(grid, "Categoria 1", "FirstCategory"); AddColumn(grid, "Elemento 1", "FirstName");
            AddColumn(grid, "ID 2", "SecondId"); AddColumn(grid, "Categoria 2", "SecondCategory"); AddColumn(grid, "Elemento 2", "SecondName");
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.White));
            var selectedRow = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedRow.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#DCEBFA"))));
            rowStyle.Triggers.Add(selectedRow);
            var completedRow = new DataTrigger { Binding = new Binding("IsCompleted"), Value = true };
            completedRow.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#BFE8C9"))));
            rowStyle.Triggers.Add(completedRow); grid.RowStyle = rowStyle;
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.Transparent));
            var selectedCell = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            selectedCell.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.Transparent));
            selectedCell.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.Black));
            cellStyle.Triggers.Add(selectedCell); grid.CellStyle = cellStyle;
            var checkbox = new FrameworkElementFactory(typeof(CheckBox));
            checkbox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkbox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding("IsCompleted") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "Concluído", Width = 90, IsReadOnly = true, CanUserSort = true, SortMemberPath = "IsCompleted", CellTemplate = new DataTemplate { VisualTree = checkbox } });
            bool restoring = false;
            foreach (var item in report.Conflicts)
                item.PropertyChanged += (sender, args) =>
                {
                    if (restoring || args.PropertyName != nameof(InternalClashItem.IsCompleted)) return;
                    var changed = (InternalClashItem)sender;
                    if (completionStore != null)
                    {
                        try { completionStore.SetCompleted(changed.CompletionKey, changed.IsCompleted); }
                        catch (Exception ex)
                        {
                            restoring = true; try { changed.IsCompleted = !changed.IsCompleted; } finally { restoring = false; }
                            memoryStatus.Text = "Não foi possível salvar a marcação: " + ex.Message; memoryStatus.Foreground = Brushes.DarkRed;
                        }
                    }
                    var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                    if (view.SortDescriptions.Any(sort => sort.PropertyName == nameof(InternalClashItem.IsCompleted)))
                        grid.Dispatcher.BeginInvoke(new Action(() => view.Refresh()));
                };
            var show = new WpfButton { Content = "Selecionar e enquadrar", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) };
            var markers = new WpfButton { Content = "Criar avisos na planta", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0), IsEnabled = report.Conflicts.Count > 0 };
            markers.Click += (s, e) => createMarkers();
            void Select() { if (grid.SelectedItem is InternalClashItem item) navigate(item); }
            show.Click += (s, e) => Select(); grid.MouseDoubleClick += (s, e) =>
            {
                System.Windows.DependencyObject current = e.OriginalSource as System.Windows.DependencyObject;
                while (current != null && current != grid)
                {
                    if (current is CheckBox) return;
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
                Select();
            };
            var unverified = new WpfButton { Content = "Ver itens não verificados", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0), IsEnabled = report.UnverifiedCount > 0 };
            unverified.Click += (s, e) =>
            {
                var details = new Window { Title = "Itens não verificados", Width = 850, Height = 600, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                details.Content = new TextBox { Text = $"Itens não verificados: {report.UnverifiedCount:N0}. Exibindo até 10.000 entradas.\r\n" + string.Join("\r\n", report.Unverified), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                details.ShowDialog();
            };
            var close = new WpfButton { Content = "Fechar", Padding = new Thickness(10, 5, 10, 5), IsCancel = true };
            bar.Children.Add(show); bar.Children.Add(markers); bar.Children.Add(unverified); bar.Children.Add(close);
            DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar); root.Children.Add(grid); Content = root;
        }
        private static void AddColumn(DataGrid grid, string title, string path) => grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(path), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
    }
}
