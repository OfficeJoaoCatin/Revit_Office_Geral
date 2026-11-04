using System.Collections.Generic;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityIssue
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public IssueSeverity Severity { get; set; } = IssueSeverity.Info;
        public int Priority { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Details { get; } = new List<string>();

        public string StatusText
        {
            get
            {
                switch (Severity)
                {
                    case IssueSeverity.Ok:
                        return "OK";
                    case IssueSeverity.Warning:
                        return "Parcial";
                    case IssueSeverity.Blocked:
                        return "Oculta";
                    default:
                        return "Info";
                }
            }
        }

        public string Icon
        {
            get
            {
                switch (Severity)
                {
                    case IssueSeverity.Ok:
                        return "OK";
                    case IssueSeverity.Warning:
                        return "!";
                    case IssueSeverity.Blocked:
                        return "X";
                    default:
                        return "i";
                }
            }
        }

        public string ProblemForeground
        {
            get
            {
                switch (Severity)
                {
                    case IssueSeverity.Blocked:
                        return "#991B1B";
                    case IssueSeverity.Warning:
                        return "#A16207";
                    default:
                        return "#475569";
                }
            }
        }
    }
}
