using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Export
{
    public class BatchExportRequest
    {
        public string DestinationFolder { get; set; } = string.Empty;

        public bool ExportPdf { get; set; }

        public bool ExportDwg { get; set; }

        public bool UseDocumentPdfSettings { get; set; } = true;

        public bool ForceGlobalDwgSettings { get; set; } = true;

        public string NamingTemplate { get; set; } = "{SheetNumber} - {SheetName}";

        public PdfExportSettingsInfo PdfSettings { get; set; } = new PdfExportSettingsInfo();

        public DwgExportSettingsInfo DwgSettings { get; set; } = new DwgExportSettingsInfo();

        public List<ExportDocumentInfo> Documents { get; set; } = new List<ExportDocumentInfo>();

        public Dictionary<Document, List<SheetInfo>> SheetsByDocument { get; set; } = new Dictionary<Document, List<SheetInfo>>();

        public int TimeoutSeconds { get; set; } = 120;

        public long MemoryLimitMb { get; set; } = 8192;

        public CheckpointData? ResumeCheckpoint { get; set; }
    }
}

