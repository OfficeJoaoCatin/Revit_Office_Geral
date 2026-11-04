using ACadSharp.Entities;
using CSMath;
using RevitFamilyImporter.Models.DetailImport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    public static class DwgGeometryUtilities
    {
        public static DwgEntityKind GetKind(Entity entity)
        {
            if (entity is Line) return DwgEntityKind.Line;
            if (entity is LwPolyline || entity is Polyline2D || entity is Polyline3D) return DwgEntityKind.Polyline;
            if (entity is Insert) return DwgEntityKind.BlockReference;
            if (entity is TextEntity) return DwgEntityKind.Text;
            if (entity is MText) return DwgEntityKind.MText;
            if (entity is Hatch) return DwgEntityKind.Hatch;
            return DwgEntityKind.Other;
        }

        public static bool IsSupported(Entity entity)
        {
            DwgEntityKind kind = GetKind(entity);
            return kind == DwgEntityKind.Line ||
                   kind == DwgEntityKind.Polyline ||
                   kind == DwgEntityKind.BlockReference ||
                   kind == DwgEntityKind.Text ||
                   kind == DwgEntityKind.MText ||
                   kind == DwgEntityKind.Hatch;
        }

        public static bool IsClosedPolyline(Entity entity)
        {
            if (entity is LwPolyline lwPolyline)
                return lwPolyline.IsClosed;

            if (entity is IPolyline polyline)
                return polyline.IsClosed;

            return false;
        }

        public static List<DwgPoint2> GetPolylinePoints(Entity entity)
        {
            if (entity is LwPolyline lwPolyline)
            {
                return lwPolyline.Vertices
                    .Select(v => new DwgPoint2(v.Location.X, v.Location.Y))
                    .ToList();
            }

            if (entity is IPolyline polyline)
            {
                return polyline.Vertices
                    .Select(v => ToPoint2(v.Location))
                    .ToList();
            }

            return new List<DwgPoint2>();
        }

        public static DwgBounds GetBounds(Entity entity)
        {
            if (TryGetNativeBounds(entity, out DwgBounds nativeBounds))
                return nativeBounds;

            List<DwgPoint2> points = GetGeometryPoints(entity);
            return points.Count > 0
                ? DwgBounds.FromPoints(points)
                : new DwgBounds(0, 0, 0, 0);
        }

        private static bool TryGetNativeBounds(Entity entity, out DwgBounds bounds)
        {
            bounds = new DwgBounds(0, 0, 0, 0);

            try
            {
                BoundingBox box = entity.GetBoundingBox();
                bounds = new DwgBounds(box.Min.X, box.Min.Y, box.Max.X, box.Max.Y);
                return bounds.Width > 0 || bounds.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        public static List<DwgPoint2> GetGeometryPoints(Entity entity)
        {
            if (entity is Line line)
            {
                return new List<DwgPoint2>
                {
                    new DwgPoint2(line.StartPoint.X, line.StartPoint.Y),
                    new DwgPoint2(line.EndPoint.X, line.EndPoint.Y)
                };
            }

            if (entity is LwPolyline || entity is Polyline2D || entity is Polyline3D)
                return GetPolylinePoints(entity);

            if (entity is Insert insert)
                return new List<DwgPoint2> { new DwgPoint2(insert.InsertPoint.X, insert.InsertPoint.Y) };

            if (entity is TextEntity text)
                return new List<DwgPoint2> { new DwgPoint2(text.InsertPoint.X, text.InsertPoint.Y) };

            if (entity is MText mText)
                return new List<DwgPoint2> { new DwgPoint2(mText.InsertPoint.X, mText.InsertPoint.Y) };

            try
            {
                BoundingBox box = entity.GetBoundingBox();
                return new List<DwgPoint2>
                {
                    new DwgPoint2(box.Min.X, box.Min.Y),
                    new DwgPoint2(box.Max.X, box.Max.Y)
                };
            }
            catch
            {
                return new List<DwgPoint2>();
            }
        }

        public static string GetText(Entity entity)
        {
            if (entity is TextEntity text)
                return text.Value ?? string.Empty;

            if (entity is MText mText)
                return mText.PlainText ?? mText.Value ?? string.Empty;

            return string.Empty;
        }

        public static DwgPoint2 ToPoint2(IVector vector)
        {
            if (vector is XY xy)
                return new DwgPoint2(xy.X, xy.Y);

            if (vector is XYZ xyz)
                return new DwgPoint2(xyz.X, xyz.Y);

            Type type = vector.GetType();
            object x = type.GetProperty("X")?.GetValue(vector);
            object y = type.GetProperty("Y")?.GetValue(vector);
            return new DwgPoint2(Convert.ToDouble(x), Convert.ToDouble(y));
        }

        public static bool PointInPolygon(DwgPoint2 point, IList<DwgPoint2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                DwgPoint2 pi = polygon[i];
                DwgPoint2 pj = polygon[j];

                bool crosses = (pi.Y > point.Y) != (pj.Y > point.Y);
                if (crosses)
                {
                    double xAtY = (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 0.0000000001) + pi.X;
                    if (point.X < xAtY)
                        inside = !inside;
                }

                j = i;
            }

            return inside;
        }
    }
}


