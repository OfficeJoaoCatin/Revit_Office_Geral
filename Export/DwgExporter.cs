using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Services;

namespace RevitFamilyImporter.Export
{
    public class DwgExporter
    {
        private readonly ExportSettingsService _settingsService = new ExportSettingsService();

        public string Export(ExportJob job, BatchExportRequest request)
        {
            Directory.CreateDirectory(job.OutputDirectory);

            DWGExportOptions options = _settingsService.CreateDwgOptions(
                job.Document,
                request.DwgSettings,
                request.ForceGlobalDwgSettings);

            ICollection<ElementId> viewIds = job.Sheets
                .Select(sheet => sheet.Id ?? new ElementId((int)sheet.IdValue))
                .ToList();

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(job.OutputFileName);
            bool exported = job.Document.Export(job.OutputDirectory, fileNameWithoutExtension, viewIds, options);
            if (!exported)
                throw new InvalidOperationException("A API do Revit retornou falha ao exportar DWG.");

            return Path.Combine(job.OutputDirectory, job.OutputFileName);
        }
    }
}
