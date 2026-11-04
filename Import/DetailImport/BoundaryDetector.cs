using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class BoundaryDetector
    {
        public const string DefaultBoundaryLayer = "LIMITE_DETS";

        private readonly ILogger _logger;

        public BoundaryDetector(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public static string NormalizeLayerName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultBoundaryLayer
                : value.Trim();
        }

        public List<DetailRegion> Detect(
            DwgDocumentModel document,
            string boundaryLayer = DefaultBoundaryLayer,
            DetailImportResult result = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var stopwatch = Stopwatch.StartNew();
            List<DetailRegion> regions = new List<DetailRegion>();
            string layerName = string.IsNullOrWhiteSpace(boundaryLayer)
                ? DefaultBoundaryLayer
                : boundaryLayer.Trim();

            foreach (DwgEntityInfo entity in document.Entities)
            {
                if (!entity.IsPolyline)
                    continue;

                if (!string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!DwgGeometryUtilities.IsClosedPolyline(entity.NativeEntity))
                    continue;

                List<DwgPoint2> boundary = DwgGeometryUtilities.GetPolylinePoints(entity.NativeEntity);
                if (boundary.Count < 3)
                    continue;

                var region = new DetailRegion
                {
                    Id = regions.Count + 1,
                    BoundaryEntity = entity,
                    Bounds = DwgBounds.FromPoints(boundary),
                    Center = DwgBounds.FromPoints(boundary).Center
                };
                region.Boundary.AddRange(boundary);
                regions.Add(region);
            }

            regions = regions
                .OrderByDescending(r => r.Bounds.MaxY)
                .ThenBy(r => r.Bounds.MinX)
                .ToList();

            for (int i = 0; i < regions.Count; i++)
                regions[i].Id = i + 1;

            stopwatch.Stop();
            if (result != null)
                result.DetectionMilliseconds = stopwatch.ElapsedMilliseconds;

            _logger.Info($"Limites detectados no layer {layerName}: {regions.Count}; {stopwatch.ElapsedMilliseconds} ms.");
            return regions;
        }
    }
}


