using System;
using System.Collections.Generic;
using System.IO;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Services
{
    public class NamingService
    {
        private const string DefaultTemplate = "{SheetNumber} - {SheetName}";
        private const string FlatSeparator = " - ";

        public string BuildRelativePath(
            ExportDocumentInfo document,
            SheetInfo sheet,
            ExportFormat format,
            string template)
        {
            string normalizedTemplate = EnsureSheetNameToken(template);
            string resolved = ResolveTemplate(document, sheet, format, normalizedTemplate);
            string extension = format == ExportFormat.Pdf ? ".pdf" : ".dwg";
            string withoutExtension = Path.ChangeExtension(resolved, null) ?? resolved;

            string[] parts = withoutExtension.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                parts = new[] { sheet.Number };

            List<string> cleanParts = new List<string>();
            foreach (string part in parts)
            {
                string clean = SanitizeSegment(part);
                if (!string.IsNullOrWhiteSpace(clean) && clean != "_")
                    cleanParts.Add(clean);
            }

            string fileName = cleanParts.Count == 0
                ? SanitizeSegment(sheet.Number)
                : string.Join(FlatSeparator, cleanParts);

            return fileName + extension;
        }

        public string BuildFullPath(
            string destinationFolder,
            ExportDocumentInfo document,
            SheetInfo sheet,
            ExportFormat format,
            string template)
        {
            return Path.Combine(destinationFolder, BuildRelativePath(document, sheet, format, template));
        }


        private static string EnsureSheetNameToken(string template)
        {
            string value = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
            bool hasSheetName = value.IndexOf("{SheetName}", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                value.IndexOf("{SheetTitle}", StringComparison.OrdinalIgnoreCase) >= 0;

            return hasSheetName
                ? value
                : value.TrimEnd('/', '\\', ' ', '-') + " - {SheetName}";
        }

        private static string ResolveTemplate(
            ExportDocumentInfo document,
            SheetInfo sheet,
            ExportFormat format,
            string template)
        {
            string value = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;

            Dictionary<string, string> tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{ProjectName}"] = document.ProjectName,
                ["{Project}"] = document.ProjectName,
                ["{FileName}"] = Path.GetFileNameWithoutExtension(document.FilePath),
                ["{SheetNumber}"] = sheet.Number,
                ["{SheetName}"] = sheet.Name,
                ["{SheetTitle}"] = sheet.Name,
                ["{Discipline}"] = sheet.Discipline,
                ["{Format}"] = format.ToString().ToUpperInvariant(),
                ["{Date}"] = DateTime.Now.ToString("yyyyMMdd")
            };

            foreach (KeyValuePair<string, string> token in tokens)
            {
                value = ReplaceIgnoreCase(value, token.Key, token.Value ?? string.Empty);
            }

            return value;
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            int index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                source = source.Remove(index, oldValue.Length).Insert(index, newValue);
                index = source.IndexOf(oldValue, index + newValue.Length, StringComparison.OrdinalIgnoreCase);
            }

            return source;
        }

        private static string SanitizeSegment(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "_" : value.Trim();
        }
    }
}


