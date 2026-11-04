using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Models
{
    public class DwgExportSettingsInfo : ObservableObject
    {
        private string _version = "Default";
        private bool _exportLinks;
        private bool _mergeViews = true;

        public string Name { get; set; } = "Padrao";

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public bool ExportLinks
        {
            get => _exportLinks;
            set => SetProperty(ref _exportLinks, value);
        }

        public bool MergeViews
        {
            get => _mergeViews;
            set => SetProperty(ref _mergeViews, value);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
