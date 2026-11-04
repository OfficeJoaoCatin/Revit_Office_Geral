using RevitFamilyImporter.Models.LegendLibrary;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace RevitFamilyImporter.UI.LegendLibrary
{
    public class LegendItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public LegendItemViewModel(LegendItem item, ImageSource preview)
        {
            Item = item;
            Preview = preview;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LegendItem Item { get; }

        public ImageSource Preview { get; }

        public int Id => Item.Id;

        public string Name => Item.Name;

        public string Size => $"{Item.Width:0.##} x {Item.Height:0.##}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


