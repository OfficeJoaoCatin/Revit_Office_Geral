using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using RevitFamilyImporter.Export;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Utils
{
    public class Logger
    {
        private readonly object _sync = new object();

        public Logger(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            LogPath = Path.Combine(outputFolder, "ExportLog.txt");
            WriteLine("Data;Tempo;Projeto;Folha;Formato;DuracaoMs;MemoriaMB;Status;Mensagem");
        }

        public string LogPath { get; }

        public void LogJob(ExportJob job, string status, string message)
        {
            string line = string.Join(";",
                DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                Escape(job.DocumentInfo.ProjectName),
                Escape(job.SheetNumbers),
                job.Format,
                job.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                GetCurrentMemoryMb().ToString(CultureInfo.InvariantCulture),
                Escape(status),
                Escape(message));

            WriteLine(line);
        }

        public void LogMessage(string message)
        {
            string line = string.Join(";",
                DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                "0",
                GetCurrentMemoryMb().ToString(CultureInfo.InvariantCulture),
                "Info",
                Escape(message));

            WriteLine(line);
        }

        public void LogException(ExportJob job, Exception exception)
        {
            LogJob(job, ExportJobStatus.Failed.ToString(), $"{exception.GetType().Name}: {exception.Message}");
        }

        private void WriteLine(string line)
        {
            lock (_sync)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }

        private static long GetCurrentMemoryMb()
        {
            using Process process = Process.GetCurrentProcess();
            return process.WorkingSet64 / 1024 / 1024;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace(";", ",").Replace(Environment.NewLine, " ");
        }
    }
}
