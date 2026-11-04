using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendLibraryImporter
    {
        private readonly ILogger _logger;

        public LegendLibraryImporter(ILogger logger)
        {
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public DwgDocumentModel Document { get; private set; }

        public List<LegendItem> Items { get; private set; } = new List<LegendItem>();

        public void Load(string dwgPath, string boundaryLayer = LegendItemDetector.DefaultBoundaryLayer, string nameLayer = DetailNamingService.OfficialNameLayer)
        {
            var stopwatch = Stopwatch.StartNew();
            Document = new DwgReader(_logger).Read(dwgPath);
            Items = new LegendItemDetector(_logger).Detect(Document, boundaryLayer, nameLayer);
            stopwatch.Stop();
            _logger.Info($"Biblioteca CAD carregada: itens={Items.Count}; {stopwatch.ElapsedMilliseconds} ms.");
        }
    }
}



