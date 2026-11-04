using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Export
{
    public class ExportJob
    {
        public Document Document { get; set; } = null!;

        public ExportDocumentInfo DocumentInfo { get; set; } = null!;

        public List<SheetInfo> Sheets { get; set; } = new List<SheetInfo>();

        public ExportFormat Format { get; set; }

        public ExportJobStatus Status { get; set; } = ExportJobStatus.Pending;

        public string OutputDirectory { get; set; } = string.Empty;

        public string OutputFileName { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => StartedAt.HasValue && CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : TimeSpan.Zero;

        public string SheetNumbers => string.Join(", ", Sheets.Select(sheet => sheet.Number));

        public string Key
        {
            get
            {
                string sheetKey = string.Join("+", Sheets.Select(sheet => sheet.IdValue));
                return $"{DocumentInfo.StableKey}|{Format}|{sheetKey}|{OutputFileName}";
            }
        }
    }
}
