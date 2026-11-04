using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class RevitElementCollector
    {
        public List<DwgCategoryOption> GetCategoryOptions()
        {
            return new List<DwgCategoryOption>
            {
                Option(BuiltInCategory.OST_LightingFixtures, "Lighting Fixtures", true),
                Option(BuiltInCategory.OST_ElectricalFixtures, "Electrical Fixtures", true),
                Option(BuiltInCategory.OST_ElectricalEquipment, "Electrical Equipment", true),
                Option(BuiltInCategory.OST_Conduit, "Conduits", true),
                Option(BuiltInCategory.OST_ConduitFitting, "Conduit Fittings", true),
                Option(BuiltInCategory.OST_CableTray, "Cable Trays", true),
                Option(BuiltInCategory.OST_CableTrayFitting, "Cable Tray Fittings", true),
                Option(BuiltInCategory.OST_GenericModel, "Generic Models", false)
            };
        }

        public List<RevitElementClashInfo> Collect(Document document, Autodesk.Revit.DB.View view, IEnumerable<ElementId> categoryIds)
        {
            HashSet<int> allowed = new HashSet<int>((categoryIds ?? Enumerable.Empty<ElementId>())
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .Select(id => id.IntegerValue));

            List<RevitElementClashInfo> result = new List<RevitElementClashInfo>();
            if (document == null || view == null || allowed.Count == 0)
                return result;

            IList<Element> elements = new FilteredElementCollector(document, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in elements)
            {
                try
                {
                    if (element.IsHidden(view))
                        continue;
                }
                catch
                {
                    // Algumas categorias não expõem o estado de ocultação por vista.
                }

                if (element.Category == null || !allowed.Contains(element.Category.Id.IntegerValue))
                    continue;

                BoundingBoxXYZ box = null;
                try { box = element.get_BoundingBox(view); } catch { }
                if (box == null)
                    continue;

                ViewProjectionService projection = new ViewProjectionService(view);
                List<Point2D> footprint = GetGeometryFootprint(element, view, projection);
                if (footprint.Count < 3)
                    footprint = GetBoundingFootprint(box, projection);
                if (footprint.Count < 3)
                    continue;

                ElementType type = element.Document.GetElement(element.GetTypeId()) as ElementType;
                string family = type is FamilySymbol symbol ? symbol.FamilyName : type?.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString();
                result.Add(new RevitElementClashInfo
                {
                    ElementId = element.Id,
                    Category = element.Category.Name,
                    Family = family ?? string.Empty,
                    Type = type?.Name ?? element.Name ?? string.Empty,
                    Footprint = footprint,
                    Center = GetCenter(footprint),
                    MinX = footprint.Min(point => point.X),
                    MaxX = footprint.Max(point => point.X),
                    MinY = footprint.Min(point => point.Y),
                    MaxY = footprint.Max(point => point.Y)
                });
            }

            return result;
        }

        private static DwgCategoryOption Option(BuiltInCategory category, string name, bool selected)
        {
            return new DwgCategoryOption
            {
                CategoryId = new ElementId(category),
                Name = name,
                IsSelected = selected
            };
        }

        private static List<Point2D> GetGeometryFootprint(Element element, Autodesk.Revit.DB.View view,
            ViewProjectionService projection)
        {
            List<XYZ> points = new List<XYZ>();
            try
            {
                Options options = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false,
                    DetailLevel = ViewDetailLevel.Fine,
                    View = view
                };
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry != null)
                    CollectGeometryPoints(geometry, points);
            }
            catch
            {
                // Alguns elementos não permitem extrair geometria nessa vista.
            }

            return BuildConvexHull(points.Select(projection.Project));
        }

        private static void CollectGeometryPoints(GeometryElement geometry, ICollection<XYZ> points)
        {
            foreach (GeometryObject geometryObject in geometry)
            {
                if (geometryObject is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry = null;
                    try { instanceGeometry = instance.GetInstanceGeometry(); } catch { }
                    if (instanceGeometry != null)
                        CollectGeometryPoints(instanceGeometry, points);
                    continue;
                }

                if (geometryObject is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        try
                        {
                            foreach (XYZ point in edge.AsCurve().Tessellate())
                                points.Add(point);
                        }
                        catch { }
                    }
                    continue;
                }

                if (geometryObject is Mesh mesh)
                {
                    foreach (XYZ point in mesh.Vertices)
                        points.Add(point);
                    continue;
                }

                if (geometryObject is PolyLine polyline)
                {
                    foreach (XYZ point in polyline.GetCoordinates())
                        points.Add(point);
                    continue;
                }

                if (geometryObject is Curve curve)
                {
                    try
                    {
                        foreach (XYZ point in curve.Tessellate())
                            points.Add(point);
                    }
                    catch { }
                }
            }
        }

        private static List<Point2D> GetBoundingFootprint(BoundingBoxXYZ box, ViewProjectionService projection)
        {
            Transform transform = box.Transform ?? Transform.Identity;
            List<Point2D> points = new List<Point2D>();
            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
                points.Add(projection.Project(transform.OfPoint(new XYZ(x, y, z))));
            return BuildConvexHull(points);
        }

        private static Point2D GetCenter(IList<Point2D> footprint)
        {
            return new Point2D(footprint.Average(point => point.X), footprint.Average(point => point.Y));
        }

        private static List<Point2D> BuildConvexHull(IEnumerable<Point2D> source)
        {
            List<Point2D> points = source
                .GroupBy(point => string.Format("{0:0.###########}|{1:0.###########}", point.X, point.Y))
                .Select(group => group.First())
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToList();
            if (points.Count <= 1)
                return points;

            List<Point2D> lower = new List<Point2D>();
            foreach (Point2D point in points)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 1e-9)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(point);
            }

            List<Point2D> upper = new List<Point2D>();
            for (int index = points.Count - 1; index >= 0; index--)
            {
                Point2D point = points[index];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 1e-9)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static double Cross(Point2D a, Point2D b, Point2D c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }
}
