using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RevitFamilyImporter.Export;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Profiles;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Services
{
    public class CheckpointManager
    {
        public CheckpointManager()
        {
            CheckpointFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM Tools",
                "RevitFamilyImporter",
                "BatchExportCheckpoints");

            Directory.CreateDirectory(CheckpointFolder);
        }

        public string CheckpointFolder { get; }

        public CheckpointData? FindLatestIncomplete()
        {
            Directory.CreateDirectory(CheckpointFolder);

            return Directory.GetFiles(CheckpointFolder, "*.json")
                .Select(LoadFromFile)
                .Where(checkpoint => checkpoint != null && !checkpoint.IsCompleted)
                .Cast<CheckpointData>()
                .OrderByDescending(checkpoint => checkpoint.UpdatedAt)
                .FirstOrDefault();
        }

        public CheckpointData Start(BatchExportRequest request)
        {
            CheckpointData checkpoint = new CheckpointData
            {
                Profile = CreateProfileSnapshot(request),
                SelectedDocumentKeys = request.Documents.Select(document => document.StableKey).ToList(),
                SelectedSheetIdsByDocument = request.SheetsByDocument.ToDictionary(
                    pair => request.Documents.First(document => document.Document == pair.Key).StableKey,
                    pair => pair.Value.Select(sheet => sheet.IdValue).ToList())
            };

            if (request.ResumeCheckpoint != null)
            {
                checkpoint.Id = request.ResumeCheckpoint.Id;
                checkpoint.StartedAt = request.ResumeCheckpoint.StartedAt;
                checkpoint.CompletedJobKeys = new HashSet<string>(request.ResumeCheckpoint.CompletedJobKeys, StringComparer.OrdinalIgnoreCase);
                checkpoint.FailedJobKeys = new HashSet<string>(request.ResumeCheckpoint.FailedJobKeys, StringComparer.OrdinalIgnoreCase);
            }

            Save(checkpoint);
            return checkpoint;
        }

        public void UpdateCurrent(CheckpointData checkpoint, ExportJob job)
        {
            checkpoint.CurrentProjectPath = job.DocumentInfo.FilePath;
            checkpoint.CurrentSheetNumber = job.SheetNumbers;
            checkpoint.CurrentJobKey = job.Key;
            checkpoint.UpdatedAt = DateTime.Now;
            Save(checkpoint);
        }

        public void MarkCompleted(CheckpointData checkpoint, ExportJob job)
        {
            checkpoint.CompletedJobKeys.Add(job.Key);
            checkpoint.CurrentJobKey = string.Empty;
            checkpoint.UpdatedAt = DateTime.Now;
            Save(checkpoint);
        }

        public void MarkFailed(CheckpointData checkpoint, ExportJob job)
        {
            checkpoint.FailedJobKeys.Add(job.Key);
            checkpoint.CurrentJobKey = string.Empty;
            checkpoint.UpdatedAt = DateTime.Now;
            Save(checkpoint);
        }

        public void MarkBatchCompleted(CheckpointData checkpoint)
        {
            checkpoint.IsCompleted = true;
            checkpoint.CurrentJobKey = string.Empty;
            checkpoint.UpdatedAt = DateTime.Now;
            Save(checkpoint);
        }

        public void Save(CheckpointData checkpoint)
        {
            Directory.CreateDirectory(CheckpointFolder);
            string path = GetPath(checkpoint.Id);
            JsonStorage.Save(path, checkpoint);
        }

        private CheckpointData? LoadFromFile(string path)
        {
            try
            {
                CheckpointData? checkpoint = JsonStorage.Load<CheckpointData>(path);
                if (checkpoint != null)
                {
                    checkpoint.CompletedJobKeys = new HashSet<string>(checkpoint.CompletedJobKeys, StringComparer.OrdinalIgnoreCase);
                    checkpoint.FailedJobKeys = new HashSet<string>(checkpoint.FailedJobKeys, StringComparer.OrdinalIgnoreCase);
                }

                return checkpoint;
            }
            catch
            {
                return null;
            }
        }

        private string GetPath(string id)
        {
            return Path.Combine(CheckpointFolder, id + ".json");
        }

        private static BatchExportProfile CreateProfileSnapshot(BatchExportRequest request)
        {
            return new BatchExportProfile
            {
                Name = "Checkpoint",
                DestinationFolder = request.DestinationFolder,
                ExportPdf = request.ExportPdf,
                ExportDwg = request.ExportDwg,
                UseDocumentPdfSettings = request.UseDocumentPdfSettings,
                ForceGlobalDwgSettings = request.ForceGlobalDwgSettings,
                PdfSetupName = request.PdfSettings.Name,
                DwgSetupName = request.DwgSettings.Name,
                PdfCombine = request.PdfSettings.Combine,
                PdfDpi = request.PdfSettings.DPI,
                PdfCenter = request.PdfSettings.Center,
                PdfVectorMode = request.PdfSettings.VectorMode,
                DwgVersion = request.DwgSettings.Version,
                DwgExportLinks = request.DwgSettings.ExportLinks,
                DwgMergeViews = request.DwgSettings.MergeViews,
                NamingTemplate = request.NamingTemplate,
                TimeoutSeconds = request.TimeoutSeconds,
                MemoryLimitMb = request.MemoryLimitMb
            };
        }
    }
}
