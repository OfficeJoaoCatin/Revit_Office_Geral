using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class ResultGenerator
    {
        private readonly IntersectionService _intersection = new IntersectionService();

        public DwgClashReport Generate(DwgClashSettings settings, IList<RevitElementClashInfo> elements, IList<DwgRegion> regions,
            Action<string, int, int> progress = null, CancellationToken cancellationToken = default)
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

            progress?.Invoke("Indexando regiões do DWG...", 0, 0);
            var index = new DwgRegionSpatialIndex(regions, cancellationToken);
            var grouped = new Dictionary<(string, int), DwgClashResult>();
            for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RevitElementClashInfo element = elements[elementIndex];
                if (elementIndex % 25 == 0) progress?.Invoke("Comparando elementos com o DWG...", elementIndex, elements.Count);
                foreach (int regionIndex in index.Query(element, settings.ToleranceFeet, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DwgRegion region = regions[regionIndex];
                    if (!_intersection.Matches(element, region, settings.Mode, settings.ClosedRegionMode,
                            settings.ToleranceFeet, out double distance, cancellationToken))
                        continue;

                    if (string.IsNullOrEmpty(region.CompletionGeometryKey))
                        region.CompletionGeometryKey = DwgClashCompletionKey.ForRegion(region, cancellationToken);
                    string owner = string.IsNullOrWhiteSpace(element.OwnerUniqueId) ? element.UniqueId : element.OwnerUniqueId;
                    // Sem identidade estável não agrupa elementos diferentes por engano.
                    if (string.IsNullOrWhiteSpace(owner)) owner = "element-index:" + elementIndex;
                    var groupKey = (owner, regionIndex);
                    if (grouped.TryGetValue(groupKey, out DwgClashResult existing))
                    {
                        if (!string.IsNullOrWhiteSpace(element.UniqueId)) existing.PreviousCompletionKeys.Add(element.UniqueId + "|" + region.CompletionGeometryKey);
                        // Prefere a família mãe quando ela também conflita; mantém a geometria
                        // aninhada caso apenas o componente tenha sido atingido.
                        if (element.UniqueId == owner && existing.Element.UniqueId != owner)
                        {
                            existing.Element = element;
                            existing.DistanceFeet = distance == double.MaxValue ? 0 : distance;
                        }
                        continue;
                    }
                    var result = new DwgClashResult
                    {
                        Number = report.Results.Count + 1,
                        Element = element,
                        Region = region,
                        DistanceFeet = distance == double.MaxValue ? 0 : distance
                    };
                    if (!string.IsNullOrWhiteSpace(element.UniqueId)) result.PreviousCompletionKeys.Add(element.UniqueId + "|" + region.CompletionGeometryKey);
                    grouped.Add(groupKey, result);
                    report.Results.Add(result);
                }
            }
            progress?.Invoke("Comparação concluída.", elements.Count, elements.Count);

            return report;
        }
    }
}
