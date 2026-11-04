using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.Commands
{
    internal class DocumentOperationReport
    {
        private readonly List<string> _details = new List<string>();

        public int Succeeded { get; private set; }

        public int Skipped { get; private set; }

        public int Failed { get; private set; }

        public void AddSuccess(Document document, string action)
        {
            Succeeded++;
            _details.Add($"OK - {DocumentName(document)}: {action}");
        }

        public void AddSkipped(Document document, string reason)
        {
            Skipped++;
            _details.Add($"Ignorado - {DocumentName(document)}: {reason}");
        }

        public void AddFailure(Document document, string reason)
        {
            Failed++;
            _details.Add($"Falha - {DocumentName(document)}: {reason}");
        }

        public string BuildDialogText(string successLabel)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{successLabel}: {Succeeded}");
            builder.AppendLine($"Ignorados: {Skipped}");
            builder.AppendLine($"Falhas: {Failed}");

            if (_details.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Detalhes:");
                foreach (string detail in _details.Take(12))
                    builder.AppendLine(detail);

                if (_details.Count > 12)
                    builder.AppendLine($"...mais {_details.Count - 12} item(ns).");
            }

            return builder.ToString();
        }

        public static string DocumentName(Document document)
        {
            if (!string.IsNullOrWhiteSpace(document.Title))
                return document.Title.Trim();

            if (!string.IsNullOrWhiteSpace(document.PathName))
                return Path.GetFileNameWithoutExtension(document.PathName);

            return "Documento sem nome";
        }
    }
}
