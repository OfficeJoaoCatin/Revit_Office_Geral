using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.LinkConflict.Models;

namespace RevitFamilyImporter.LinkConflict.Services
{
    public sealed class LinkConflictMarkerService
    {
        private const string MarkerLineStyleName = "Office - Conflitos em Link";
        private const double MarkerRadiusFeet = 0.18;

        public ViewPlan CreateInCopiedView(
            Document document,
            ViewPlan sourceView,
            IReadOnlyList<LinkConflictItem> conflicts,
            out int created,
            string viewSuffix = "Conflitos em Link",
            bool withDetailing = false)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (sourceView == null)
                throw new ArgumentNullException(nameof(sourceView));
            if (conflicts == null)
                throw new ArgumentNullException(nameof(conflicts));

            using (Transaction transaction = new Transaction(document, "Criar avisos de conflitos"))
            {
                transaction.Start();

                ViewDuplicateOption duplicateMode = withDetailing ? ViewDuplicateOption.WithDetailing : ViewDuplicateOption.Duplicate;
                if (!sourceView.CanViewBeDuplicated(duplicateMode))
                    throw new InvalidOperationException("A vista selecionada não permite a cópia necessária para criar os avisos.");
                ElementId duplicateId = sourceView.Duplicate(duplicateMode);
                ViewPlan view = document.GetElement(duplicateId) as ViewPlan;
                if (view == null)
                    throw new InvalidOperationException("Não foi possível criar uma cópia da vista de piso.");

                view.Name = GetUniqueViewName(document, sourceView.Name + " - " + viewSuffix);

                GraphicsStyle markerStyle = GetOrCreateMarkerStyle(document);

                Level level = view.GenLevel;
                double viewElevation = level != null ? level.Elevation : 0;
                XYZ right = view.RightDirection.Normalize();
                XYZ up = view.UpDirection.Normalize();
                double radius = Math.Max(MarkerRadiusFeet, view.Scale * 0.0025);
                created = 0;

                foreach (LinkConflictItem conflict in conflicts)
                {
                    XYZ center = ResolveHostMarkerPoint(document, view, conflict, viewElevation);
                    if (center == null)
                        continue;

                    CreateMarkerCircle(document, view, center, radius, right, up, markerStyle);
                    created++;
                }

                transaction.Commit();
                return view;
            }
        }

        private static string GetUniqueViewName(Document document, string baseName)
        {
            HashSet<string> names = new HashSet<string>(
                new FilteredElementCollector(document)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .Select(view => view.Name),
                StringComparer.OrdinalIgnoreCase);

            if (!names.Contains(baseName))
                return baseName;

            for (int index = 2; index < 10000; index++)
            {
                string candidate = $"{baseName} ({index})";
                if (!names.Contains(candidate))
                    return candidate;
            }

            return $"{baseName} {DateTime.Now:HHmmss}";
        }

        private static GraphicsStyle GetOrCreateMarkerStyle(Document document)
        {
            Category linesCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            Category markerCategory = linesCategory.SubCategories
                .Cast<Category>()
                .FirstOrDefault(category => string.Equals(category.Name, MarkerLineStyleName, StringComparison.OrdinalIgnoreCase));

            if (markerCategory == null)
            {
                markerCategory = document.Settings.Categories.NewSubcategory(linesCategory, MarkerLineStyleName);
                markerCategory.LineColor = new Autodesk.Revit.DB.Color(220, 0, 0);
                markerCategory.SetLineWeight(5, GraphicsStyleType.Projection);
            }
            return markerCategory.GetGraphicsStyle(GraphicsStyleType.Projection);
        }

