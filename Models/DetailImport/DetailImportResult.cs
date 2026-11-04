using System.Collections.Generic;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class DetailImportResult
    {
        public List<ImportedDetail> Imported { get; } = new List<ImportedDetail>();

        public List<string> Failures { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public int TotalRequested { get; set; }

        public long ReadMilliseconds { get; set; }

        public long DetectionMilliseconds { get; set; }

        public long PreviewMilliseconds { get; set; }

        public long ImportMilliseconds { get; set; }

        public long TotalMilliseconds { get; set; }
    }
}

