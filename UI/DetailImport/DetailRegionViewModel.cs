using RevitFamilyImporter.Models.DetailImport;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace RevitFamilyImporter.UI.DetailImport
{
    public class DetailRegionViewModel : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public DetailRegionViewModel(DetailRegion region, ImageSource preview)
        {
            Region = region;
            Preview = preview;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DetailRegion Region { get; }

        public ImageSource Preview { get; }

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

        public int Id => Region.Id;

        public string Name => Region.Name;

        public string Width => Region.Bounds.Width.ToString("0.##");

        public string Height => Region.Bounds.Height.ToString("0.##");

        public int EntityCount => Region.Entities.Count;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


