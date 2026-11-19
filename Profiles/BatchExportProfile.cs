using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Profiles
{
    public class BatchExportProfile
    {
        public string Name { get; set; } = "Novo perfil";

        public string DestinationFolder { get; set; } = string.Empty;

        public bool ExportPdf { get; set; } = true;

        public bool ExportDwg { get; set; }

        public bool UseDocumentPdfSettings { get; set; } = true;

        public bool ForceGlobalDwgSettings { get; set; } = true;

        public bool CopyDwgSetupToProjects { get; set; }
        public string DwgSetupSourceKey { get; set; } = string.Empty;

        public string PdfSetupName { get; set; } = string.Empty;

        public string DwgSetupName { get; set; } = string.Empty;

        public bool PdfCombine { get; set; }

        public int PdfDpi { get; set; } = 300;

        public bool PdfCenter { get; set; } = true;

        public bool PdfVectorMode { get; set; } = true;

        public string DwgVersion { get; set; } = "Default";

        public bool DwgExportLinks { get; set; }

        public bool DwgMergeViews { get; set; } = true;

        public SheetSelectionMode SheetSelectionMode { get; set; } = SheetSelectionMode.AllSheets;

        public string NamingTemplate { get; set; } = "{SheetNumber} - {SheetName}";

        public int TimeoutSeconds { get; set; } = 120;

        public long MemoryLimitMb { get; set; } = 8192;
    }
}

