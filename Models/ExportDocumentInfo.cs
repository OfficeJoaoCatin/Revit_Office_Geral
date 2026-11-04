using System.IO;
using System.Runtime.Serialization;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Models
{
    public class ExportDocumentInfo : ObservableObject
    {
        private bool _selected = true;

        public string ProjectName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        [IgnoreDataMember]
        public Document? Document { get; set; }

        public bool Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public int SheetCount { get; set; }

        public string DisplayPath => string.IsNullOrWhiteSpace(FilePath)
            ? "(sem caminho salvo)"
            : FilePath;

        public string StableKey => string.IsNullOrWhiteSpace(FilePath)
            ? ProjectName
            : Path.GetFullPath(FilePath).ToUpperInvariant();
    }
}
