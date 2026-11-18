using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class IntersectionService
    {
        private readonly PointInPolygonService _pointService = new PointInPolygonService();

        public bool Matches(
            RevitElementClashInfo element,
            DwgRegion region,
            DwgClashAnalysisMode mode,
            DwgClashClosedRegionMode closedRegionMode,
            double tolerance,
            out double distance,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            distance = double.MaxValue;
            if (element == null || region == null || !BoundingBoxesOverlap(element, region, tolerance))
                return false;

            if (mode == DwgClashAnalysisMode.Center)
            {
                bool insideArea = closedRegionMode == DwgClashClosedRegionMode.AreaOverlap &&
                    _pointService.Contains(element.Center, region, tolerance, cancellationToken);
                distance = insideArea
                    ? 0
                    : _pointService.DistanceToPolyline(element.Center, region.Points, region.IsClosed, cancellationToken);
                return distance <= tolerance;
            }

            if (closedRegionMode == DwgClashClosedRegionMode.AreaOverlap && region.IsClosed &&
                (element.Footprint.Any(point => _pointService.Contains(point, region, tolerance, cancellationToken)) ||
                 _pointService.Contains(region.Points[0], new DwgRegion
                 {
                     IsClosed = true,
                     Points = element.Footprint
                 }, tolerance, cancellationToken)))
            {
                distance = 0;
                return true;
            }

            for (int index = 0; index < element.Footprint.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Point2D start = element.Footprint[index];
                Point2D end = element.Footprint[(index + 1) % element.Footprint.Count];
                for (int regionIndex = 0; regionIndex < region.Points.Count - (region.IsClosed ? 0 : 1); regionIndex++)
                {
                    if (regionIndex % 256 == 0) cancellationToken.ThrowIfCancellationRequested();
                    Point2D regionStart = region.Points[regionIndex];
                    Point2D regionEnd = region.Points[(regionIndex + 1) % region.Points.Count];
                    double segmentDistance = SegmentDistance(start, end, regionStart, regionEnd);
                    distance = Math.Min(distance, segmentDistance);
                    if (segmentDistance <= tolerance)
                        return true;
                }
            }

            return false;
        }

        private static bool BoundingBoxesOverlap(RevitElementClashInfo element, DwgRegion region, double tolerance)
        {
            return element.MinX <= region.MaxX + tolerance &&
                   element.MaxX + tolerance >= region.MinX &&
                   element.MinY <= region.MaxY + tolerance &&
                   element.MaxY + tolerance >= region.MinY;
        }

        private static double SegmentDistance(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            if (SegmentsIntersect(a, b, c, d))
                return 0;

            PointInPolygonService helper = new PointInPolygonService();
            return Math.Min(Math.Min(helper.DistanceToPolyline(a, new[] { c, d }), helper.DistanceToPolyline(b, new[] { c, d })),
                Math.Min(helper.DistanceToPolyline(c, new[] { a, b }), helper.DistanceToPolyline(d, new[] { a, b })));
        }

        private static bool SegmentsIntersect(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            double d1 = Direction(c, d, a);
            double d2 = Direction(c, d, b);
            double d3 = Direction(a, b, c);
            double d4 = Direction(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Direction(Point2D a, Point2D b, Point2D c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
        }
    }
}
