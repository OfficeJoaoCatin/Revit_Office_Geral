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
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;
namespace Autodesk.Revit.DB { public class XYZ {} }
class Program
{
    [STAThread]
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "office-link-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new BoundedLruCache<int, string>(2);
            int created = 0;
            string Get(int id) => cache.GetOrAdd(id, () => { created++; return id.ToString(); });
            Get(1); Get(2); Get(1); Get(3); Get(1);
            Check(created == 3, "A cache descartou um elemento utilizado recentemente.");
            Get(2);
            Check(created == 4, "A cache não descartou o elemento menos recente.");
            try { cache.GetOrAdd(4, () => throw new InvalidOperationException()); } catch (InvalidOperationException) { }
            Get(2); Get(1);
            Check(created == 4, "Falha na criação alterou os elementos existentes da cache.");
            var store = new LinkConflictCompletionStore("host", "linked", "link-1", root);
            Check(!store.IsCompleted("category|linked-0|host-0"), "Memória vazia não deve marcar conflitos.");
            bool rejectedInvalidKey = false;
            try { store.IsCompleted(""); } catch (ArgumentException) { rejectedInvalidKey = true; }
            Check(rejectedInvalidKey, "Memória vazia aceitou chave inválida.");
            store.SetCompleted("category|linked-1|host-1", true);
            store.SetCompleted("category|linked-2|host-2", true);
            var reopened = new LinkConflictCompletionStore("host", "linked", "link-1", root);
            Check(reopened.IsCompleted("category|linked-1|host-1"), "A marcaÃ§Ã£o nÃ£o foi recuperada.");
            Check(!new LinkConflictCompletionStore("host", "linked", "link-2", root).IsCompleted("category|linked-1|host-1"), "VÃ­nculos diferentes compartilharam marcaÃ§Ã£o.");
            Check(!new LinkConflictCompletionStore("other-host", "linked", "link-1", root).IsCompleted("category|linked-1|host-1"), "Projetos diferentes compartilharam marcaÃ§Ã£o.");
            Check(!new LinkConflictCompletionStore("host", "other-linked", "link-1", root).IsCompleted("category|linked-1|host-1"), "Modelos vinculados diferentes compartilharam marcaÃ§Ã£o.");
            reopened.SetCompleted("category|linked-1|host-1", false);
            var again = new LinkConflictCompletionStore("host", "linked", "link-1", root);
            Check(!again.IsCompleted("category|linked-1|host-1") && again.IsCompleted("category|linked-2|host-2"), "DesmarcaÃ§Ã£o removeu a marcaÃ§Ã£o errada.");
            string setupXml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LinkConflictSetupWindow.txt"));
            setupXml = Regex.Replace(setupXml, @"\s+x:Class=""[^""]*""", "");
            setupXml = Regex.Replace(setupXml, @"\s+(?:SelectionChanged|Click)=""[^""]*""", "");
            var setupWindow = (Window)XamlReader.Parse(setupXml);
            var diagnosticOption = (CheckBox)setupWindow.FindName("DiagnosticModeCheckBox");
            Check(diagnosticOption != null && diagnosticOption.IsChecked == false, "Diagnóstico deve ser opcional e iniciar desmarcado.");
            var startField = (TextBox)setupWindow.FindName("DiagnosticStartText");
            Check(startField != null && startField.Text == "1" && !startField.IsEnabled, "Posição inicial deve começar em 1 e ficar desativada fora do diagnóstico.");
            diagnosticOption.IsChecked = true;
            Check(startField.IsEnabled, "Posição inicial deve ser ativada no diagnóstico.");
            string xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LinkConflictResultsWindow.txt"));
            xml = Regex.Replace(xml, @"\s+x:Class=""[^""]*""", "");
            xml = Regex.Replace(xml, @"\s+(?:SelectionChanged|MouseDoubleClick|PreviewMouseDoubleClick|Click)=""[^""]*""", "");
            var window = (Window)XamlReader.Parse(xml);
            var unverifiedButton = (Button)window.FindName("UnverifiedButton");
            var unverifiedStatus = (TextBlock)window.FindName("UnverifiedStatusText");
            Check(unverifiedButton != null && unverifiedStatus != null && unverifiedButton.Visibility == System.Windows.Visibility.Collapsed && unverifiedStatus.Visibility == System.Windows.Visibility.Collapsed,
                "Itens não verificados devem ter controles separados, ocultos quando não há ocorrências.");
            var grid = (DataGrid)window.FindName("ConflictsGrid");
            var column = grid.Columns.OfType<DataGridTemplateColumn>().Last();
            Check(column.CanUserSort && column.SortMemberPath == "IsCompleted", "A coluna nÃ£o estÃ¡ configurada para ordenaÃ§Ã£o.");
            var unfinished = new LinkConflictItem { Number = 1, IsCompleted = false };
            var completed = new LinkConflictItem { Number = 2, IsCompleted = true };
            var list = new List<LinkConflictItem> { unfinished, completed };
            grid.ItemsSource = list;
            var view = CollectionViewSource.GetDefaultView(list);
            view.SortDescriptions.Add(new SortDescription(column.SortMemberPath, ListSortDirection.Descending));
            Check(ReferenceEquals(view.Cast<object>().First(), completed), "ConcluÃ­dos nÃ£o foram agrupados no topo.");
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(column.SortMemberPath, ListSortDirection.Ascending));
            Check(ReferenceEquals(view.Cast<object>().First(), unfinished), "ConcluÃ­dos nÃ£o foram agrupados ao final.");
            Console.WriteLine("MemÃ³ria local, isolamento por projeto/vÃ­nculo e ordenaÃ§Ã£o verificados.");
        }
        finally
        {
            if (Directory.Exists(root) && root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
