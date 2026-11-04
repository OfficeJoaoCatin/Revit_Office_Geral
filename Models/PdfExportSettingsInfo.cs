using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Models
{
    public class PdfExportSettingsInfo : ObservableObject
    {
        private bool _combine;
        private int _dpi = 300;
        private bool _center = true;
        private bool _vectorMode = true;

        public string Name { get; set; } = "Padrao";

        public bool Combine
        {
            get => _combine;
            set => SetProperty(ref _combine, value);
        }

        public int DPI
        {
            get => _dpi;
            set => SetProperty(ref _dpi, value);
        }

        public bool Center
        {
            get => _center;
            set => SetProperty(ref _center, value);
        }

        public bool VectorMode
        {
            get => _vectorMode;
            set => SetProperty(ref _vectorMode, value);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
