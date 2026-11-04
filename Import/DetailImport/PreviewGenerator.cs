using RevitFamilyImporter.Models.DetailImport;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class PreviewGenerator
    {
        public ImageSource Generate(DetailRegion region, double width = 220, double height = 160)
        {
            if (region == null)
                return null;

            var stopwatch = Stopwatch.StartNew();
            DrawingGroup group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(WpfBrushes.White, new WpfPen(WpfBrushes.LightGray, 1), new RectangleGeometry(new Rect(0, 0, width, height))));

            DwgBounds bounds = region.Bounds;
            double margin = 10;
            double scaleX = (width - margin * 2) / Math.Max(bounds.Width, 0.0001);
            double scaleY = (height - margin * 2) / Math.Max(bounds.Height, 0.0001);
            double scale = Math.Min(scaleX, scaleY);

            foreach (DwgEntityInfo entity in region.Entities)
            {
                WpfPen pen = CreatePen(entity.Kind);

                if (entity.Kind == DwgEntityKind.Line && entity.Points.Count >= 2)
                {
                    StreamGeometry geometry = new StreamGeometry();
                    using (StreamGeometryContext context = geometry.Open())
                    {
                        context.BeginFigure(Map(entity.Points[0], bounds, height, margin, scale), false, false);
                        context.LineTo(Map(entity.Points[1], bounds, height, margin, scale), true, false);
                    }

                    geometry.Freeze();
                    group.Children.Add(new GeometryDrawing(null, pen, geometry));
                }
                else if (entity.Kind == DwgEntityKind.Polyline && entity.Points.Count >= 2)
                {
                    StreamGeometry geometry = new StreamGeometry();
                    using (StreamGeometryContext context = geometry.Open())
                    {
                        context.BeginFigure(Map(entity.Points[0], bounds, height, margin, scale), false, DwgGeometryUtilities.IsClosedPolyline(entity.NativeEntity));
                        foreach (DwgPoint2 point in entity.Points.Skip(1))
                            context.LineTo(Map(point, bounds, height, margin, scale), true, false);
                    }

                    geometry.Freeze();
                    group.Children.Add(new GeometryDrawing(null, pen, geometry));
                }
                else if (entity.Kind == DwgEntityKind.BlockReference ||
                         entity.Kind == DwgEntityKind.Text ||
                         entity.Kind == DwgEntityKind.MText ||
                         entity.Kind == DwgEntityKind.Hatch)
                {
                    Rect rect = Map(entity.Bounds, bounds, height, margin, scale);
                    if (rect.Width < 3) rect.Width = 3;
                    if (rect.Height < 3) rect.Height = 3;
                    WpfBrush fill = entity.Kind == DwgEntityKind.Hatch
                        ? new SolidColorBrush(WpfColor.FromArgb(32, 36, 90, 110))
                        : null;
                    group.Children.Add(new GeometryDrawing(fill, pen, new RectangleGeometry(rect)));
                }
            }

            group.Freeze();
            stopwatch.Stop();
            return new DrawingImage(group);
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

        private static WpfPoint Map(DwgPoint2 point, DwgBounds bounds, double height, double margin, double scale)
        {
            double x = margin + (point.X - bounds.MinX) * scale;
            double y = height - margin - (point.Y - bounds.MinY) * scale;
            return new WpfPoint(x, y);
        }

        private static Rect Map(DwgBounds entityBounds, DwgBounds regionBounds, double height, double margin, double scale)
        {
            WpfPoint min = Map(new DwgPoint2(entityBounds.MinX, entityBounds.MinY), regionBounds, height, margin, scale);
            WpfPoint max = Map(new DwgPoint2(entityBounds.MaxX, entityBounds.MaxY), regionBounds, height, margin, scale);
            return new Rect(
                Math.Min(min.X, max.X),
                Math.Min(min.Y, max.Y),
                Math.Abs(max.X - min.X),
                Math.Abs(max.Y - min.Y));
        }
    }
}




