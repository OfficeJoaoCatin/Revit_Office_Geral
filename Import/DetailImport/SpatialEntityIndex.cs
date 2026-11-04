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

        private readonly double _cellSize;

        public SpatialEntityIndex(IEnumerable<DwgEntityInfo> entities, double cellSize)
        {
            _cellSize = Math.Max(cellSize, 1.0);

            foreach (DwgEntityInfo entity in entities ?? Enumerable.Empty<DwgEntityInfo>())
            {
                DwgBounds bounds = entity.Bounds;
                int minX = ToCell(bounds.MinX);
                int maxX = ToCell(bounds.MaxX);
                int minY = ToCell(bounds.MinY);
                int maxY = ToCell(bounds.MaxY);

                if (minX == maxX && minY == maxY && bounds.Width == 0 && bounds.Height == 0)
                {
                    AddToCell(GetKey(entity.Center.X, entity.Center.Y), entity);
                    continue;
                }

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
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

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
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
            return (int)Math.Floor(value / _cellSize);
        }
    }
}


