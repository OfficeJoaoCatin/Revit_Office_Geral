using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Export
{
    public class ExportCoordinator
    {
        private readonly NamingService _namingService = new NamingService();
        private readonly CheckpointManager _checkpointManager = new CheckpointManager();

        public ExportReport Run(
            BatchExportRequest request,
            Action<ExportProgressInfo>? progress,
            Func<bool>? cancellationRequested)
        {
            Directory.CreateDirectory(request.DestinationFolder);

            Logger logger = new Logger(request.DestinationFolder);
            Heartbeat heartbeat = new Heartbeat();
            Watchdog watchdog = new Watchdog();
            TimeoutController timeoutController = new TimeoutController(request.TimeoutSeconds);
            MemoryMonitor memoryMonitor = new MemoryMonitor(request.MemoryLimitMb);
            ExportWorker worker = new ExportWorker(timeoutController, logger, heartbeat);

            List<ExportJob> jobs = BuildJobs(request);
            ExportQueue queue = new ExportQueue(jobs);
            CheckpointData checkpoint = _checkpointManager.Start(request);

            ExportReport report = new ExportReport
            {
                StartedAt = DateTime.Now,
                OutputFolder = request.DestinationFolder,
                LogPath = logger.LogPath,
                TotalJobs = jobs.Count
            };

            int completedCount = 0;
            progress?.Invoke(new ExportProgressInfo
            {
                Completed = 0,
                Total = jobs.Count,
                Message = "Fila criada"
            });

            while (queue.TryDequeue(out ExportJob? job) && job != null)
            {
                if (cancellationRequested?.Invoke() == true)
                {
                    job.Status = ExportJobStatus.Cancelled;
                    report.Cancelled++;
                    break;
                }

                memoryMonitor.CleanupBetweenJobs(logger);

                if (watchdog.IsStale(heartbeat))
                    logger.LogMessage(watchdog.BuildWarning(heartbeat));

                progress?.Invoke(new ExportProgressInfo
                {
                    Completed = completedCount,
                    Total = jobs.Count,
                    CurrentJob = job,
                    Message = $"Exportando {job.Format}: {job.DocumentInfo.ProjectName} / {job.SheetNumbers}"
                });

                _checkpointManager.UpdateCurrent(checkpoint, job);
                string filePath = worker.Execute(job, request);

                completedCount++;

                if (job.Status == ExportJobStatus.Completed)
                {
                    report.Succeeded++;
                    if (!string.IsNullOrWhiteSpace(filePath))
                        report.Files.Add(filePath);

                    _checkpointManager.MarkCompleted(checkpoint, job);
                }
                else if (job.Status == ExportJobStatus.Failed)
                {
                    report.Failed++;
                    _checkpointManager.MarkFailed(checkpoint, job);
                }

                progress?.Invoke(new ExportProgressInfo
                {
                    Completed = completedCount,
                    Total = jobs.Count,
                    CurrentJob = job,
                    Message = job.Status == ExportJobStatus.Completed
                        ? "Item concluido"
                        : $"Item falhou: {job.ErrorMessage}"
                });

                if (cancellationRequested?.Invoke() == true)
                    break;
            }

            int remaining = jobs.Count(job => job.Status == ExportJobStatus.Pending);
            if (cancellationRequested?.Invoke() == true && remaining > 0)
            {
                foreach (ExportJob pending in jobs.Where(job => job.Status == ExportJobStatus.Pending))
                {
                    pending.Status = ExportJobStatus.Cancelled;
                }

                report.Cancelled += remaining;
                logger.LogMessage($"Exportacao cancelada com seguranca. Itens pendentes: {remaining}.");
            }
            else
            {
                _checkpointManager.MarkBatchCompleted(checkpoint);
                logger.LogMessage("Exportacao em lote finalizada.");
            }

            report.FinishedAt = DateTime.Now;
            return report;
        }

        private List<ExportJob> BuildJobs(BatchExportRequest request)
        {
            List<ExportJob> jobs = new List<ExportJob>();
            HashSet<string> completedKeys = request.ResumeCheckpoint?.CompletedJobKeys
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ExportDocumentInfo document in request.Documents.Where(document => document.Selected && document.Document != null))
            {
                if (!request.SheetsByDocument.TryGetValue(document.Document!, out List<SheetInfo>? allSheets))
                    continue;

                List<SheetInfo> selectedSheets = allSheets
                    .Where(sheet => sheet.Selected && sheet.CanBePrinted)
                    .ToList();

                if (selectedSheets.Count == 0)
                    continue;

                if (request.ExportPdf)
                {
                    IEnumerable<ExportJob> pdfJobs = request.PdfSettings.Combine
                        ? CreateCombinedPdfJob(request, document, selectedSheets)
                        : selectedSheets.Select(sheet => CreateSingleSheetJob(request, document, sheet, ExportFormat.Pdf));

                    jobs.AddRange(pdfJobs);
                }

                if (request.ExportDwg)
                {
                    jobs.AddRange(selectedSheets.Select(sheet => CreateSingleSheetJob(request, document, sheet, ExportFormat.Dwg)));
                }
            }

            return jobs
                .Where(job => !completedKeys.Contains(job.Key))
                .ToList();
        }

        private IEnumerable<ExportJob> CreateCombinedPdfJob(
            BatchExportRequest request,
            ExportDocumentInfo document,
            List<SheetInfo> sheets)
        {
            string outputDirectory = request.DestinationFolder;

            yield return new ExportJob
            {
                Document = document.Document!,
                DocumentInfo = document,
                Sheets = sheets,
                Format = ExportFormat.Pdf,
                OutputDirectory = outputDirectory,
                OutputFileName = Sanitize(document.ProjectName) + ".pdf"
            };
        }

        private ExportJob CreateSingleSheetJob(
            BatchExportRequest request,
            ExportDocumentInfo document,
            SheetInfo sheet,
            ExportFormat format)
        {
            string fullPath = _namingService.BuildFullPath(
                request.DestinationFolder,
                document,
                sheet,
                format,
                request.NamingTemplate);

            return new ExportJob
            {
                Document = document.Document!,
                DocumentInfo = document,
                Sheets = new List<SheetInfo> { sheet },
                Format = format,
                OutputDirectory = Path.GetDirectoryName(fullPath) ?? request.DestinationFolder,
                OutputFileName = Path.GetFileName(fullPath)
            };
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "Projeto" : value.Trim();
        }
    }
}




