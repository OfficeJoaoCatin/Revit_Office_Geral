using Autodesk.Revit.DB;
using RevitColor = Autodesk.Revit.DB.Color;

namespace RevitFamilyImporter.VisibilityInspector.Utils
{
    internal static class GraphicOverrideFormatter
    {
        public static string ColorText(RevitColor color)
        {
            if (color == null || !color.IsValid)
                return string.Empty;

            return $"RGB({color.Red},{color.Green},{color.Blue})";
        }

        public static bool HasAnyOverride(OverrideGraphicSettings settings)
        {
            if (settings == null)
                return false;

            return settings.ProjectionLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                   settings.CutLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                   settings.Transparency >= 0 ||
                   settings.Halftone ||
                   IsValidColor(settings.ProjectionLineColor) ||
                   IsValidColor(settings.CutLineColor) ||
                   IsValidColor(settings.SurfaceForegroundPatternColor) ||
                   settings.ProjectionLinePatternId != ElementId.InvalidElementId ||
                   settings.SurfaceForegroundPatternId != ElementId.InvalidElementId;
        }

        public static string Summary(OverrideGraphicSettings settings)
        {
            if (settings == null)
                return string.Empty;

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            string color = ColorText(settings.ProjectionLineColor);
            if (!string.IsNullOrWhiteSpace(color)) parts.Add($"Linha {color}");
            if (settings.ProjectionLineWeight != OverrideGraphicSettings.InvalidPenNumber) parts.Add($"Peso {settings.ProjectionLineWeight}");
            if (settings.Transparency >= 0) parts.Add($"Transparência {settings.Transparency}");
            if (settings.Halftone) parts.Add("Meio-tom");
            string surface = ColorText(settings.SurfaceForegroundPatternColor);
            if (!string.IsNullOrWhiteSpace(surface)) parts.Add($"Superfície {surface}");
            return parts.Count == 0 ? "Sem override gráfico explícito" : string.Join("; ", parts);
        }

        private static bool IsValidColor(RevitColor color)
        {
            return color != null && color.IsValid;
        }
    }
}
