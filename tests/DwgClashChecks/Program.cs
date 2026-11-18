using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;
using RevitFamilyImporter.LinkConflict.Services;
namespace Autodesk.Revit.DB {
    public class ElementId { public int IntegerValue { get; set; } }
    public class View { public string Name { get; set; } = string.Empty; }
    public class ImportInstance { public string Name { get; set; } = string.Empty; }
}
class Program
{
    [STAThread]
    static void Main()
    {
        DwgRegion region = NewRegion(true, new Point2D(0,0), new Point2D(10,0), new Point2D(10,10), new Point2D(0,10));
        var same = NewRegion(true, new Point2D(10,10), new Point2D(10,0), new Point2D(0,0), new Point2D(0,10));
        var changed = NewRegion(true, new Point2D(0,0), new Point2D(11,0), new Point2D(11,10), new Point2D(0,10));
        string key = DwgClashCompletionKey.ForRegion(region);
        Check(key == DwgClashCompletionKey.ForRegion(same), "A chave mudou após inverter ou girar a ordem dos vértices.");
        Check(key != DwgClashCompletionKey.ForRegion(changed), "Geometrias diferentes receberam a mesma chave.");
        var element = new RevitElementClashInfo { UniqueId="element-1", Center=new Point2D(5,5), MinX=4, MaxX=6, MinY=4, MaxY=6,
            Footprint=new List<Point2D> { new Point2D(4,4), new Point2D(6,4), new Point2D(6,6), new Point2D(4,6) } };
        var intersection = new IntersectionService();
        Check(!intersection.Matches(element, region, DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.BoundaryOnly, .1, out _), "O interior foi considerado com a opção desmarcada.");
        Check(intersection.Matches(element, region, DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.AreaOverlap, .1, out _), "O interior não foi considerado no modo de interseção.");
        Check(!intersection.Matches(element, region, DwgClashAnalysisMode.Center, DwgClashClosedRegionMode.BoundaryOnly, .1, out _), "O centro interno foi considerado com somente bordas.");
        Check(intersection.Matches(element, region, DwgClashAnalysisMode.Center, DwgClashClosedRegionMode.AreaOverlap, .1, out _), "O centro interno não foi considerado.");
        var nearLine = NewRegion(false, new Point2D(6.05,3), new Point2D(6.05,7));
        Check(intersection.Matches(element, nearLine, DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.BoundaryOnly, .1, out _), "A proximidade dentro da tolerância não foi considerada.");
        Check(!intersection.Matches(element, nearLine, DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.BoundaryOnly, .01, out _), "A proximidade fora da tolerância foi considerada.");
        string root = Path.Combine(Path.GetTempPath(), "office-dwg-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LinkConflictCompletionStore("project", "view", "dwg", root);
            store.SetCompleted(element.UniqueId + "|" + key, true);
            Check(new LinkConflictCompletionStore("project", "view", "dwg", root).IsCompleted(element.UniqueId + "|" + DwgClashCompletionKey.ForRegion(same)), "A marcação não foi recuperada.");
            Check(!new LinkConflictCompletionStore("project", "other-view", "dwg", root).IsCompleted(element.UniqueId + "|" + key), "Vistas distintas compartilharam marcação.");
            Check(!new LinkConflictCompletionStore("project", "view", "other-dwg", root).IsCompleted(element.UniqueId + "|" + key), "DWGs distintos compartilharam marcação.");
            var reopened = new LinkConflictCompletionStore("project", "view", "dwg", root);
            reopened.SetCompleted(element.UniqueId + "|" + key, false);
            Check(!new LinkConflictCompletionStore("project", "view", "dwg", root).IsCompleted(element.UniqueId + "|" + key), "A desmarcação não foi persistida.");
        }
        finally { if (Directory.Exists(root) && root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true); }
        string xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DwgClashResultsWindow.txt"));
        xml = Regex.Replace(xml, @"\s+x:Class=""[^""]*""", "");
        xml = Regex.Replace(xml, @"\s+(?:SelectionChanged|MouseDoubleClick|PreviewMouseDoubleClick|Click)=""[^""]*""", "");
        var window = (Window)XamlReader.Parse(xml);
        var grid = (DataGrid)window.FindName("ResultsGrid");
        var column = grid.Columns.OfType<DataGridTemplateColumn>().Last();
        Check(column.CanUserSort && column.SortMemberPath == "IsCompleted", "A coluna não permite ordenação.");
        var done = new DwgClashResult { IsCompleted = true };
        var pending = new DwgClashResult();
        Check(done.Status == "CONCLUÍDO" && pending.Status == "CLASH", "O status não corresponde à marcação.");
        var list = new List<DwgClashResult> { pending, done };
        grid.ItemsSource = list;
        var view = CollectionViewSource.GetDefaultView(list);
        view.SortDescriptions.Add(new SortDescription("IsCompleted", ListSortDirection.Descending));
        Check(ReferenceEquals(view.Cast<object>().First(), done), "Concluídos não foram para o topo.");
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription("IsCompleted", ListSortDirection.Ascending));
        Check(ReferenceEquals(view.Cast<object>().First(), pending), "Concluídos não foram para o fim.");
        var row = new DataGridRow { DataContext=done, Style=grid.RowStyle };
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => {}, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Check(((SolidColorBrush)row.Background).Color == (Color)ColorConverter.ConvertFromString("#BFE8C9"), "A linha concluída não ficou verde.");
        SpatialIndexChecks.Run();
        Console.WriteLine("Geometria, tolerância, memória, agrupamento, linha verde e índice espacial verificados.");
    }
    static DwgRegion NewRegion(bool closed, params Point2D[] points) => new DwgRegion { Layer="layer", IsClosed=closed, Points=points.ToList() };
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
