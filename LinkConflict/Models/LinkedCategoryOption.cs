using Autodesk.Revit.DB;
using System.ComponentModel;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public class LinkedCategoryOption : INotifyPropertyChanged
    {
        private bool _isSelected;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public ElementId CategoryId { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string DisplayName => $"{Name} ({Count:N0})";
    }
}
