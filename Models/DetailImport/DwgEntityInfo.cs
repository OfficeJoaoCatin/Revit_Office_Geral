using ACadSharp.Entities;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class DwgEntityInfo
    {
        public int Index { get; set; }

        public string Handle { get; set; }

        public string Layer { get; set; }

        public DwgEntityKind Kind { get; set; }

        public DwgBounds Bounds { get; set; }

        public DwgPoint2 Center { get; set; }

        public string Text { get; set; }

        public Entity NativeEntity { get; set; }

        public List<DwgPoint2> Points { get; } = new List<DwgPoint2>();

        public bool IsText => Kind == DwgEntityKind.Text || Kind == DwgEntityKind.MText;

        public bool IsPolyline => Kind == DwgEntityKind.Polyline;
    }
}

