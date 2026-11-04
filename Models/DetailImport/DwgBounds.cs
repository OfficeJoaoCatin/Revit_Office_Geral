using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.Models.DetailImport
{
    public struct DwgBounds
    {
        public DwgBounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        public double Width => Math.Max(0, MaxX - MinX);

        public double Height => Math.Max(0, MaxY - MinY);

        public DwgPoint2 Center => new DwgPoint2((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);

        public bool Contains(DwgPoint2 point)
        {
            return point.X >= MinX &&
                   point.X <= MaxX &&
                   point.Y >= MinY &&
                   point.Y <= MaxY;
        }

        public bool Intersects(DwgBounds other)
        {
            return !(other.MaxX < MinX ||
                     other.MinX > MaxX ||
                     other.MaxY < MinY ||
                     other.MinY > MaxY);
        }

        public DwgBounds Expand(double value)
        {
            if (value <= 0)
                return this;

            return new DwgBounds(
                MinX - value,
                MinY - value,
                MaxX + value,
                MaxY + value);
        }

        public DwgBounds Include(DwgBounds other)
        {
            return new DwgBounds(
                Math.Min(MinX, other.MinX),
                Math.Min(MinY, other.MinY),
                Math.Max(MaxX, other.MaxX),
                Math.Max(MaxY, other.MaxY));
        }

        public static DwgBounds FromPoints(IEnumerable<DwgPoint2> points)
        {
            List<DwgPoint2> list = (points ?? Enumerable.Empty<DwgPoint2>()).ToList();
            if (list.Count == 0)
                return new DwgBounds(0, 0, 0, 0);

            return new DwgBounds(
                list.Min(p => p.X),
                list.Min(p => p.Y),
                list.Max(p => p.X),
                list.Max(p => p.Y));
        }
    }
}

