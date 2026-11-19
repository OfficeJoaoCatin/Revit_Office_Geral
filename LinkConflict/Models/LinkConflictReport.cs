using System;
using System.Collections.Generic;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public class LinkConflictReport
    {
        public string DiagnosticText { get; set; } = string.Empty;
        public bool HasUnverified { get; set; }
        public string UnverifiedSummary { get; set; } = string.Empty;
        public string LinkName { get; set; } = string.Empty;
        public int LinkInstanceId { get; set; }
        public string LinkedDocumentTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int LinkedElementCount { get; set; }
        public int CandidateHostElementCount { get; set; }
        public int UnboundedLinkedElementCount { get; set; }
        public long UnboundedHostPairCount { get; set; }
        public TimeSpan AnalysisDuration { get; set; }
        public TimeSpan GeometryDuration { get; set; }
        public TimeSpan SearchDuration { get; set; }
        public TimeSpan ComparisonDuration { get; set; }
        public int PrefilteredLinkedElementCount { get; set; }
        public int ExcludedCurveCount { get; set; }
        public bool CurveScanPartial { get; set; }
        public bool CurveScanFailed { get; set; }
        public TimeSpan CurvePreparationDuration { get; set; }
        public int EmptyRegionQueries { get; set; }
        public int EmptyRegionReuses { get; set; }
        public int EmptyRegionSkips { get; set; }
        public long ComparedPairCount { get; set; }
        public List<LinkConflictItem> Conflicts { get; } = new List<LinkConflictItem>();
    }
}
