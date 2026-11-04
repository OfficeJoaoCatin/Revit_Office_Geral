using System.Collections.Generic;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class DetailRegion
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public List<DwgPoint2> Boundary { get; } = new List<DwgPoint2>();

        public DwgBounds Bounds { get; set; }

        public DwgPoint2 Center { get; set; }

        public DwgEntityInfo BoundaryEntity { get; set; }

        public List<DwgEntityInfo> Entities { get; } = new List<DwgEntityInfo>();
    }
}

