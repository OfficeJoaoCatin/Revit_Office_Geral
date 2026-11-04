using System;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models
{
    public class ExportReport
    {
        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime FinishedAt { get; set; } = DateTime.Now;

        public int TotalJobs { get; set; }

        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        public string OutputFolder { get; set; } = string.Empty;

        public string LogPath { get; set; } = string.Empty;

        public TimeSpan Elapsed => FinishedAt - StartedAt;
    }
}
