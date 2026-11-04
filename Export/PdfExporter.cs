using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Services;

namespace RevitFamilyImporter.Export
{
    public class PdfExporter
    {
        private readonly ExportSettingsService _settingsService = new ExportSettingsService();

        public string Export(ExportJob job, BatchExportRequest request)
        {
            Directory.CreateDirectory(job.OutputDirectory);

            PDFExportOptions options = _settingsService.CreatePdfOptions(
                job.Document,
                request.PdfSettings,
                request.UseDocumentPdfSettings,
                job.OutputFileName,
                request.PdfSettings.Combine && job.Sheets.Count > 1);

            IList<ElementId> viewIds = job.Sheets
                .Select(sheet => sheet.Id ?? new ElementId((int)sheet.IdValue))
                .ToList();

            bool exported = job.Document.Export(job.OutputDirectory, viewIds, options);
            if (!exported)
                throw new InvalidOperationException("A API do Revit retornou falha ao exportar PDF.");

            return Path.Combine(job.OutputDirectory, job.OutputFileName);
        }
    }
}
