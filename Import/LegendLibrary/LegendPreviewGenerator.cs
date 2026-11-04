using ACadSharp.Entities;
using CSMath;
using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendPreviewGenerator
    {
        public ImageSource GenerateItemPreview(LegendItem item, double width = 240, double height = 170)
        {
            if (item == null)
                return null;

            List<DwgEntityInfo> previewEntities = GetSymbolPreviewEntities(item);
            DwgBounds previewBounds = NormalizePreviewBounds(GetPreviewBounds(previewEntities, item.Bounds));
            return Render(previewEntities, previewBounds, width, height, item.Name, false);
        }

        public ImageSource GenerateCompositionPreview(LegendComposition composition, double width = 520, double height = 420)
        {
            DrawingGroup group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(WpfBrushes.White, new WpfPen(WpfBrushes.LightGray, 1), new RectangleGeometry(new Rect(0, 0, width, height))));

            if (composition == null || composition.PlacedItems.Count == 0)
                return new DrawingImage(group);

            DwgBounds bounds = composition.Bounds.Expand(Math.Max(4, composition.Bounds.Height * 0.03));
            double margin = 14;
            double scale = Math.Min(
                (width - margin * 2) / Math.Max(bounds.Width, 0.0001),
                (height - margin * 2) / Math.Max(bounds.Height, 0.0001));

            foreach (KeyValuePair<string, DwgPoint2> title in composition.SectionTitlePositions)
            {
                System.Windows.Point point = Map(title.Value, bounds, height, margin, scale);
                group.Children.Add(CreateTextDrawing($"DESCRICAO DO SIMBOLO - {title.Key}".ToUpperInvariant(), point, 12, WpfBrushes.Black));
            }

            foreach (LegendPlacedItem placed in composition.PlacedItems)
            {
                foreach (DwgEntityInfo entity in GetSymbolPreviewEntities(placed.Item))
                {
                    DwgPoint2 offset = new DwgPoint2(
                        placed.TargetOrigin.X - placed.Item.Bounds.MinX,
                        placed.TargetOrigin.Y - placed.Item.Bounds.MinY);
                    DrawEntity(group, entity.NativeEntity, entity.Kind, bounds, height, margin, scale, offset, CSMath.Transform.CreateTranslation(new XYZ(offset.X, offset.Y, 0)), 0);
                }

                System.Windows.Point labelPoint = Map(new DwgPoint2(placed.TextX, placed.TextY), bounds, height, margin, scale);
                group.Children.Add(CreateTextDrawing(placed.Item.Name ?? string.Empty, labelPoint, 10, WpfBrushes.Black));
            }

            group.Freeze();
            return new DrawingImage(group);
        }

        private ImageSource Render(IEnumerable<DwgEntityInfo> entities, DwgBounds bounds, double width, double height, string label, bool drawLabel)
        {
            DrawingGroup group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(WpfBrushes.White, new WpfPen(WpfBrushes.LightGray, 1), new RectangleGeometry(new Rect(0, 0, width, height))));

            double margin = 10;
            double scale = Math.Min(
                (width - margin * 2) / Math.Max(bounds.Width, 0.0001),
                (height - margin * 2) / Math.Max(bounds.Height, 0.0001));

            foreach (DwgEntityInfo entity in entities ?? Enumerable.Empty<DwgEntityInfo>())
                DrawEntity(group, entity.NativeEntity, entity.Kind, bounds, height, margin, scale, new DwgPoint2(0, 0), CSMath.Transform.CreateTranslation(new XYZ(0, 0, 0)), 0);

            if (drawLabel && !string.IsNullOrWhiteSpace(label))
                group.Children.Add(CreateTextDrawing(label, new System.Windows.Point(8, height - 18), 10, WpfBrushes.DimGray));

            group.Freeze();
            return new DrawingImage(group);
        }

        private static List<DwgEntityInfo> GetSymbolPreviewEntities(LegendItem item)
        {
            if (item == null)
                return new List<DwgEntityInfo>();

            if (item.BlockEntity != null)
                return new List<DwgEntityInfo> { item.BlockEntity };

            List<DwgEntityInfo> symbolEntities = item.Entities
                .Where(entity => entity.Kind != DwgEntityKind.Text && entity.Kind != DwgEntityKind.MText)
                .ToList();

            return symbolEntities.Count > 0
                ? symbolEntities
                : item.Entities.ToList();
        }

        private static DwgBounds GetPreviewBounds(IList<DwgEntityInfo> entities, DwgBounds fallback)
        {
            if (entities == null || entities.Count == 0)
                return fallback;

            DwgBounds bounds = entities[0].Bounds;
            foreach (DwgEntityInfo entity in entities.Skip(1))
                bounds = bounds.Include(entity.Bounds);

            if (bounds.Width <= 0 && bounds.Height <= 0)
                return fallback;

            return bounds;
        }

        private static DwgBounds NormalizePreviewBounds(DwgBounds bounds)
        {
            double width = Math.Max(bounds.Width, 0.001);
            double height = Math.Max(bounds.Height, 0.001);
            double targetRatio = 1.45;
            double centerX = (bounds.MinX + bounds.MaxX) / 2.0;
            double centerY = (bounds.MinY + bounds.MaxY) / 2.0;

            if (width / height > targetRatio)
                height = width / targetRatio;
            else if (height / width > targetRatio)
                width = height / targetRatio;

            double padding = Math.Max(width, height) * 0.12;
            return new DwgBounds(
                centerX - width / 2.0 - padding,
                centerY - height / 2.0 - padding,
                centerX + width / 2.0 + padding,
                centerY + height / 2.0 + padding);
        }

        private void DrawEntity(
            DrawingGroup group,
            Entity entity,
            DwgEntityKind kind,
            DwgBounds viewBounds,
            double height,
            double margin,
            double scale,
            DwgPoint2 offset,
            CSMath.Transform transform,
            int depth)
        {
            if (entity == null || depth > 4)
                return;

            if (entity is Insert insert && insert.Block != null && insert.Block.Entities != null && insert.Block.Entities.Count > 0)
            {
                CSMath.Transform nested = insert.GetTransform();
                foreach (Entity nestedEntity in insert.Block.Entities)
                {
                    DrawEntity(group, nestedEntity, DwgGeometryUtilities.GetKind(nestedEntity), viewBounds, height, margin, scale, offset, nested, depth + 1);
                }
                return;
            }

            WpfPen pen = CreatePen(kind);
            List<DwgPoint2> points = GetEntityPoints(entity, transform);

            if (kind == DwgEntityKind.Line && points.Count >= 2)
            {
                group.Children.Add(new GeometryDrawing(null, pen, CreatePolylineGeometry(points, false, viewBounds, height, margin, scale, offset)));
                return;
            }

            if (kind == DwgEntityKind.Polyline && points.Count >= 2)
            {
                group.Children.Add(new GeometryDrawing(null, pen, CreatePolylineGeometry(points, DwgGeometryUtilities.IsClosedPolyline(entity), viewBounds, height, margin, scale, offset)));
                return;
            }

            if (kind == DwgEntityKind.Text || kind == DwgEntityKind.MText)
            {
                DwgPoint2 point = points.FirstOrDefault();
                string text = DwgGeometryUtilities.GetText(entity);
                if (!string.IsNullOrWhiteSpace(text))
                    group.Children.Add(CreateTextDrawing(text, Map(point.Add(offset), viewBounds, height, margin, scale), 8, WpfBrushes.DarkSlateBlue));
                return;
            }

            DwgBounds bounds = DwgGeometryUtilities.GetBounds(entity);
            Rect rect = Map(bounds.Translate(offset), viewBounds, height, margin, scale);
            if (rect.Width < 3) rect.Width = 3;
            if (rect.Height < 3) rect.Height = 3;
            WpfBrush fill = kind == DwgEntityKind.Hatch ? new WpfSolidColorBrush(WpfColor.FromArgb(40, 36, 90, 110)) : null;
            group.Children.Add(new GeometryDrawing(fill, pen, new RectangleGeometry(rect)));
        }

        private static StreamGeometry CreatePolylineGeometry(
            IList<DwgPoint2> points,
            bool closed,
            DwgBounds bounds,
            double height,
            double margin,
            double scale,
            DwgPoint2 offset)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(Map(points[0].Add(offset), bounds, height, margin, scale), false, closed);
                foreach (DwgPoint2 point in points.Skip(1))
                    context.LineTo(Map(point.Add(offset), bounds, height, margin, scale), true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static List<DwgPoint2> GetEntityPoints(Entity entity, CSMath.Transform transform)
        {
            return DwgGeometryUtilities.GetGeometryPoints(entity)
                .Select(point => ApplyTransform(point, transform))
                .ToList();
        }

        private static DwgPoint2 ApplyTransform(DwgPoint2 point, CSMath.Transform transform)
        {
            if (transform == null)
                return point;

            XYZ mapped = transform.ApplyTranslation(transform.ApplyRotation(transform.ApplyScale(new XYZ(point.X, point.Y, 0))));
            return new DwgPoint2(mapped.X, mapped.Y);
        }

        private static WpfPen CreatePen(DwgEntityKind kind)
        {
            WpfBrush brush;
            switch (kind)
            {
                case DwgEntityKind.Text:
                case DwgEntityKind.MText:
                    brush = WpfBrushes.DarkSlateBlue;
                    break;
                case DwgEntityKind.BlockReference:
                    brush = WpfBrushes.DarkCyan;
                    break;
                case DwgEntityKind.Hatch:
                    brush = WpfBrushes.SlateGray;
                    break;
                default:
                    brush = WpfBrushes.Black;
                    break;
            }

            WpfPen pen = new WpfPen(brush, 1);
            pen.Freeze();
            return pen;
        }

        private static GeometryDrawing CreateTextDrawing(string text, System.Windows.Point point, double size, WpfBrush brush)
        {
#if NET48
            var formatted = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Arial"),
                size,
                brush);
#else
            var formatted = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Arial"),
                size,
                brush,
                1.0);
