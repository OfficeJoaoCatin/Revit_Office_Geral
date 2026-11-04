using RevitFamilyImporter.Models.LegendLibrary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RevitFamilyImporter.UI.LegendLibrary
{
    public class LegendSectionViewModel : INotifyPropertyChanged
    {
        private string _title;
        private LegendItemViewModel _selectedItem;

        public LegendSectionViewModel(string title)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "GRUPO" : title;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LegendItemViewModel> Items { get; } =
            new ObservableCollection<LegendItemViewModel>();

        public string Title
        {
            get => _title;
            set
            {
                if (_title == value)
                    return;

                _title = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Header));
            }
        }

        public string Header => $"{Title} ({Items.Count})";

        public LegendItemViewModel SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value)
                    return;

                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public LegendSection ToModel()
        {
            var section = new LegendSection
            {
                Title = string.IsNullOrWhiteSpace(Title) ? "GRUPO" : Title.Trim()
            };

            foreach (LegendItemViewModel item in Items)
                section.Items.Add(item.Item);

            return section;
        }

        public void RefreshHeader()
        {
            OnPropertyChanged(nameof(Header));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


