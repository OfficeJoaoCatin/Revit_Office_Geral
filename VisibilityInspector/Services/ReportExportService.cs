using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using RevitFamilyImporter.VisibilityInspector.Models;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class ReportExportService
    {
        public void Export(VisibilityReport report, string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string content;
            switch (extension)
            {
                case ".csv":
                    content = BuildCsv(report);
                    break;
                case ".json":
                    content = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                    break;
                case ".html":
                    content = BuildHtml(report);
                    break;
                case ".pdf":
                    content = BuildPrintableHtml(report);
                    break;
                default:
                    content = BuildText(report);
                    break;
            }

            File.WriteAllText(path, content, Encoding.UTF8);
        }

        public string BuildText(VisibilityReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Visibility Inspector");
            builder.AppendLine($"Elemento: {report.ElementId} - {report.ElementName}");
            builder.AppendLine($"Origem: {report.ElementSource}");
            if (report.IsLinkedElement)
            {
                builder.AppendLine($"Documento host: {report.HostDocumentTitle}");
                builder.AppendLine($"Documento vinculado: {report.LinkedDocumentTitle}");
                builder.AppendLine($"Instância do link: {report.LinkInstanceName} ({report.LinkInstanceId})");
            }
            builder.AppendLine($"Categoria: {report.Category}");
            builder.AppendLine($"Familia/Tipo: {report.Family} / {report.Type}");
            builder.AppendLine($"Vista: {report.ViewName}");
            builder.AppendLine($"Status: {report.OverallStatus}");
            builder.AppendLine(report.Summary);
            builder.AppendLine();
            builder.AppendLine("Diagnostico");
            foreach (VisibilityIssue issue in report.Issues.OrderBy(i => i.Priority))
            {
                builder.AppendLine($"{issue.Priority}. {issue.Title}: {issue.StatusText} - {issue.Summary}");
                foreach (string detail in issue.Details)
                    builder.AppendLine($"   - {detail}");
            }

            return builder.ToString();
        }

        private string BuildCsv(VisibilityReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Tipo;Nome;Status;Resumo;Detalhes");
            foreach (VisibilityIssue issue in report.Issues.OrderBy(i => i.Priority))
                builder.AppendLine($"Diagnostico;{Escape(issue.Title)};{Escape(issue.StatusText)};{Escape(issue.Summary)};{Escape(string.Join(" | ", issue.Details))}");
            foreach (FilterResult filter in report.Filters)
                builder.AppendLine($"Filtro;{Escape(filter.Name)};{Escape(filter.PassesText)};{Escape(filter.Conclusion)};{Escape($"Oculta={filter.HiddenText}; Cor={filter.Color}; Peso={filter.Weight}; Transparencia={filter.Transparency}")}");
            return builder.ToString();
        }

        private string BuildHtml(VisibilityReport report)
        {
            return BuildPrintableHtml(report);
        }

        private string BuildPrintableHtml(VisibilityReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Visibility Inspector</title>");
            builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:28px;color:#172033}h1{font-size:22px}table{border-collapse:collapse;width:100%;margin:12px 0}td,th{border:1px solid #d7dde8;padding:6px;text-align:left}th{background:#eef3f8}.ok{color:#166534}.blocked{color:#b91c1c}.warn{color:#a16207}</style>");
            builder.AppendLine("</head><body>");
            builder.AppendLine("<h1>Visibility Inspector</h1>");
            builder.AppendLine($"<p><b>Elemento:</b> {Html(report.ElementId)} - {Html(report.ElementName)}<br>");
            builder.AppendLine($"<b>Origem:</b> {Html(report.ElementSource)}<br>");
            if (report.IsLinkedElement)
            {
                builder.AppendLine($"<b>Documento host:</b> {Html(report.HostDocumentTitle)}<br>");
                builder.AppendLine($"<b>Documento vinculado:</b> {Html(report.LinkedDocumentTitle)}<br>");
                builder.AppendLine($"<b>Instância do link:</b> {Html(report.LinkInstanceName)} ({Html(report.LinkInstanceId)})<br>");
            }
            builder.AppendLine($"<b>Categoria:</b> {Html(report.Category)}<br><b>Vista:</b> {Html(report.ViewName)}<br><b>Status:</b> {Html(report.OverallStatus)}</p>");
            builder.AppendLine($"<p>{Html(report.Summary)}</p>");
            builder.AppendLine("<table><tr><th>Prioridade</th><th>Analise</th><th>Status</th><th>Resumo</th><th>Detalhes</th></tr>");
            foreach (VisibilityIssue issue in report.Issues.OrderBy(i => i.Priority))
                builder.AppendLine($"<tr><td>{issue.Priority}</td><td>{Html(issue.Title)}</td><td>{Html(issue.StatusText)}</td><td>{Html(issue.Summary)}</td><td>{Html(string.Join("<br>", issue.Details))}</td></tr>");
            builder.AppendLine("</table></body></html>");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            return value.Contains(";") || value.Contains("\"") || value.Contains("\n")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        private static string Html(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
