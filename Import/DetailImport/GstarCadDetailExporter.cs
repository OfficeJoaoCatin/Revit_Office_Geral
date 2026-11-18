using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitFamilyImporter.Import.DetailImport
{
    internal sealed class GstarCadDetailExporter : IDisposable
    {
        private const int DefaultTimeoutMilliseconds = 120000;
        private readonly int _timeoutMilliseconds;
        private readonly ILogger _logger;
        private Process _worker;
        private bool _stopped;
        private sealed class WorkerExportException : InvalidOperationException
        {
            public WorkerExportException(string message) : base(message) { }
        }
        public GstarCadDetailExporter(ILogger logger, int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            _logger = logger ?? new Logger("DetailImport");
            _timeoutMilliseconds = timeoutMilliseconds > 0 ? timeoutMilliseconds : throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        }

        public static bool TryGetRunningSessions(out string sessions)
        {
            var found = new List<string>();
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        string name = process.ProcessName ?? string.Empty;
                        if (name.IndexOf("gcad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("gstar", StringComparison.OrdinalIgnoreCase) >= 0)
                            found.Add($"{name} (PID {process.Id})");
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
            sessions = string.Join(", ", found.Distinct(StringComparer.OrdinalIgnoreCase));
            return found.Count > 0;
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        public bool TryExport(DwgDocumentModel source, DetailRegion region, string outputFolder,
            string boundaryLayer, string nameLayer, int maxSelectedEntities, CancellationToken cancellationToken, out string filePath)
        {
            filePath = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (_stopped) throw new InvalidOperationException("O processo auxiliar foi interrompido; inicie uma nova importação.");
            if (_worker == null)
            {
                string exe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "OfficeGstarCadWorker.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("O auxiliar OfficeGstarCadWorker.exe não foi instalado junto ao plugin.", exe);
                var info = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                _worker = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível iniciar o auxiliar do GstarCAD.");
                _logger.Info($"Auxiliar GstarCAD iniciado: PID {_worker.Id}.");
            }
            var b = region.Bounds;
            string request = string.Join("|", "EXPORT", Encode(source.FilePath), region.Id.ToString(CultureInfo.InvariantCulture),
                Encode(region.Name), Num(b.MinX), Num(b.MinY), Num(b.MaxX), Num(b.MaxY), Encode(outputFolder),
                maxSelectedEntities.ToString(CultureInfo.InvariantCulture), "1");
            try
            {
                _logger.Info($"Auxiliar: solicitando detalhe {region.Name}; limite {_timeoutMilliseconds / 1000} s.");
                _worker.StandardInput.WriteLine(request);
                _worker.StandardInput.Flush();
                Task<string> response = _worker.StandardOutput.ReadLineAsync();
                Task timer = Task.Delay(_timeoutMilliseconds, cancellationToken);
                Task winner = Task.WhenAny(response, timer).GetAwaiter().GetResult();
                if (winner != response)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"O GstarCAD excedeu {_timeoutMilliseconds / 1000.0:0.#} segundos para gerar o detalhe. O auxiliar foi interrompido; confira se uma janela do GstarCAD permaneceu aberta.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                string result = response.GetAwaiter().GetResult();
                if (result == null) throw new InvalidOperationException("O auxiliar do GstarCAD encerrou sem responder.");
                string[] parts = result.Split(new[] { '|' }, 2);
                if (parts.Length != 2) throw new InvalidOperationException("Resposta inválida do auxiliar do GstarCAD.");
                string payload = Decode(parts[1]);
                if (parts[0] == "ERR") throw new WorkerExportException(payload);
                if (parts[0] != "OK") throw new InvalidOperationException("Resposta desconhecida do auxiliar do GstarCAD.");
                if (!File.Exists(payload)) throw new FileNotFoundException("O auxiliar não gerou o DWG solicitado.", payload);
                filePath = payload;
                return true;
            }
            catch (WorkerExportException) { throw; }
            catch
            {
                StopWorker(false);
                throw;
            }
        }

        private void StopWorker(bool graceful)
        {
            _stopped = true;
            Process process = _worker;
            _worker = null;
            if (process == null) return;
            try
            {
                if (!process.HasExited && graceful)
                {
                    try { process.StandardInput.WriteLine("QUIT"); process.StandardInput.Flush(); } catch { }
                    process.WaitForExit(2000);
                }
                if (!process.HasExited)
                {
                    _logger.Warn("Interrompendo apenas o processo auxiliar; o GstarCAD pode permanecer aberto.");
                    process.Kill();
                    process.WaitForExit(2000);
                }
            }
            catch (Exception ex) { _logger.Warn("Não foi possível encerrar o auxiliar: " + ex.Message); }
            finally { process.Dispose(); }
        }
        public void Dispose() => StopWorker(true);
    }
}
