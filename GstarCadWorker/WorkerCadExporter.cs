using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace RevitFamilyImporter.Import.DetailImport
{
    internal class GstarCadDetailExporter : IDisposable
    {
        private const int SelectionSetCrossing = 1;

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        private sealed class OwnershipException : Exception
        {
            public OwnershipException(string message) : base(message) { }
        }

        private static HashSet<int> GetExistingProcessIds()
        {
            var ids = new HashSet<int>();
            foreach (Process process in Process.GetProcesses())
            {
                try { ids.Add(process.Id); }
                finally { process.Dispose(); }
            }
            return ids;
        }

        private static int VerifyNewCadProcess(object app, HashSet<int> before)
        {
            try
            {
                long hwnd = Convert.ToInt64(((dynamic)app).HWND);
                uint pid;
                if (hwnd == 0 || GetWindowThreadProcessId(new IntPtr(hwnd), out pid) == 0 ||
                    pid == 0 || before.Contains((int)pid))
                    throw new OwnershipException("Não foi possível comprovar que a sessão do GstarCAD foi iniciada pelo auxiliar; a sessão será preservada.");
                return (int)pid;
            }
            catch (OwnershipException) { throw; }
            catch (Exception ex)
            {
                throw new OwnershipException("Não foi possível identificar a sessão do GstarCAD; nenhuma sessão será encerrada. " + ex.Message);
            }
        }

        internal sealed class SelectionLimitException : Exception
        {
            public SelectionLimitException(string message) : base(message) { }
        }

        private static readonly string[] ProgIds =
        {
            "GStarCAD.Application",
            "Gcad.Application",
            "GStarCAD.Application.23",
            "Gcad.Application.23",
            "GStarCAD.Application.22",
            "Gcad.Application.22",
            "GcadVba.GcadApplication"
        };

        private readonly ILogger _logger;
        private object _app;
        private string _progId;
        private dynamic _sourceDoc;
        private string _sourcePath;
        private bool _disabled;
        private bool _disposed;

        public GstarCadDetailExporter(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public static bool TryGetRunningSessions(out string sessions)
        {
            var found = new List<string>();

            try
            {
                int currentProcessId;
                using (Process current = Process.GetCurrentProcess()) currentProcessId = current.Id;
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.Id == currentProcessId)
                            continue;
                        string name = process.ProcessName ?? string.Empty;
                        if (name.IndexOf("gcad", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("gstar", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        found.Add($"{name} (PID {process.Id})");
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch { }

            sessions = string.Join(", ", found.Distinct(StringComparer.OrdinalIgnoreCase));
            return found.Count > 0;
        }

        public bool TryExport(
            DwgDocumentModel source,
            DetailRegion region,
            string outputFolder,
            string boundaryLayer,
            string nameLayer,
            int maxSelectedEntities,
            out string filePath)
        {
            filePath = null;

            if (source == null ||
                string.IsNullOrWhiteSpace(source.FilePath) ||
                !File.Exists(source.FilePath) ||
                region == null)
            {
                return false;
            }

            try
            {
                if (!EnsureSession(source.FilePath))
                    return false;

                Directory.CreateDirectory(outputFolder);
                string name = DetailNamingService.CleanName(region.Name);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Detalhe_{region.Id:000}";

                filePath = Path.Combine(outputFolder, $"{region.Id:000}_{name}_gstar.dwg");
                TryDeleteFile(filePath);

                dynamic selection = null;
                try
                {
                    selection = CreateSelectionSet(_sourceDoc, "RCR_DETAIL_EXPORT");
                    _logger.Info($"GstarCAD: iniciando seleção para {region.Name}.");
                    SelectCrossing(selection, region.Bounds.Expand(GetSelectionTolerance(region)));
                    _logger.Info($"GstarCAD: seleção concluída para {region.Name}.");

                    int count = GetSelectionCount(selection);
                    if (count == 0)
                        throw new InvalidOperationException("GstarCAD não encontrou entidades no recorte do detalhe.");
                    if (count > maxSelectedEntities)
                    {
                        throw new SelectionLimitException(
                            $"A seleção do GstarCAD encontrou {count} entidades, acima do limite configurado de {maxSelectedEntities}.");
                    }

                    _logger.Info($"GstarCAD: iniciando WBlock para {region.Name}; entidades={count}.");
                    _sourceDoc.WBlock(filePath, selection);
                    _logger.Info($"GstarCAD: WBlock concluído para {region.Name}.");
                }
                finally
                {
                    if (selection != null)
                    {
                        TryDeleteSelectionSet(selection);
                        TryReleaseComObject((object)selection);
                    }
                }

                _logger.Info($"Detalhe {region.Name} exportado por GstarCAD ({_progId}): {filePath}");
                return File.Exists(filePath);
            }
            catch (SelectionLimitException)
            {
                if (!string.IsNullOrWhiteSpace(filePath)) TryDeleteFile(filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Exportacao de alta fidelidade por GstarCAD falhou para {region?.Name}: {ex.Message}");
                if (!string.IsNullOrWhiteSpace(filePath))
                    TryDeleteFile(filePath);
                filePath = null;
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CloseSession();
        }

        private bool EnsureSession(string sourcePath)
        {
            if (_disabled)
                return false;

            try
            {
                if (_app == null)
                {
                    if (TryGetRunningSessions(out string runningSessions))
                    {
                        _logger.Warn($"GstarCAD já está em uso ({runningSessions}). A exportação de alta fidelidade foi ignorada para não interferir com a sessão do usuário.");
                        _disabled = true;
                        return false;
                    }

                    _logger.Info("GstarCAD: iniciando criação da aplicação COM.");
                    _app = CreateApplication(out _progId);
                    if (_app == null)
                    {
                        _disabled = true;
                        return false;
                    }

                    _logger.Info($"Sessao unica GstarCAD iniciada para exportacao de detalhes ({_progId}).");
                }

                if (_sourceDoc != null &&
                    string.Equals(_sourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                TryReleaseComObject((object)_sourceDoc);
                _sourceDoc = null;
                _logger.Info($"GstarCAD: iniciando abertura do DWG {sourcePath}.");
                _sourceDoc = OpenDocument(_app, sourcePath, true);
                _logger.Info("GstarCAD: abertura do DWG concluída.");
                _sourcePath = sourcePath;
                return _sourceDoc != null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel iniciar/reaproveitar sessao do GstarCAD. A exportacao de alta fidelidade falhou. Motivo: {ex.Message}");
                _disabled = true;
                CloseSession();
                return false;
            }
        }

        private void CloseSession()
        {
            // Não feche documentos nem a aplicação CAD: o usuário pode ter começado
            // a trabalhar nessa sessão enquanto ela estava visível.
            TryReleaseComObject((object)_sourceDoc);
            _sourceDoc = null;
            _sourcePath = null;
            TryReleaseComObject(_app);
            _app = null;
            _progId = null;
        }

        private object CreateApplication(out string progId)
        {
            progId = null;

            foreach (string candidate in ProgIds)
            {
                try
                {
                    Type type = Type.GetTypeFromProgID(candidate);
                    if (type == null)
                        continue;

                    HashSet<int> existingProcesses = GetExistingProcessIds();
                    object app = Activator.CreateInstance(type);
                    if (app == null)
                        continue;

                    try
                    {
                        int cadPid = VerifyNewCadProcess(app, existingProcesses);
                        _logger.Info($"Sessão GstarCAD identificada: PID {cadPid}, ProgID {candidate}.");
                    }
                    catch { TryReleaseComObject(app); throw; }
                    progId = candidate;
                    // A sessão fica visível para que avisos CAD não deixem o Revit aguardando uma janela oculta.
                    TrySetProperty(app, "Visible", true);
                    TrySetProperty(app, "DisplayAlerts", true);
                    return app;
                }
                catch (OwnershipException) { throw; }
                catch
                {
                    // Try the next registered GstarCAD ProgID.
                }
            }

            return null;
        }

        private dynamic OpenDocument(object app, string path, bool readOnly)
        {
            dynamic documents = ((dynamic)app).Documents;

            return documents.Open(path, readOnly);
        }

        private dynamic CreateSelectionSet(dynamic doc, string name)
        {
            dynamic sets = doc.SelectionSets;
            DeleteSelectionSetIfExists(sets, name);
            return sets.Add(name);
        }

        private void DeleteSelectionSetIfExists(dynamic sets, string name)
        {
            try
            {
                foreach (object item in (IEnumerable)sets)
                {
                    dynamic selection = item;
                    string selectionName = Convert.ToString(selection.Name);
                    if (selectionName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        selection.Delete();
                        return;
                    }
                }
            }
            catch
            {
                // Selection set cleanup is best-effort because CAD COM collections vary by version.
            }
        }

        private void SelectCrossing(dynamic selection, DwgBounds bounds)
        {
            double[] min = { bounds.MinX, bounds.MinY, 0.0 };
            double[] max = { bounds.MaxX, bounds.MaxY, 0.0 };
            selection.Select(SelectionSetCrossing, min, max);
        }

        private int GetSelectionCount(dynamic selection)
        {
            try
            {
                return Convert.ToInt32(selection.Count);
            }
            catch
            {
                return 0;
            }
        }

        private void TryReleaseComObject(object value)
        {
            if (value == null)
                return;

            try
            {
                if (Marshal.IsComObject(value))
                    Marshal.FinalReleaseComObject(value);
            }
            catch
            {
                // COM cleanup is best-effort.
            }
        }

        private void TryDeleteSelectionSet(dynamic selection)
        {
            try { selection.Delete(); } catch { }
        }

        private void TrySetProperty(object target, string name, object value)
        {
            try
            {
                target.GetType().InvokeMember(
                    name,
                    System.Reflection.BindingFlags.SetProperty,
                    null,
                    target,
                    new[] { value });
            }
            catch
            {
                try { ((dynamic)target).GetType().GetProperty(name)?.SetValue(target, value); } catch { }
            }
        }

        private double GetSelectionTolerance(DetailRegion region)
        {
            double size = Math.Max(region.Bounds.Width, region.Bounds.Height);
            return Math.Max(size * 0.002, 0.01);
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The fallback exporter can use a different file if this one is locked.
            }
        }
    }
}


