using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RevitFamilyImporter.Services.DetailImport
{
    public class TraceLogger : ILogger
    {
        private static readonly object FileLock = new object();
        private readonly string _scope;

        public TraceLogger(string scope)
        {
            _scope = string.IsNullOrWhiteSpace(scope) ? "RevitFamilyImporter" : scope;
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Warn(string message)
        {
            Write("WARN", message);
        }

        public void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception = null)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{_scope}] {level} {message}";
            Trace.WriteLine(line);
            if (exception != null)
                Trace.WriteLine(exception);

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RevitFamilyImporter",
                    "Logs");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"{_scope}_{DateTime.Now:yyyyMMdd}.log");
                string content = exception == null
                    ? line + Environment.NewLine
                    : line + Environment.NewLine + exception + Environment.NewLine;

                lock (FileLock)
                    File.AppendAllText(path, content, Encoding.UTF8);
            }
            catch
            {
                // Registro em arquivo não deve interromper a operação do Revit.
            }
        }
    }
}


