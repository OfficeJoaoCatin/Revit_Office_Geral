using Autodesk.Revit.DB;
using System.ComponentModel;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgClashResult : INotifyPropertyChanged
    {
        private bool _isCompleted;
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted == value) return;
                _isCompleted = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
        public string CompletionKey => string.IsNullOrWhiteSpace(Element?.UniqueId) || string.IsNullOrWhiteSpace(Region?.CompletionGeometryKey)
            ? string.Empty
            : Element.UniqueId + "|" + Region.CompletionGeometryKey;

        public int Number { get; set; }
        public RevitElementClashInfo Element { get; set; }
        public DwgRegion Region { get; set; }
        public double DistanceFeet { get; set; }
        public string Status => IsCompleted ? "CONCLUÍDO" : "CLASH";
        public int ElementId => Element?.ElementId?.IntegerValue ?? -1;
        public string Category => Element?.Category ?? string.Empty;
        public string Family => Element?.Family ?? string.Empty;
        public string Type => Element?.Type ?? string.Empty;
        public string Layer => Region?.Layer ?? string.Empty;
        public string RegionName => Region?.Name ?? string.Empty;
        public string DistanceText => DistanceFeet > 0 ? $"{DistanceFeet * 304.8:0.0} mm" : "Interseção";
    }
}
