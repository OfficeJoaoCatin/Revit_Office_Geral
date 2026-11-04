using System.Collections.Generic;
using System.Linq;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class DwgRegionBuilder
    {
        public List<DwgRegion> Build(IEnumerable<DwgRegion> entities)
        {
            return (entities ?? Enumerable.Empty<DwgRegion>())
                .Where(region => region != null && region.Points.Count >= 2)
                .Select((region, index) =>
                {
                    region.Number = index + 1;
                    return region;
                })
                .ToList();
        }
    }
}
