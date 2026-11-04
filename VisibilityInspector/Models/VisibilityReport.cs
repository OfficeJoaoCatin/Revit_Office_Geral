using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityReport
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementUniqueId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string ElementSource { get; set; } = "Modelo ativo";
        public string Category { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsLinkedElement { get; set; }
        public string HostDocumentTitle { get; set; } = string.Empty;
        public string LinkedDocumentTitle { get; set; } = string.Empty;
        public string LinkInstanceName { get; set; } = string.Empty;
        public string LinkInstanceId { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public string OverallStatus { get; set; } = "VISÍVEL";
        public string Summary { get; set; } = string.Empty;
        public List<VisibilityIssue> Issues { get; } = new List<VisibilityIssue>();
        public List<FilterResult> Filters { get; } = new List<FilterResult>();
        public List<OverrideRecord> Overrides { get; } = new List<OverrideRecord>();
        public List<ParameterSnapshot> Parameters { get; } = new List<ParameterSnapshot>();
        public List<VisibilityIssue> Flow { get; } = new List<VisibilityIssue>();
        public List<ViewVisibilitySummary> ViewSearchResults { get; } = new List<ViewVisibilitySummary>();
        public VisibilityComparison? Comparison { get; set; }

        public IEnumerable<VisibilityIssue> Problems =>
            Issues.Where(issue => issue.Severity == IssueSeverity.Blocked || issue.Severity == IssueSeverity.Warning)
                .OrderBy(issue => issue.Priority);
    }
}
