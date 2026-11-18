using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class LayerCollector
    {
        private readonly DwgGeometryReader _reader = new DwgGeometryReader();

        public List<DwgLayerOption> GetLayers(Document document, ImportInstance importInstance,
            Action<string, int, int> progress = null, CancellationToken cancellationToken = default)
        {
            return _reader.CountLayers(document, importInstance, progress, cancellationToken)
                .OrderBy(pair => pair.Key, System.StringComparer.OrdinalIgnoreCase)
                .Select(pair => new DwgLayerOption { Name = pair.Key, EntityCount = pair.Value })
                .ToList();
        }
    }
}