#endif
            Geometry geometry = formatted.BuildGeometry(point);
            return new GeometryDrawing(brush, null, geometry);
        }

        private static System.Windows.Point Map(DwgPoint2 point, DwgBounds bounds, double height, double margin, double scale)
        {
            double x = margin + (point.X - bounds.MinX) * scale;
            double y = height - margin - (point.Y - bounds.MinY) * scale;
            return new System.Windows.Point(x, y);
        }

        private static Rect Map(DwgBounds entityBounds, DwgBounds regionBounds, double height, double margin, double scale)
        {
            System.Windows.Point min = Map(new DwgPoint2(entityBounds.MinX, entityBounds.MinY), regionBounds, height, margin, scale);
            System.Windows.Point max = Map(new DwgPoint2(entityBounds.MaxX, entityBounds.MaxY), regionBounds, height, margin, scale);
            return new Rect(
                Math.Min(min.X, max.X),
                Math.Min(min.Y, max.Y),
                Math.Abs(max.X - min.X),
                Math.Abs(max.Y - min.Y));
        }
    }

    internal static class LegendDwgPointExtensions
    {
        public static DwgPoint2 Add(this DwgPoint2 point, DwgPoint2 other)
        {
            return new DwgPoint2(point.X + other.X, point.Y + other.Y);
        }

        public static DwgBounds Translate(this DwgBounds bounds, DwgPoint2 offset)
        {
            return new DwgBounds(
                bounds.MinX + offset.X,
                bounds.MinY + offset.Y,
                bounds.MaxX + offset.X,
                bounds.MaxY + offset.Y);
        }
    }
}




