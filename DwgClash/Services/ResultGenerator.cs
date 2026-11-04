using System.Collections.Generic;
using System.Linq;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class ResultGenerator
    {
        private readonly IntersectionService _intersection = new IntersectionService();

        public DwgClashReport Generate(DwgClashSettings settings, IList<RevitElementClashInfo> elements, IList<DwgRegion> regions)
        {
            DwgClashReport report = new DwgClashReport
            {
                ViewName = settings.View?.Name ?? string.Empty,
                ImportName = settings.ImportInstance?.Name ?? string.Empty,
                Layer = settings.Layer,
                ModeText = settings.Mode == DwgClashAnalysisMode.Center ? "Centro" : "Interseção",
                ClosedRegionText = settings.ClosedRegionMode == DwgClashClosedRegionMode.AreaOverlap
                    ? "Interior de regiões: ligado"
                    : "Somente bordas de regiões fechadas",
                ElementsAnalyzed = elements?.Count ?? 0
            };

            if (elements == null || regions == null)
                return report;

            foreach (RevitElementClashInfo element in elements)
            {
                foreach (DwgRegion region in regions)
                {
                    if (!_intersection.Matches(element, region, settings.Mode, settings.ClosedRegionMode,
                            settings.ToleranceFeet, out double distance))
                        continue;

                    report.Results.Add(new DwgClashResult
                    {
                        Number = report.Results.Count + 1,
                        Element = element,
                        Region = region,
                        DistanceFeet = distance == double.MaxValue ? 0 : distance
                    });
                }
            }

            return report;
        }
    }
}