        private static XYZ ResolveHostMarkerPoint(
            Document document,
            ViewPlan view,
            LinkConflictItem conflict,
            double viewElevation)
        {
            if (conflict == null)
                return null;

            XYZ fallback = conflict.MarkerPoint == null
                ? null
                : new XYZ(conflict.MarkerPoint.X, conflict.MarkerPoint.Y, viewElevation);

            Element hostElement = document.GetElement(new ElementId(conflict.HostElementId));
            if (hostElement == null)
                return fallback;

            Outline hostOutline = GetElementOutline(hostElement, view) ?? GetElementOutline(hostElement, null);
            if (hostOutline == null)
                return fallback;

            XYZ target = fallback ?? GetOutlineCenter(hostOutline, viewElevation);
            XYZ min = hostOutline.MinimumPoint;
            XYZ max = hostOutline.MaximumPoint;

            return new XYZ(
                Clamp(target.X, min.X, max.X),
                Clamp(target.Y, min.Y, max.Y),
                viewElevation);
        }

        private static Outline GetElementOutline(Element element, Autodesk.Revit.DB.View view)
        {
            try
            {
                BoundingBoxXYZ box = element?.get_BoundingBox(view);
                if (box == null)
                    return null;

                Transform transform = box.Transform ?? Transform.Identity;
                bool found = false;
                XYZ min = null;
                XYZ max = null;
                foreach (XYZ point in GetBoxCorners(box))
                {
                    XYZ transformed = transform.OfPoint(point);
                    min = !found
                        ? transformed
                        : new XYZ(
                            Math.Min(min.X, transformed.X),
                            Math.Min(min.Y, transformed.Y),
                            Math.Min(min.Z, transformed.Z));
                    max = !found
                        ? transformed
                        : new XYZ(
                            Math.Max(max.X, transformed.X),
                            Math.Max(max.Y, transformed.Y),
                            Math.Max(max.Z, transformed.Z));
                    found = true;
                }

                return found ? new Outline(min, max) : null;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<XYZ> GetBoxCorners(BoundingBoxXYZ box)
        {
            yield return new XYZ(box.Min.X, box.Min.Y, box.Min.Z);
            yield return new XYZ(box.Min.X, box.Min.Y, box.Max.Z);
            yield return new XYZ(box.Min.X, box.Max.Y, box.Min.Z);
            yield return new XYZ(box.Min.X, box.Max.Y, box.Max.Z);
            yield return new XYZ(box.Max.X, box.Min.Y, box.Min.Z);
            yield return new XYZ(box.Max.X, box.Min.Y, box.Max.Z);
            yield return new XYZ(box.Max.X, box.Max.Y, box.Min.Z);
            yield return new XYZ(box.Max.X, box.Max.Y, box.Max.Z);
        }

        private static XYZ GetOutlineCenter(Outline outline, double viewElevation)
        {
            XYZ min = outline.MinimumPoint;
            XYZ max = outline.MaximumPoint;
            return new XYZ(
                (min.X + max.X) * 0.5,
                (min.Y + max.Y) * 0.5,
                viewElevation);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (min > max)
            {
                double swap = min;
                min = max;
                max = swap;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static void CreateMarkerCircle(
            Document document,
            ViewPlan view,
            XYZ center,
            double radius,
            XYZ right,
            XYZ up,
            GraphicsStyle markerStyle)
        {
            Arc firstHalf = Arc.Create(center, radius, 0, Math.PI, right, up);
            Arc secondHalf = Arc.Create(center, radius, Math.PI, Math.PI * 2, right, up);
            Line horizontal = Line.CreateBound(center - right * radius, center + right * radius);
            Line vertical = Line.CreateBound(center - up * radius, center + up * radius);

            SetMarkerStyle(document.Create.NewDetailCurve(view, firstHalf), markerStyle);
            SetMarkerStyle(document.Create.NewDetailCurve(view, secondHalf), markerStyle);
            SetMarkerStyle(document.Create.NewDetailCurve(view, horizontal), markerStyle);
            SetMarkerStyle(document.Create.NewDetailCurve(view, vertical), markerStyle);
        }

        private static void SetMarkerStyle(CurveElement curve, GraphicsStyle markerStyle)
        {
            if (curve != null && markerStyle != null)
                curve.LineStyle = markerStyle;
        }
    }
}
