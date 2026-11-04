using System;
using System.Diagnostics;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Export
{
    public class ExportWorker
    {
        private readonly PdfExporter _pdfExporter = new PdfExporter();
        private readonly DwgExporter _dwgExporter = new DwgExporter();
        private readonly TimeoutController _timeoutController;
        private readonly Logger _logger;
        private readonly Heartbeat _heartbeat;

        public ExportWorker(TimeoutController timeoutController, Logger logger, Heartbeat heartbeat)
        {
            _timeoutController = timeoutController;
            _logger = logger;
            _heartbeat = heartbeat;
        }

        public string Execute(ExportJob job, BatchExportRequest request)
        {
            job.Status = ExportJobStatus.Running;
            job.StartedAt = DateTime.Now;
            _heartbeat.Beat();

            Stopwatch stopwatch = _timeoutController.Start();

            try
            {
                string filePath = job.Format == ExportFormat.Pdf
                    ? _pdfExporter.Export(job, request)
                    : _dwgExporter.Export(job, request);

                stopwatch.Stop();
                job.CompletedAt = DateTime.Now;

                if (_timeoutController.HasTimedOut(stopwatch))
                    throw new TimeoutException($"Tempo limite excedido ({_timeoutController.MaxDuration.TotalSeconds:F0}s).");

                job.Status = ExportJobStatus.Completed;
                _logger.LogJob(job, job.Status.ToString(), filePath);
                return filePath;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                job.CompletedAt = DateTime.Now;
                job.Status = ExportJobStatus.Failed;
                job.ErrorMessage = exception.Message;
                _logger.LogException(job, exception);
                return string.Empty;
            }
            finally
            {
                _heartbeat.Beat();
            }
        }
    }
}
