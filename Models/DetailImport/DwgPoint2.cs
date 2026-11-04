using System;

namespace RevitFamilyImporter.Models.DetailImport
{
    public struct DwgPoint2
    {
        public DwgPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public double DistanceTo(DwgPoint2 other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return $"{X:0.###}, {Y:0.###}";
        }
    }
}

