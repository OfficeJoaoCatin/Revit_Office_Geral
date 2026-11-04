namespace RevitFamilyImporter.Models
{
    public enum ExportFormat
    {
        Pdf,
        Dwg
    }

    public enum ExportJobStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public enum SheetSelectionMode
    {
        AllSheets,
        IssuedOnly,
        Manual
    }
}
