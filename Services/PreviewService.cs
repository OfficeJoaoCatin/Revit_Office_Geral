using System.Collections.Generic;
using System.IO;
using System.Linq;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Services
{
    public class PreviewService
    {
        private readonly NamingService _namingService = new NamingService();

        public IReadOnlyList<string> BuildPreview(
            string destinationFolder,
            IEnumerable<ExportDocumentInfo> documents,
            IDictionary<ExportDocumentInfo, IEnumerable<SheetInfo>> sheetsByDocument,
            bool exportPdf,
            bool exportDwg,
            bool combinePdf,
            string namingTemplate,
            int maxItems = 200)
        {
            List<string> result = new List<string>();
            string rootName = string.IsNullOrWhiteSpace(destinationFolder)
                ? "Destino"
                : new DirectoryInfo(destinationFolder).Name;

            foreach (ExportDocumentInfo document in documents.Where(document => document.Selected))
            {
                if (!sheetsByDocument.TryGetValue(document, out IEnumerable<SheetInfo>? sheets))
                    continue;

                List<SheetInfo> selectedSheets = sheets.Where(sheet => sheet.Selected).ToList();

                if (exportPdf && combinePdf && selectedSheets.Count > 0)
                {
                    result.Add(Path.Combine(rootName, Sanitize(document.ProjectName) + ".pdf"));
                    if (result.Count >= maxItems)
                        return result;
                }

                foreach (SheetInfo sheet in selectedSheets)
                {
                    AddPreview(result, rootName, document, sheet, ExportFormat.Pdf, exportPdf && !combinePdf, namingTemplate);
                    AddPreview(result, rootName, document, sheet, ExportFormat.Dwg, exportDwg, namingTemplate);

                    if (result.Count >= maxItems)
                        return result;
                }
            }

            return result;
        }

        private void AddPreview(
            List<string> result,
            string rootName,
            ExportDocumentInfo document,
            SheetInfo sheet,
            ExportFormat format,
            bool enabled,
            string namingTemplate)
        {
            if (!enabled)
                return;

            string fileName = _namingService.BuildRelativePath(document, sheet, format, namingTemplate);
            result.Add(Path.Combine(rootName, fileName));
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "Projeto" : value.Trim();
        }
    }
}
