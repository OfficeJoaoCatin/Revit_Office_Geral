using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class DetailExtractor
    {
        private readonly ILogger _logger;

        public DetailExtractor(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public void Extract(
            DwgDocumentModel document,
            IList<DetailRegion> regions,
            string boundaryLayer = BoundaryDetector.DefaultBoundaryLayer)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (regions == null || regions.Count == 0)
                return;

            var stopwatch = Stopwatch.StartNew();
            double cellSize = EstimateCellSize(regions);
            double tolerance = Math.Max(cellSize * 0.002, 0.01);
            List<DwgEntityInfo> candidates = document.Entities
                .Where(entity =>
                    !string.Equals(entity.Layer, boundaryLayer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var index = new SpatialEntityIndex(candidates, cellSize);

            foreach (DetailRegion region in regions)
            {
                region.Entities.Clear();
                if (region.BoundaryEntity != null)
                    region.Entities.Add(region.BoundaryEntity);

                DwgBounds expandedBounds = region.Bounds.Expand(tolerance);
                foreach (DwgEntityInfo entity in index.Query(expandedBounds))
                {
                    if (!expandedBounds.Intersects(entity.Bounds))
                        continue;

                    if (!IsInsideOrCrossingRegion(entity, region, tolerance))
                        continue;

                    region.Entities.Add(entity);
                }
            }

            stopwatch.Stop();
            _logger.Info($"Entidades extraidas para {regions.Count} detalhes em {stopwatch.ElapsedMilliseconds} ms.");
        }

        private static double EstimateCellSize(IList<DetailRegion> regions)
        {
            double width = regions.Select(r => Math.Max(r.Bounds.Width, 1.0)).DefaultIfEmpty(1.0).Average();
            double height = regions.Select(r => Math.Max(r.Bounds.Height, 1.0)).DefaultIfEmpty(1.0).Average();
            return Math.Max(1.0, Math.Min(width, height));
        }

        private static bool IsInsideOrCrossingRegion(DwgEntityInfo entity, DetailRegion region, double tolerance)
        {
            if (DwgGeometryUtilities.PointInPolygon(entity.Center, region.Boundary))
                return true;

            DwgBounds bounds = entity.Bounds.Expand(tolerance);
            var corners = new[]
            {
                new DwgPoint2(bounds.MinX, bounds.MinY),
                new DwgPoint2(bounds.MinX, bounds.MaxY),
                new DwgPoint2(bounds.MaxX, bounds.MinY),
                new DwgPoint2(bounds.MaxX, bounds.MaxY)
            };

            if (corners.Any(point => DwgGeometryUtilities.PointInPolygon(point, region.Boundary)))
                return true;

            if (entity.Points.Any(point => region.Bounds.Expand(tolerance).Contains(point) &&
                                           DwgGeometryUtilities.PointInPolygon(point, region.Boundary)))
            {
                return true;
            }

            return region.Bounds.Expand(tolerance).Intersects(bounds);
        }
    }
}


