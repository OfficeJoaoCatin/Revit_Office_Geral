using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class DwgReader
    {
        private readonly ILogger _logger;

        public DwgReader(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public DwgDocumentModel Read(string path, DetailImportResult result = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Informe o caminho do arquivo DWG.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("Arquivo DWG não encontrado.", path);

            var stopwatch = Stopwatch.StartNew();
            CadDocument cadDocument = ACadSharp.IO.DwgReader.Read(path, OnNotification);

            var model = new DwgDocumentModel
            {
                FilePath = path,
                NativeDocument = cadDocument
            };

            model.Layers.AddRange(
                cadDocument.Layers
                    .Select(layer => layer.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name));

            model.Blocks.AddRange(
                cadDocument.BlockRecords
                    .Select(block => block.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name));

            int index = 0;
            foreach (Entity entity in cadDocument.Entities)
            {
                DwgBounds bounds = DwgGeometryUtilities.GetBounds(entity);
                var info = new DwgEntityInfo
                {
                    Index = index++,
                    Handle = entity.Handle.ToString(),
                    Layer = entity.Layer?.Name ?? "0",
                    Kind = DwgGeometryUtilities.GetKind(entity),
                    Bounds = bounds,
                    Center = bounds.Center,
                    Text = DwgGeometryUtilities.GetText(entity),
                    NativeEntity = entity
                };

                info.Points.AddRange(DwgGeometryUtilities.GetGeometryPoints(entity));
                model.Entities.Add(info);

                if (info.IsText)
                    model.Texts.Add(info);
            }

            stopwatch.Stop();
            if (result != null)
                result.ReadMilliseconds = stopwatch.ElapsedMilliseconds;

            _logger.Info($"DWG lido: {path}; entidades={model.Entities.Count}; layers={model.Layers.Count}; blocos={model.Blocks.Count}; {stopwatch.ElapsedMilliseconds} ms.");
            return model;
        }

        private void OnNotification(object sender, NotificationEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Message))
                return;

            _logger.Warn(e.Message);
        }
    }
}


