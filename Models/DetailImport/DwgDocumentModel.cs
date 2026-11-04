using ACadSharp;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class DwgDocumentModel
    {
        public string FilePath { get; set; }

        public CadDocument NativeDocument { get; set; }

        public List<DwgEntityInfo> Entities { get; } = new List<DwgEntityInfo>();

        public List<string> Layers { get; } = new List<string>();

        public List<DwgEntityInfo> Texts { get; } = new List<DwgEntityInfo>();

        public List<string> Blocks { get; } = new List<string>();
    }
}

