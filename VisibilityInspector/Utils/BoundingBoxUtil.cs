using System;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.VisibilityInspector.Utils
{
    internal static class BoundingBoxUtil
    {
        private const double DefaultTolerance = 1.0 / 304.8; // 1 mm in feet.

        public static bool Intersects(BoundingBoxXYZ a, BoundingBoxXYZ b, double tolerance = DefaultTolerance)
        {
            if (a == null || b == null)
                return false;

            return a.Min.X <= b.Max.X + tolerance && a.Max.X >= b.Min.X - tolerance &&
                   a.Min.Y <= b.Max.Y + tolerance && a.Max.Y >= b.Min.Y - tolerance &&
                   a.Min.Z <= b.Max.Z + tolerance && a.Max.Z >= b.Min.Z - tolerance;
        }

        public static bool Contains(BoundingBoxXYZ container, BoundingBoxXYZ content, double tolerance = DefaultTolerance)
        {
            if (container == null || content == null)
                return false;

            return content.Min.X >= container.Min.X - tolerance && content.Max.X <= container.Max.X + tolerance &&
                   content.Min.Y >= container.Min.Y - tolerance && content.Max.Y <= container.Max.Y + tolerance &&
                   content.Min.Z >= container.Min.Z - tolerance && content.Max.Z <= container.Max.Z + tolerance;
        }

        public static bool IntersectsSectionBox(BoundingBoxXYZ sectionBox, BoundingBoxXYZ elementBox, double tolerance = DefaultTolerance)
        {
            if (sectionBox == null || elementBox == null)
                return false;

            Transform sectionTransform = sectionBox.Transform ?? Transform.Identity;
            Transform inverse;
            try
            {
                inverse = sectionTransform.Inverse;
            }
            catch
            {
                return Intersects(sectionBox, elementBox, tolerance);
            }

            BoundingBoxXYZ elementInSectionCoordinates = TransformBoundingBox(elementBox, inverse);
            return Intersects(sectionBox, elementInSectionCoordinates, tolerance);
        }

        public static BoundingBoxXYZ TransformBoundingBox(BoundingBoxXYZ box, Transform transform)
        {
            if (box == null || transform == null)
                return null;

            XYZ[] points = GetCorners(box);
            XYZ first = transform.OfPoint(points[0]);
            double minX = first.X;
            double minY = first.Y;
            double minZ = first.Z;
            double maxX = first.X;
            double maxY = first.Y;
            double maxZ = first.Z;

            for (int i = 1; i < points.Length; i++)
            {
                XYZ point = transform.OfPoint(points[i]);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                maxZ = Math.Max(maxZ, point.Z);
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        private static XYZ[] GetCorners(BoundingBoxXYZ box)
        {
            return new[]
            {
                new XYZ(box.Min.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Max.Z)
            };
        }
    }
}
