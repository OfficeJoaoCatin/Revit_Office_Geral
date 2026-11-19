using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.LinkConflict.Services
{
    // Todas as caixas são convertidas ao sistema de coordenadas do modelo antes da comparação.
    internal static class LinkBoundingBoxes
    {
        internal static bool HasValidBounds(BoundingBoxXYZ box)
        {
            if (box == null || box.Min == null || box.Max == null) return false;
            XYZ min = box.Min, max = box.Max;
            return IsFinite(min.X) && IsFinite(min.Y) && IsFinite(min.Z) &&
                   IsFinite(max.X) && IsFinite(max.Y) && IsFinite(max.Z) &&
                   min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static void AddBoxPoints(BoundingBoxXYZ box, Transform parentTransform, ref bool found, ref XYZ min, ref XYZ max)
        {
            if (!HasValidBounds(box)) return;
            Transform boxTransform = box.Transform ?? Transform.Identity;
            Transform totalTransform = parentTransform.Multiply(boxTransform);
            XYZ[] points =
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

            var transformedPoints = new XYZ[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                XYZ transformed = totalTransform.OfPoint(points[index]);
                if (transformed == null || !IsFinite(transformed.X) || !IsFinite(transformed.Y) || !IsFinite(transformed.Z)) return;
                transformedPoints[index] = transformed;
            }
            foreach (XYZ transformed in transformedPoints)
            {
                min = !found
                    ? transformed
                    : new XYZ(Math.Min(min.X, transformed.X), Math.Min(min.Y, transformed.Y), Math.Min(min.Z, transformed.Z));
                max = !found
                    ? transformed
                    : new XYZ(Math.Max(max.X, transformed.X), Math.Max(max.Y, transformed.Y), Math.Max(max.Z, transformed.Z));
                found = true;
            }
        }

        internal static Outline CreateOutline(BoundingBoxXYZ box, Transform parentTransform)
        {
            bool found = false;
            XYZ min = null;
            XYZ max = null;
            AddBoxPoints(box, parentTransform, ref found, ref min, ref max);
            return found && min != null && max != null ? new Outline(min, max) : null;
        }

        internal static ElementFilter CreateCandidateFilter(Outline combined, List<Outline> parts, bool completeParts, double tolerance)
        {
            // A filtragem por parte só é usada se todas as caixas existirem; caso contrário,
            // preserva-se a consulta pela caixa combinada.
            if (completeParts && parts != null && parts.Count > 1 && parts.Count <= 16 &&
                parts.All(part => part != null && !part.IsEmpty))
            {
                try
                {
                    var filters = parts.Select(part => (ElementFilter)new BoundingBoxIntersectsFilter(part, tolerance)).ToList();
                    return new LogicalOrFilter(filters);
                }
                catch { /* Mantém o filtro conservador pela caixa combinada. */ }
            }
            return new BoundingBoxIntersectsFilter(combined, tolerance);
        }

        internal static bool Contains(Outline outer, Outline inner)
        {
            if (outer == null || inner == null) return false;
            XYZ a = outer.MinimumPoint, b = outer.MaximumPoint;
            XYZ c = inner.MinimumPoint, d = inner.MaximumPoint;
            return a.X <= c.X && a.Y <= c.Y && a.Z <= c.Z &&
                   b.X >= d.X && b.Y >= d.Y && b.Z >= d.Z;
        }

        internal static bool BoundingBoxesOverlap(Outline firstBox, Outline secondBox, double tolerance)
        {
            if (firstBox == null || secondBox == null) return true;
            XYZ firstMin = firstBox.MinimumPoint, firstMax = firstBox.MaximumPoint;
            XYZ secondMin = secondBox.MinimumPoint, secondMax = secondBox.MaximumPoint;
            return firstMin.X <= secondMax.X + tolerance &&
                   firstMax.X + tolerance >= secondMin.X &&
                   firstMin.Y <= secondMax.Y + tolerance &&
                   firstMax.Y + tolerance >= secondMin.Y &&
                   firstMin.Z <= secondMax.Z + tolerance &&
                   firstMax.Z + tolerance >= secondMin.Z;
        }
    }
}
