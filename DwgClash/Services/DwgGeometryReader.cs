using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class DwgGeometryReader
    {
        private const double PointToleranceFeet = 1e-6;

        public List<DwgRegion> ReadRegions(Document document, Autodesk.Revit.DB.View view,
            ImportInstance importInstance, string layer)
        {
            List<DwgRegion> regions = new List<DwgRegion>();
            if (document == null || importInstance == null || string.IsNullOrWhiteSpace(layer))
                return regions;

            Options options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            ViewProjectionService projection = new ViewProjectionService(view);
            GeometryElement geometry = importInstance.get_Geometry(options);
            if (geometry != null)
                ReadGeometry(document, geometry, Transform.Identity, projection, string.Empty, layer, regions);

            for (int index = 0; index < regions.Count; index++)
                regions[index].Number = index + 1;

            return regions;
        }

        public Dictionary<string, int> CountLayers(Document document, ImportInstance importInstance)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (document == null || importInstance == null)
                return counts;

            Options options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometry = importInstance.get_Geometry(options);
            if (geometry != null)
                CountGeometry(document, geometry, string.Empty, counts);
            return counts;
        }

        private static void ReadGeometry(
            Document document,
            GeometryElement geometry,
            Transform transform,
            ViewProjectionService projection,
            string inheritedLayer,
            string targetLayer,
            ICollection<DwgRegion> regions)
        {
            foreach (GeometryObject geometryObject in geometry)
            {
                string layer = ResolveLayer(document, geometryObject, inheritedLayer);
                if (geometryObject is GeometryInstance instance)
                {
                    GeometryElement nestedGeometry = null;
                    try { nestedGeometry = instance.GetInstanceGeometry(); } catch { }
                    if (nestedGeometry != null)
                    {
                        // GetInstanceGeometry já retorna a cópia posicionada no sistema do modelo.
                        ReadGeometry(document, nestedGeometry, Transform.Identity, projection,
                            layer, targetLayer, regions);
                    }
                    else
                    {
                        try
                        {
                            nestedGeometry = instance.GetSymbolGeometry();
                            if (nestedGeometry != null)
                            {
                                Transform nested = transform.Multiply(instance.Transform ?? Transform.Identity);
                                ReadGeometry(document, nestedGeometry, nested, projection,
                                    layer, targetLayer, regions);
                            }
                        }
                        catch
                        {
                            // Geometrias CAD incompletas são ignoradas sem interromper a análise.
                        }
                    }
                    continue;
                }

                if (!string.Equals(layer, targetLayer, StringComparison.OrdinalIgnoreCase))
                    continue;

                List<XYZ> points = GetPoints(geometryObject, transform);
                if (points.Count < 2)
                    continue;

                bool closed = IsClosed(geometryObject, points);
                regions.Add(new DwgRegion
                {
                    Layer = layer,
                    IsClosed = closed,
                    Points = ToPoints2D(points, projection, closed)
                });
            }
        }

        private static void CountGeometry(
            Document document,
            GeometryElement geometry,
            string inheritedLayer,
            IDictionary<string, int> counts)
        {
            foreach (GeometryObject geometryObject in geometry)
            {
                string layer = ResolveLayer(document, geometryObject, inheritedLayer);
                if (geometryObject is GeometryInstance instance)
                {
                    GeometryElement nestedGeometry = null;
                    try { nestedGeometry = instance.GetSymbolGeometry(); } catch { }
                    if (nestedGeometry != null)
                        CountGeometry(document, nestedGeometry, layer, counts);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(layer))
                    continue;
                counts[layer] = counts.TryGetValue(layer, out int count) ? count + 1 : 1;
            }
        }

        private static string ResolveLayer(Document document, GeometryObject geometryObject, string inheritedLayer)
        {
            try
            {
                ElementId styleId = geometryObject.GraphicsStyleId;
                if (styleId != null && styleId != ElementId.InvalidElementId)
                {
                    GraphicsStyle style = document.GetElement(styleId) as GraphicsStyle;
                    string name = style?.GraphicsStyleCategory?.Name;
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
            catch { }

            return inheritedLayer ?? string.Empty;
        }

        private static List<XYZ> GetPoints(GeometryObject geometryObject, Transform transform)
        {
            try
            {
                if (geometryObject is PolyLine polyline)
                    return polyline.GetCoordinates().Select(transform.OfPoint).ToList();

                if (geometryObject is Curve curve)
                    return curve.Tessellate().Select(transform.OfPoint).ToList();
            }
            catch { }

            return new List<XYZ>();
        }

        private static bool IsClosed(GeometryObject geometryObject, IList<XYZ> points)
        {
            if (geometryObject is Curve curve && curve.IsCyclic)
                return true;

            return points.Count > 2 && points[0].DistanceTo(points[points.Count - 1]) <= PointToleranceFeet;
        }

        private static List<Point2D> ToPoints2D(IList<XYZ> points, ViewProjectionService projection, bool closed)
        {
            List<Point2D> result = points.Select(projection.Project).ToList();
            if (closed && result.Count > 1 && result[0].DistanceTo(result[result.Count - 1]) <= PointToleranceFeet)
                result.RemoveAt(result.Count - 1);
            return result;
        }
    }
}
