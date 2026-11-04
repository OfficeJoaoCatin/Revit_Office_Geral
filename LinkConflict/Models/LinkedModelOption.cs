using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public sealed class LinkedModelOption
    {
        public RevitLinkInstance LinkInstance { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public IReadOnlyList<LinkedCategoryOption> Categories { get; set; } = new List<LinkedCategoryOption>();

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DocumentTitle) || string.Equals(Name, DocumentTitle, System.StringComparison.OrdinalIgnoreCase))
                    return Name;

                return $"{Name} - {DocumentTitle}";
            }
        }
    }
}
