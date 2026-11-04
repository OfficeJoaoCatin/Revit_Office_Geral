using RevitFamilyImporter.Models.DetailImport;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class LegendItem
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public List<DwgPoint2> Boundary { get; } = new List<DwgPoint2>();

        public DwgBounds Bounds { get; set; }

        public List<DwgEntityInfo> Entities { get; } = new List<DwgEntityInfo>();

        public DwgEntityInfo BlockEntity { get; set; }

        public DwgEntityInfo TextEntity { get; set; }

        public double Width => Bounds.Width;

        public double Height => Bounds.Height;
    }
}

