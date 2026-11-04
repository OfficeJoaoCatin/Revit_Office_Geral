using System;
using System.Collections.Generic;
using RevitFamilyImporter.Profiles;

namespace RevitFamilyImporter.Models
{
    public class CheckpointData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public bool IsCompleted { get; set; }

        public string CurrentProjectPath { get; set; } = string.Empty;

        public string CurrentSheetNumber { get; set; } = string.Empty;

        public string CurrentJobKey { get; set; } = string.Empty;

        public BatchExportProfile Profile { get; set; } = new BatchExportProfile();

        public List<string> SelectedDocumentKeys { get; set; } = new List<string>();

        public Dictionary<string, List<long>> SelectedSheetIdsByDocument { get; set; } = new Dictionary<string, List<long>>();

        public HashSet<string> CompletedJobKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> FailedJobKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
