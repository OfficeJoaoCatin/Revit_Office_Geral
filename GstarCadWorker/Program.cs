using System;
using System.Globalization;
using System.Text;
using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;

internal static class Program
{
    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    [STAThread]
    private static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        var logger = new Logger("DetailImportWorker");
        using (var exporter = new GstarCadDetailExporter(logger))
        {
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (line == "QUIT") break;
                try
                {
                    string[] p = line.Split('|');
                    if (p.Length != 11 || p[0] != "EXPORT") throw new InvalidOperationException("Solicitação inválida.");
                    var source = new DwgDocumentModel { FilePath = Decode(p[1]) };
                    var region = new DetailRegion { Id = int.Parse(p[2], CultureInfo.InvariantCulture), Name = Decode(p[3]), Bounds = new DwgBounds(Number(p[4]), Number(p[5]), Number(p[6]), Number(p[7])) };
                    string folder = Decode(p[8]);
                    int max = int.Parse(p[9], CultureInfo.InvariantCulture);
                    if (p[10] != "1") throw new InvalidOperationException("Versão de protocolo inválida.");
                    string path;
                    if (exporter.TryExport(source, region, folder, "", "", max, out path))
                        Console.WriteLine("OK|" + Encode(path));
                    else
                        Console.WriteLine("ERR|" + Encode("A exportação do GstarCAD falhou. Confira o log DetailImportWorker."));
                }
                catch (Exception ex)
                {
                    logger.Error("Falha no auxiliar de exportação.", ex);
                    Console.WriteLine("ERR|" + Encode(ex.Message));
                }
                Console.Out.Flush();
            }
        }
    }
}
