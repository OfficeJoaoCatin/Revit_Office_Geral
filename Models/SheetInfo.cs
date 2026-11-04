using System.Runtime.Serialization;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Models
{
    public class SheetInfo : ObservableObject
    {
        private bool _selected = true;

        [IgnoreDataMember]
        public ElementId? Id { get; set; }

        public long IdValue { get; set; }

        public string DocumentKey { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Discipline { get; set; } = string.Empty;

        public bool Issued { get; set; }

        public bool CanBePrinted { get; set; }

        public bool Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public string DisplayName => $"{Number} - {Name}";
    }
}
