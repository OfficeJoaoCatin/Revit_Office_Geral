using System.Collections.Generic;

namespace RevitFamilyImporter.Models
{
    public class SheetTitleBlockUpdateReport
    {
        public int RowsRead { get; set; }
        public int RowsEligible { get; set; }
        public int RowsSelected { get; set; }
        public int SheetsCreated { get; set; }
        public int SheetsUpdated { get; set; }
        public int SheetsMissingSkipped { get; set; }
        public int SheetsRenamed { get; set; }
        public int ParametersUpdated { get; set; }
        public int ParametersSkipped { get; set; }
        public int Errors { get; set; }

        public List<string> Messages { get; } = new List<string>();
    }
}
