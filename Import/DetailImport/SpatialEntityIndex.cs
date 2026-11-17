using RevitFamilyImporter.Models.DetailImport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    internal class SpatialEntityIndex
    {
        private readonly Dictionary<string, List<DwgEntityInfo>> _cells =
            new Dictionary<string, List<DwgEntityInfo>>();

        private const long MaxCellsPerRange = 10000;
        private readonly List<DwgEntityInfo> _all = new List<DwgEntityInfo>();
        private readonly List<DwgEntityInfo> _large = new List<DwgEntityInfo>();
        private readonly double _cellSize;

        public SpatialEntityIndex(IEnumerable<DwgEntityInfo> entities, double cellSize)
        {
            _cellSize = Math.Max(cellSize, 1.0);

            foreach (DwgEntityInfo entity in entities ?? Enumerable.Empty<DwgEntityInfo>())
            {
                _all.Add(entity);
                DwgBounds bounds = entity.Bounds;
                int minX = ToCell(bounds.MinX);
                int maxX = ToCell(bounds.MaxX);
                int minY = ToCell(bounds.MinY);
                int maxY = ToCell(bounds.MaxY);

                if (IsLargeRange(minX, maxX, minY, maxY))
                {
                    _large.Add(entity);
                    continue;
                }

                if (minX == maxX && minY == maxY && bounds.Width == 0 && bounds.Height == 0)
                {
                    AddToCell(GetKey(entity.Center.X, entity.Center.Y), entity);
                    continue;
                }

                for (long x = minX; x <= maxX; x++)
                    for (long y = minY; y <= maxY; y++)
                        AddToCell($"{x}:{y}", entity);
            }
        }

        public IEnumerable<DwgEntityInfo> Query(DwgBounds bounds)
        {
            int minX = ToCell(bounds.MinX);
            int maxX = ToCell(bounds.MaxX);
            int minY = ToCell(bounds.MinY);
            int maxY = ToCell(bounds.MaxY);
            HashSet<int> yielded = new HashSet<int>();
            if (IsLargeRange(minX, maxX, minY, maxY))
            {
                foreach (DwgEntityInfo entity in _all)
                    if (entity.Bounds.Intersects(bounds) && yielded.Add(entity.Index))
                        yield return entity;
                yield break;
            }

            foreach (DwgEntityInfo entity in _large)
                if (entity.Bounds.Intersects(bounds) && yielded.Add(entity.Index))
                    yield return entity;

            for (long x = minX; x <= maxX; x++)
            {
                for (long y = minY; y <= maxY; y++)
                {
                    if (!_cells.TryGetValue($"{x}:{y}", out List<DwgEntityInfo> list))
                        continue;

                    foreach (DwgEntityInfo entity in list)
                    {
                        if (yielded.Add(entity.Index))
                            yield return entity;
                    }
                }
            }
        }

        private static bool IsLargeRange(int minX, int maxX, int minY, int maxY)
        {
            long width = (long)maxX - minX + 1;
            long height = (long)maxY - minY + 1;
            return width > MaxCellsPerRange || height > MaxCellsPerRange ||
                   width * height > MaxCellsPerRange;
        }

        private string GetKey(double x, double y)
        {
            return $"{ToCell(x)}:{ToCell(y)}";
        }

        private void AddToCell(string key, DwgEntityInfo entity)
        {
            if (!_cells.TryGetValue(key, out List<DwgEntityInfo> list))
            {
                list = new List<DwgEntityInfo>();
                _cells[key] = list;
            }

            list.Add(entity);
        }

        private int ToCell(double value)
        {
            double cell = Math.Floor(value / _cellSize);
            if (double.IsNaN(cell)) return 0;
            if (cell <= int.MinValue) return int.MinValue;
            if (cell >= int.MaxValue) return int.MaxValue;
            return (int)cell;
        }
    }
}


