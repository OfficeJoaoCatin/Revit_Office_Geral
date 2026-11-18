using Autodesk.Revit.DB;
using System.ComponentModel;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public class LinkConflictItem : INotifyPropertyChanged
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
            }
        }

        public string CompletionKey { get; set; } = string.Empty;
        public int Number { get; set; }
        public int LinkedElementId { get; set; }
        public string LinkedElementName { get; set; } = string.Empty;
        public string LinkedFamily { get; set; } = string.Empty;
        public string LinkedType { get; set; } = string.Empty;
        public string LinkedCategory { get; set; } = string.Empty;
        public int HostElementId { get; set; }
        public string HostElementName { get; set; } = string.Empty;
        public string HostFamily { get; set; } = string.Empty;
        public string HostType { get; set; } = string.Empty;
        public string HostCategory { get; set; } = string.Empty;
        public double IntersectionVolumeCubicMeters { get; set; }
        public XYZ MarkerPoint { get; set; }
        public bool IsApproximate { get; set; }

        public string Summary =>
            $"Link #{LinkedElementId} - {LinkedElementName} x Host #{HostElementId} - {HostElementName}";

        public string IntersectionText =>
            IntersectionVolumeCubicMeters > 0
                ? $"{IntersectionVolumeCubicMeters:0.000} m³"
                : "Interseção por caixa (aproximada)";
    }
}
