using System;
using System.Collections.Generic;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class PointInPolygonService
    {
        public bool Contains(Point2D point, DwgRegion region, double tolerance)
        {
            if (region == null || region.Points.Count < 2)
                return false;

            if (!region.IsClosed)
                return DistanceToPolyline(point, region.Points) <= tolerance;

            if (DistanceToPolyline(point, region.Points, true) <= tolerance)
                return true;

            bool inside = false;
            for (int index = 0, previous = region.Points.Count - 1; index < region.Points.Count; previous = index++)
            {
                Point2D currentPoint = region.Points[index];
                Point2D previousPoint = region.Points[previous];
                bool crosses = currentPoint.Y > point.Y != previousPoint.Y > point.Y;
                if (crosses && point.X <
                    (previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y) /
                    (previousPoint.Y - currentPoint.Y) + currentPoint.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public double DistanceToPolyline(Point2D point, IList<Point2D> line, bool closed = false)
        {
            double best = double.MaxValue;
            int count = closed ? line.Count : line.Count - 1;
            for (int index = 0; index < count; index++)
            {
                Point2D start = line[index];
                Point2D end = line[(index + 1) % line.Count];
                best = Math.Min(best, DistanceToSegment(point, start, end));
            }
            return best;
        }

        private static double DistanceToSegment(Point2D point, Point2D start, Point2D end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 1e-12)
                return point.DistanceTo(start);

            double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            return point.DistanceTo(new Point2D(start.X + t * dx, start.Y + t * dy));
        }
    }
}
