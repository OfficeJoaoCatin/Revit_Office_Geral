using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    // Grade limitada: regiões enormes e consultas enormes usam fallback linear para não perder candidatos.
    internal sealed class DwgRegionSpatialIndex
    {
        private const int MaxCellsPerRegion = 32;
        private const int MaxCellsPerQuery = 4096;
        private readonly List<Bounds> _bounds = new List<Bounds>();
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly List<int> _large = new List<int>();
        private readonly double _cellSize;

        private sealed class Bounds
        {
            public double MinX, MinY, MaxX, MaxY;
            public bool IsFinite => Finite(MinX) && Finite(MinY) && Finite(MaxX) && Finite(MaxY);
        }

        public DwgRegionSpatialIndex(IList<DwgRegion> regions, CancellationToken cancellationToken = default)
        {
            foreach (DwgRegion region in regions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _bounds.Add(new Bounds { MinX = region.MinX, MinY = region.MinY, MaxX = region.MaxX, MaxY = region.MaxY });
            }
            var valid = _bounds.Where(bounds => bounds.IsFinite).ToList();
            if (valid.Count == 0) { _cellSize = 1; return; }
            double spanX = valid.Max(b => b.MaxX) - valid.Min(b => b.MinX);
            double spanY = valid.Max(b => b.MaxY) - valid.Min(b => b.MinY);
            double scale = Math.Sqrt(Math.Max(spanX, 0.001) * Math.Max(spanY, 0.001) / valid.Count);
            _cellSize = !Finite(scale) || scale < 0.001 ? 0.001 : scale;
            for (int i = 0; i < _bounds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Bounds b = _bounds[i];
                if (!b.IsFinite) { _large.Add(i); continue; }
                int minX = ToCell(b.MinX), maxX = ToCell(b.MaxX), minY = ToCell(b.MinY), maxY = ToCell(b.MaxY);
                if (ExceedsLimit(minX, maxX, minY, maxY, MaxCellsPerRegion)) { _large.Add(i); continue; }
                for (long x = minX; x <= maxX; x++)
                    for (long y = minY; y <= maxY; y++)
                    {
                        long key = CellKey((int)x, (int)y);
                        if (!_cells.TryGetValue(key, out List<int> list)) _cells[key] = list = new List<int>();
                        list.Add(i);
                    }
            }
        }

        public List<int> Query(RevitElementClashInfo element, double tolerance, CancellationToken cancellationToken = default)
        {
            // Margem mínima conservadora protege comparações no limite contra arredondamento.
            double magnitude = Math.Max(Math.Max(Math.Abs(element.MinX), Math.Abs(element.MaxX)),
                Math.Max(Math.Abs(element.MinY), Math.Abs(element.MaxY)));
            double padding = Math.Max(0, tolerance) + 1e-9 + magnitude * 1e-12;
            double minX = element.MinX - padding, maxX = element.MaxX + padding;
            double minY = element.MinY - padding, maxY = element.MaxY + padding;
            if (!Finite(minX) || !Finite(maxX) || !Finite(minY) || !Finite(maxY))
                return AllMatches(minX, minY, maxX, maxY, cancellationToken);
            int x0 = ToCell(minX), x1 = ToCell(maxX), y0 = ToCell(minY), y1 = ToCell(maxY);
            if (ExceedsLimit(x0, x1, y0, y1, MaxCellsPerQuery))
                return AllMatches(minX, minY, maxX, maxY, cancellationToken);
            var result = new HashSet<int>();
            foreach (int index in _large)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Overlaps(_bounds[index], minX, minY, maxX, maxY)) result.Add(index);
            }
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_cells.TryGetValue(CellKey((int)x, (int)y), out List<int> list)) continue;
                    foreach (int index in list)
                        if (Overlaps(_bounds[index], minX, minY, maxX, maxY)) result.Add(index);
                }
            List<int> ordered = result.ToList();
            ordered.Sort(); // Mantém a mesma ordem de regiões da análise original.
            return ordered;
        }

        private List<int> AllMatches(double minX, double minY, double maxX, double maxY, CancellationToken cancellationToken)
        {
            var result = new List<int>();
            for (int i = 0; i < _bounds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Overlaps(_bounds[i], minX, minY, maxX, maxY)) result.Add(i);
            }
            return result;
        }
        private static bool Overlaps(Bounds b, double minX, double minY, double maxX, double maxY) =>
            minX <= b.MaxX && maxX >= b.MinX && minY <= b.MaxY && maxY >= b.MinY;
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool ExceedsLimit(int x0, int x1, int y0, int y1, int limit)
        {
            long width = (long)x1 - x0 + 1, height = (long)y1 - y0 + 1;
            return width <= 0 || height <= 0 || width > limit || height > limit || width * height > limit;
        }
        private int ToCell(double coordinate)
        {
            double cell = Math.Floor(coordinate / _cellSize);
            if (cell <= int.MinValue) return int.MinValue;
            if (cell >= int.MaxValue) return int.MaxValue;
            return (int)cell;
        }
        private static long CellKey(int x, int y) => ((long)(uint)x << 32) | (uint)y;
    }
}
