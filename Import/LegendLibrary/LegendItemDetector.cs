using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendItemDetector
    {
        public const string DefaultBoundaryLayer = "LIMITE_DETS";

        private readonly ILogger _logger;

        public LegendItemDetector(ILogger logger)
        {
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public static string NormalizeLayerName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultBoundaryLayer
                : value.Trim();
        }

        public List<LegendItem> Detect(DwgDocumentModel document, string boundaryLayer = DefaultBoundaryLayer, string nameLayer = DetailNamingService.OfficialNameLayer)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var stopwatch = Stopwatch.StartNew();
            string layer = NormalizeLayerName(boundaryLayer);
            string normalizedNameLayer = DetailNamingService.NormalizeLayerName(nameLayer, DetailNamingService.OfficialNameLayer);

            List<LegendItem> items = document.Entities
                .Where(entity =>
                    entity.IsPolyline &&
                    string.Equals(entity.Layer, layer, StringComparison.OrdinalIgnoreCase) &&
                    DwgGeometryUtilities.IsClosedPolyline(entity.NativeEntity))
                .Select((entity, index) => CreateBoundaryItem(entity, index + 1))
                .Where(item => item != null)
                .OrderByDescending(item => item.Bounds.MaxY)
                .ThenBy(item => item.Bounds.MinX)
                .ToList();

            List<DwgEntityInfo> candidates = document.Entities
                .Where(entity => !string.Equals(entity.Layer, layer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (LegendItem item in items)
            {
                foreach (DwgEntityInfo entity in candidates)
                {
                    if (!item.Bounds.Contains(entity.Center))
                        continue;

                    if (!DwgGeometryUtilities.PointInPolygon(entity.Center, item.Boundary))
                        continue;

                    item.Entities.Add(entity);
                }

                item.BlockEntity = item.Entities.FirstOrDefault(e => e.Kind == DwgEntityKind.BlockReference);
                item.TextEntity = item.Entities
                    .Where(e => e.IsText && string.Equals(e.Layer, normalizedNameLayer, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.Center.Y)
                    .ThenBy(e => e.Center.X)
                    .FirstOrDefault()
                    ?? item.Entities.FirstOrDefault(e => e.IsText);
                item.Name = CleanName(item.TextEntity?.Text);
            }

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Id = i + 1;
                if (string.IsNullOrWhiteSpace(items[i].Name))
                    items[i].Name = $"ITEM_{items[i].Id:000}";
            }

            int overlapCount = CountOverlaps(items);
            stopwatch.Stop();
            if (overlapCount > 0)
                _logger.Warn($"Foram detectados {overlapCount} limites de legenda sobrepostos no DWG.");

            _logger.Info($"Itens de legenda detectados: {items.Count}; entidades analisadas={document.Entities.Count}; {stopwatch.ElapsedMilliseconds} ms.");
            return items;
        }

        private static LegendItem CreateBoundaryItem(DwgEntityInfo entity, int id)
        {
            List<DwgPoint2> boundary = DwgGeometryUtilities.GetPolylinePoints(entity.NativeEntity);
            if (boundary.Count < 3)
                return null;

            var item = new LegendItem
            {
                Id = id,
                Bounds = DwgBounds.FromPoints(boundary)
            };
            item.Boundary.AddRange(boundary);
            return item;
        }

        private static int CountOverlaps(IList<LegendItem> items)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (items[i].Bounds.Intersects(items[j].Bounds))
                        count++;
                }
            }

            return count;
        }

        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
            while (clean.Contains("  "))
                clean = clean.Replace("  ", " ");

            return clean;
        }
    }
}




