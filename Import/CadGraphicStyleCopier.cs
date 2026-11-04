using ACadSharp.Entities;
using ACadSharp.Tables;
using System;

namespace RevitFamilyImporter.Import
{
    internal static class CadGraphicStyleCopier
    {
        public static void CopyEntityOverrides(Entity target, Entity source)
        {
            if (target == null || source == null)
                return;

            Try(() => target.Color = source.Color);
            Try(() => target.LineWeight = source.LineWeight);
            Try(() => target.LineTypeScale = source.LineTypeScale);
            Try(() => target.Transparency = source.Transparency);
        }

        public static void CopyLayerGraphics(
            Layer target,
            Layer source,
            Func<LineType, LineType> ensureLineType)
        {
            if (target == null || source == null)
                return;

            Try(() => target.Color = source.Color);
            Try(() => target.LineWeight = source.LineWeight);
            Try(() => target.PlotFlag = source.PlotFlag);

            if (source.LineType != null && ensureLineType != null)
                Try(() => target.LineType = ensureLineType(source.LineType));
        }

        private static void Try(Action action)
        {
            try { action?.Invoke(); }
            catch { }
        }
    }
}

