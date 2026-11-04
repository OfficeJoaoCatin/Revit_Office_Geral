using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RevitFamilyImporter.Models
{
    public class SheetTitleBlockRow : INotifyPropertyChanged
    {
        private bool _selected = true;

        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string CurrentRevision { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new List<string>();
        public List<string> Headers { get; set; } = new List<string>();
        public string SearchText { get; set; } = string.Empty;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
