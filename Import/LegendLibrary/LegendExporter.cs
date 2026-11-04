using Autodesk.Revit.DB;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Diagnostics;
using System.IO;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendExporter
    {
        private readonly Document _doc;
        private readonly ILogger _logger;

        public LegendExporter(Document doc, ILogger logger)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public GeneratedLegend ExportToActiveSheet(DwgDocumentModel source, LegendComposition composition, LegendSettings settings)
        {
            var stopwatch = Stopwatch.StartNew();
            string folder = CreateTempFolder();
            string dwgPath = new LegendDwgWriter(_logger).Write(source, composition, folder);

            GeneratedLegend legend;
            using (Transaction transaction = new Transaction(_doc, "Gerar legenda CAD"))
            {
                transaction.Start();
                legend = new SheetLegendInserter(_doc, _logger)
                    .InsertOnActiveSheet(_doc.ActiveView, dwgPath, composition, settings);
                transaction.Commit();
            }

            stopwatch.Stop();
            _logger.Info($"Legenda CAD gerada em {stopwatch.ElapsedMilliseconds} ms. Arquivo temporario: {dwgPath}");
            return legend;
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "RevitFamilyImporter",
                "LegendLibrary",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}


