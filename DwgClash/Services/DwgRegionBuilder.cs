using System;
using System.Collections.Generic;
using System.Threading;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class DwgRegionBuilder
    {
        public List<DwgRegion> Build(IEnumerable<DwgRegion> entities, Action<string, int, int> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new List<DwgRegion>();
            foreach (DwgRegion region in entities ?? Array.Empty<DwgRegion>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (region == null || region.Points.Count < 2) continue;
                if (result.Count % 100 == 0) progress?.Invoke($"Preparando regiões do DWG: {result.Count:N0}...", 0, 0);
                region.Number = result.Count + 1;
                // A chave é calculada somente para regiões que realmente geram conflito.
                region.CompletionGeometryKey = string.Empty;
                result.Add(region);
            }
            return result;
        }
    }
}
