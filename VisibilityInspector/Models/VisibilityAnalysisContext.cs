using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityAnalysisContext
    {
        public VisibilityAnalysisContext(
            UIApplication uiApp,
            Document document,
            RevitView view,
            Element element,
            VisibilityReport report,
            Document elementDocument = null,
            RevitLinkInstance linkInstance = null)
        {
            UiApplication = uiApp;
            Document = document;
            HostDocument = document;
            ElementDocument = elementDocument ?? document;
            View = view;
            Element = element;
            Report = report;
            LinkInstance = linkInstance;
        }

        public UIApplication UiApplication { get; }
        public Document Document { get; }
        public Document HostDocument { get; }
        public Document ElementDocument { get; }
        public RevitView View { get; }
        public Element Element { get; }
        public RevitLinkInstance LinkInstance { get; }
        public VisibilityReport Report { get; }
        public bool IsLinkedElement => LinkInstance != null;
        public Element HostVisibilityElement => LinkInstance ?? Element;

        public BoundingBoxXYZ GetElementBoundingBoxInHostCoordinates()
        {
            BoundingBoxXYZ box = null;

            if (!IsLinkedElement)
            {
                try { box = Element.get_BoundingBox(View); }
                catch { }

                return box ?? Element.get_BoundingBox(null);
            }

            try { box = Element.get_BoundingBox(null); }
            catch { }

            if (box == null)
                return null;

            try
            {
                return TransformBoundingBox(box, LinkInstance.GetTotalTransform());
            }
            catch
            {
                return box;
            }
        }

        private static BoundingBoxXYZ TransformBoundingBox(BoundingBoxXYZ box, Transform transform)
        {
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
                minX = System.Math.Min(minX, point.X);
                minY = System.Math.Min(minY, point.Y);
                minZ = System.Math.Min(minZ, point.Z);
                maxX = System.Math.Max(maxX, point.X);
                maxY = System.Math.Max(maxY, point.Y);
                maxZ = System.Math.Max(maxZ, point.Z);
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }
    }
}
