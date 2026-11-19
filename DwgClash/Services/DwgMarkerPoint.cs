using System;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    internal static class DwgMarkerPoint
    {
        // A análise 2D não calcula o ponto exato; usa-se uma posição aproximada na planta.
        internal static Point2D ForResult(RevitElementClashInfo element, DwgRegion region)
        {
            Point2D center = element.Center;
            if (region == null || region.Points.Count == 0) return center;
            double minX = Math.Max(element.MinX, region.MinX), maxX = Math.Min(element.MaxX, region.MaxX);
            double minY = Math.Max(element.MinY, region.MinY), maxY = Math.Min(element.MaxY, region.MaxY);
            return minX <= maxX && minY <= maxY ? new Point2D((minX + maxX) / 2, (minY + maxY) / 2) : center;
        }
    }
}
