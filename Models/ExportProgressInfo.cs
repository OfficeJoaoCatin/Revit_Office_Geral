using RevitFamilyImporter.Export;

namespace RevitFamilyImporter.Models
{
    public class ExportProgressInfo
    {
        public int Completed { get; set; }

        public int Total { get; set; }

        public double Percent => Total == 0 ? 0 : (Completed * 100.0) / Total;

        public ExportJob? CurrentJob { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
