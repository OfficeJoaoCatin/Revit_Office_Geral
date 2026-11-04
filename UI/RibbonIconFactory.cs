using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace RevitFamilyImporter.UI
{
    internal enum RibbonIconKind
    {
        ImportFamilies,
        BatchExport,
        SaveAll,
        SyncCentral,
        DetailImport,
        EditParameters,
        LegendCad,
        SheetTitleBlock,
        CleanDuplicates,
        SelectIdentical,
        VisibilityInspector,
        LinkConflicts
    }

    internal static class RibbonIconFactory
    {
        public static BitmapSource Create(RibbonIconKind kind, int size)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                double scale = size / 32.0;
                context.PushTransform(new ScaleTransform(scale, scale));

                switch (kind)
                {
                    case RibbonIconKind.ImportFamilies:
                        DrawImportFamilies(context);
                        break;
                    case RibbonIconKind.BatchExport:
                        DrawBatchExport(context);
                        break;
                    case RibbonIconKind.SaveAll:
                        DrawSaveAll(context);
                        break;
                    case RibbonIconKind.SyncCentral:
                        DrawSyncCentral(context);
                        break;
                    case RibbonIconKind.DetailImport:
                        DrawDetailImport(context);
                        break;
                    case RibbonIconKind.EditParameters:
                        DrawEditParameters(context);
                        break;
                    case RibbonIconKind.LegendCad:
                        DrawLegendCad(context);
                        break;
                    case RibbonIconKind.SheetTitleBlock:
                        DrawSheetTitleBlock(context);
                        break;
                    case RibbonIconKind.CleanDuplicates:
                        DrawCleanDuplicates(context);
                        break;
                    case RibbonIconKind.SelectIdentical:
                        DrawSelectIdentical(context);
                        break;
                    case RibbonIconKind.VisibilityInspector:
                        DrawVisibilityInspector(context);
                        break;
                    case RibbonIconKind.LinkConflicts:
                        DrawLinkConflicts(context);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }

                context.Pop();
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawImportFamilies(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.35);

            context.DrawRoundedRectangle(paper, border, new Rect(9, 5, 17, 22), 2, 2);
            context.DrawRectangle(CreateBrush("#DCEBFF"), null, new Rect(12, 8, 11, 4));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(12, 15), new WpfPoint(22, 15));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(12, 18), new WpfPoint(22, 18));

            context.DrawRoundedRectangle(green, null, new Rect(4, 14, 14, 9), 1.8, 1.8);
            context.DrawRectangle(CreateBrush("#49B391"), null, new Rect(6, 11, 8, 4));

            WpfPen arrow = CreatePen(blue, 2.4);
            context.DrawLine(arrow, new WpfPoint(5, 26), new WpfPoint(17, 26));
            context.DrawLine(arrow, new WpfPoint(14, 22), new WpfPoint(18, 26));
            context.DrawLine(arrow, new WpfPoint(14, 30), new WpfPoint(18, 26));
        }

        private static void DrawBatchExport(DrawingContext context)
        {
            WpfBrush accent = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.35);

            context.DrawRoundedRectangle(CreateBrush("#DCEBFF"), border, new Rect(6, 7, 15, 18), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#F2F7FF"), border, new Rect(9, 5, 15, 18), 2, 2);
            context.DrawRoundedRectangle(paper, border, new Rect(12, 3, 15, 18), 2, 2);

            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(15, 9), new WpfPoint(24, 9));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(15, 12), new WpfPoint(24, 12));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(15, 15), new WpfPoint(22, 15));

            WpfPen arrow = CreatePen(accent, 2.7);
            context.DrawLine(arrow, new WpfPoint(11, 26), new WpfPoint(26, 26));
            context.DrawLine(arrow, new WpfPoint(22, 21), new WpfPoint(27, 26));
            context.DrawLine(arrow, new WpfPoint(22, 31), new WpfPoint(27, 26));
        }

        private static void DrawSaveAll(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush dark = CreateBrush("#174A72");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfPen border = CreatePen(dark, 1.35);

            context.DrawRoundedRectangle(CreateBrush("#DCEBFF"), border, new Rect(6, 8, 17, 18), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#F2F7FF"), border, new Rect(9, 6, 17, 18), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#FFFFFF"), border, new Rect(12, 4, 17, 18), 2, 2);

            context.DrawRectangle(blue, null, new Rect(15, 6, 9, 5));
            context.DrawRectangle(CreateBrush("#EAF2FF"), null, new Rect(16, 15, 10, 5));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(17, 17.5), new WpfPoint(25, 17.5));

            context.DrawEllipse(green, null, new WpfPoint(11, 24), 5, 5);
            WpfPen check = CreatePen("#FFFFFF", 2.2);
            context.DrawLine(check, new WpfPoint(8.5, 24), new WpfPoint(10.5, 26));
            context.DrawLine(check, new WpfPoint(10.5, 26), new WpfPoint(14.2, 21.8));
        }

        private static void DrawSyncCentral(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfPen bluePen = CreatePen(blue, 2.5);
            WpfPen greenPen = CreatePen(green, 2.5);

            context.DrawEllipse(CreateBrush("#F2F7FF"), CreatePen("#174A72", 1.2), new WpfPoint(16, 16), 8.5, 8.5);
            context.DrawRectangle(CreateBrush("#DCEBFF"), null, new Rect(12, 11, 8, 10));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(14, 14), new WpfPoint(18, 14));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(14, 17), new WpfPoint(18, 17));

            context.DrawGeometry(null, bluePen, Arc(new WpfPoint(25, 14), new WpfPoint(13, 7), SweepDirection.Counterclockwise));
            context.DrawGeometry(null, greenPen, Arc(new WpfPoint(7, 18), new WpfPoint(20, 25), SweepDirection.Counterclockwise));

            DrawTriangle(context, blue, new WpfPoint(12, 7), new WpfPoint(16, 5.5), new WpfPoint(15, 10));
            DrawTriangle(context, green, new WpfPoint(21, 25), new WpfPoint(17, 26.5), new WpfPoint(18, 22));
        }

        private static void DrawDetailImport(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);
            WpfPen grid = CreatePen("#9CB6D9", 0.9);

            context.DrawRoundedRectangle(paper, border, new Rect(6, 5, 20, 22), 2, 2);
            context.DrawLine(grid, new WpfPoint(10, 12), new WpfPoint(23, 12));
            context.DrawLine(grid, new WpfPoint(10, 17), new WpfPoint(23, 17));
            context.DrawLine(grid, new WpfPoint(14, 8), new WpfPoint(14, 24));
            context.DrawLine(grid, new WpfPoint(20, 8), new WpfPoint(20, 24));

            context.DrawRectangle(CreateBrush("#DCEBFF"), CreatePen(blue, 1.2), new Rect(9, 8, 5, 4));
            context.DrawRectangle(CreateBrush("#E9F8F3"), CreatePen(green, 1.2), new Rect(15, 13, 5, 4));
            context.DrawRectangle(CreateBrush("#DCEBFF"), CreatePen(blue, 1.2), new Rect(21, 18, 4, 4));

            WpfPen arrow = CreatePen(green, 2.4);
            context.DrawLine(arrow, new WpfPoint(5, 29), new WpfPoint(18, 29));
            context.DrawLine(arrow, new WpfPoint(15, 25), new WpfPoint(19, 29));
            context.DrawLine(arrow, new WpfPoint(15, 31), new WpfPoint(19, 29));
        }

        private static void DrawEditParameters(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);

            context.DrawRoundedRectangle(paper, border, new Rect(6, 5, 20, 22), 2, 2);
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(10, 11), new WpfPoint(22, 11));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(10, 16), new WpfPoint(22, 16));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(10, 21), new WpfPoint(18, 21));

            context.DrawEllipse(CreateBrush("#DCEBFF"), CreatePen(blue, 1.1), new WpfPoint(8.5, 11), 1.7, 1.7);
            context.DrawEllipse(CreateBrush("#E9F8F3"), CreatePen(green, 1.1), new WpfPoint(8.5, 16), 1.7, 1.7);
            context.DrawEllipse(CreateBrush("#DCEBFF"), CreatePen(blue, 1.1), new WpfPoint(8.5, 21), 1.7, 1.7);

            WpfPen pencil = CreatePen(green, 2.2);
            context.DrawLine(pencil, new WpfPoint(18, 27), new WpfPoint(27, 18));
            DrawTriangle(context, green, new WpfPoint(26.2, 17.2), new WpfPoint(29, 15), new WpfPoint(27.8, 18.6));
        }

        private static void DrawLegendCad(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfPen border = CreatePen("#174A72", 1.25);
            WpfPen grid = CreatePen("#9CB6D9", 0.85);

            context.DrawRoundedRectangle(CreateBrush("#FFFFFF"), border, new Rect(5, 5, 22, 22), 2, 2);
            context.DrawLine(grid, new WpfPoint(8, 12), new WpfPoint(24, 12));
            context.DrawLine(grid, new WpfPoint(8, 18), new WpfPoint(24, 18));
            context.DrawLine(grid, new WpfPoint(13, 8), new WpfPoint(13, 24));

            context.DrawRectangle(CreateBrush("#DCEBFF"), CreatePen(blue, 1.1), new Rect(8, 8, 4, 3));
            context.DrawEllipse(CreateBrush("#E9F8F3"), CreatePen(green, 1.1), new WpfPoint(10, 15), 2, 2);
            context.DrawLine(CreatePen(blue, 1.4), new WpfPoint(16, 9), new WpfPoint(23, 9));
            context.DrawLine(CreatePen(green, 1.4), new WpfPoint(16, 15), new WpfPoint(23, 15));
            context.DrawLine(CreatePen(blue, 1.4), new WpfPoint(16, 21), new WpfPoint(23, 21));

            context.DrawRoundedRectangle(green, null, new Rect(3, 24, 13, 5), 1.4, 1.4);
            context.DrawText(
                new FormattedText("CAD", System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 4.2, CreateBrush("#FFFFFF"), 96),
                new WpfPoint(5, 23.9));
        }

        private static void DrawSheetTitleBlock(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);
            WpfPen grid = CreatePen("#9CB6D9", 0.85);

            context.DrawRoundedRectangle(paper, border, new Rect(5, 4, 22, 24), 2, 2);
            context.DrawLine(grid, new WpfPoint(8, 20), new WpfPoint(24, 20));
            context.DrawLine(grid, new WpfPoint(14, 20), new WpfPoint(14, 27));
            context.DrawLine(grid, new WpfPoint(20, 20), new WpfPoint(20, 27));
            context.DrawLine(grid, new WpfPoint(8, 23.5), new WpfPoint(24, 23.5));

            context.DrawRectangle(CreateBrush("#DCEBFF"), CreatePen(blue, 1.1), new Rect(8, 7, 16, 9));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(10, 10), new WpfPoint(22, 10));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(10, 13), new WpfPoint(18, 13));

            context.DrawEllipse(green, null, new WpfPoint(23.5, 8), 4.5, 4.5);
            WpfPen check = CreatePen("#FFFFFF", 1.8);
            context.DrawLine(check, new WpfPoint(21.3, 8), new WpfPoint(23, 9.8));
            context.DrawLine(check, new WpfPoint(23, 9.8), new WpfPoint(26.2, 5.8));
        }

        private static void DrawCleanDuplicates(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush red = CreateBrush("#DC2626");
            WpfPen border = CreatePen("#174A72", 1.25);

            context.DrawRoundedRectangle(CreateBrush("#DCEBFF"), border, new Rect(7, 7, 13, 13), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#FFFFFF"), border, new Rect(11, 5, 13, 13), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#E9F8F3"), CreatePen(green, 1.15), new Rect(14, 8, 7, 7), 1.3, 1.3);

            context.DrawEllipse(blue, null, new WpfPoint(12, 23), 6, 6);
            WpfPen check = CreatePen("#FFFFFF", 2.1);
            context.DrawLine(check, new WpfPoint(9, 23), new WpfPoint(11.4, 25.4));
            context.DrawLine(check, new WpfPoint(11.4, 25.4), new WpfPoint(15.6, 20.7));

            WpfPen delete = CreatePen(red, 2.4);
            context.DrawLine(delete, new WpfPoint(21, 22), new WpfPoint(27, 28));
            context.DrawLine(delete, new WpfPoint(27, 22), new WpfPoint(21, 28));
        }

        private static void DrawVisibilityInspector(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);

            context.DrawRoundedRectangle(paper, border, new Rect(5, 6, 22, 19), 2, 2);
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(9, 12), new WpfPoint(22, 12));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(9, 17), new WpfPoint(19, 17));
            context.DrawEllipse(CreateBrush("#E9F8F3"), CreatePen(green, 1.1), new WpfPoint(11, 12), 2, 2);
            context.DrawEllipse(CreateBrush("#DCEBFF"), CreatePen(blue, 1.1), new WpfPoint(11, 17), 2, 2);

            context.DrawEllipse(null, CreatePen(blue, 2.1), new WpfPoint(20, 22), 5, 5);
            context.DrawLine(CreatePen(blue, 2.4), new WpfPoint(23.8, 25.8), new WpfPoint(28.5, 30.5));
            context.DrawEllipse(green, null, new WpfPoint(20, 22), 1.6, 1.6);
        }

        private static void DrawLinkConflicts(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush orange = CreateBrush("#EA580C");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);

            context.DrawRoundedRectangle(paper, border, new Rect(4, 7, 12, 18), 2, 2);
            context.DrawRoundedRectangle(CreateBrush("#FFF7ED"), CreatePen(orange, 1.2), new Rect(16, 7, 12, 18), 2, 2);
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(7, 12), new WpfPoint(13, 12));
            context.DrawLine(CreatePen("#9CB6D9", 1), new WpfPoint(19, 12), new WpfPoint(25, 12));

            context.DrawEllipse(blue, null, new WpfPoint(13.5, 21), 4, 4);
            context.DrawEllipse(orange, null, new WpfPoint(18.5, 21), 4, 4);
            WpfPen warning = CreatePen("#FFFFFF", 1.6);
            context.DrawLine(warning, new WpfPoint(16, 17.5), new WpfPoint(16, 24.5));
            context.DrawLine(warning, new WpfPoint(16, 24.5), new WpfPoint(22, 17.5));
        }

        private static void DrawSelectIdentical(DrawingContext context)
        {
            WpfBrush blue = CreateBrush("#2563EB");
            WpfBrush green = CreateBrush("#2D8F6F");
            WpfBrush paper = CreateBrush("#FFFFFF");
            WpfPen border = CreatePen("#174A72", 1.25);

            context.DrawRoundedRectangle(paper, border, new Rect(7, 6, 8, 8), 1.5, 1.5);
            context.DrawRoundedRectangle(CreateBrush("#E9F8F3"), CreatePen(green, 1.2), new Rect(17, 6, 8, 8), 1.5, 1.5);
            context.DrawRoundedRectangle(CreateBrush("#DCEBFF"), CreatePen(blue, 1.2), new Rect(7, 18, 8, 8), 1.5, 1.5);
            context.DrawRoundedRectangle(paper, border, new Rect(17, 18, 8, 8), 1.5, 1.5);

            WpfPen equalPen = CreatePen(green, 1.7);
            context.DrawLine(equalPen, new WpfPoint(9, 10), new WpfPoint(13, 10));
            context.DrawLine(equalPen, new WpfPoint(19, 10), new WpfPoint(23, 10));
            context.DrawLine(equalPen, new WpfPoint(9, 22), new WpfPoint(13, 22));
            context.DrawLine(equalPen, new WpfPoint(19, 22), new WpfPoint(23, 22));

            context.DrawEllipse(null, CreatePen(blue, 2), new WpfPoint(20, 20), 8.5, 8.5);
            context.DrawLine(CreatePen(blue, 2.3), new WpfPoint(25.5, 25.5), new WpfPoint(29.5, 29.5));
            context.DrawEllipse(green, null, new WpfPoint(20, 20), 1.5, 1.5);
        }
        private static Geometry Arc(WpfPoint start, WpfPoint end, SweepDirection direction)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.ArcTo(end, new WpfSize(10, 10), 0, false, direction, true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static void DrawTriangle(DrawingContext context, WpfBrush brush, WpfPoint a, WpfPoint b, WpfPoint c)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext stream = geometry.Open())
            {
                stream.BeginFigure(a, true, true);
                stream.LineTo(b, true, false);
                stream.LineTo(c, true, false);
            }

            geometry.Freeze();
            context.DrawGeometry(brush, null, geometry);
        }

        private static WpfBrush CreateBrush(string color)
        {
            SolidColorBrush brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private static WpfPen CreatePen(string color, double thickness)
        {
            WpfPen pen = new WpfPen(CreateBrush(color), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }

        private static WpfPen CreatePen(WpfBrush brush, double thickness)
        {
            WpfPen pen = new WpfPen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }
    }
}






