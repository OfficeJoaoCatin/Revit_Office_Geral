using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgRegion
    {
        public int Number { get; set; }
        public string Layer { get; set; } = string.Empty;
        public bool IsClosed { get; set; }
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        public string Name => $"Região {Number}";

        public double MinX => Points.Count == 0 ? 0 : Points.Min(point => point.X);
        public double MaxX => Points.Count == 0 ? 0 : Points.Max(point => point.X);
        public double MinY => Points.Count == 0 ? 0 : Points.Min(point => point.Y);
        public double MaxY => Points.Count == 0 ? 0 : Points.Max(point => point.Y);
    }
}
