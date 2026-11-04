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
        private bool _startedApplication;

        public GstarCadDetailExporter(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
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
                    SelectCrossing(selection, region.Bounds.Expand(GetSelectionTolerance(region)));

                    int count = GetSelectionCount(selection);
                    if (count == 0)
                        throw new InvalidOperationException("GstarCAD não encontrou entidades no recorte do detalhe.");
                    if (count > maxSelectedEntities)
                    {
                        throw new InvalidOperationException(
                            $"A seleção do GstarCAD encontrou {count} entidades, acima do limite configurado de {maxSelectedEntities}.");
                    }

                    _sourceDoc.WBlock(filePath, selection);
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

                CloseDocument(_sourceDoc, false);
                _sourceDoc = OpenDocument(_app, sourcePath, true);
                _sourcePath = sourcePath;
                return _sourceDoc != null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel iniciar/reaproveitar sessao do GstarCAD. O motor interno sera usado nesta importacao. Motivo: {ex.Message}");
                _disabled = true;
                CloseSession();
                return false;
            }
        }

        private void CloseSession()
        {
            CloseDocument(_sourceDoc, false);
            _sourceDoc = null;
            _sourcePath = null;

            if (_startedApplication)
                QuitApplication(_app);
            TryReleaseComObject(_app);
            _app = null;
            _progId = null;
            _startedApplication = false;
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

                    object app = Activator.CreateInstance(type);
                    if (app == null)
                        continue;

                    progId = candidate;
                    _startedApplication = true;
                    // A sessão fica visível para que avisos CAD não deixem o Revit aguardando uma janela oculta.
                    TrySetProperty(app, "Visible", true);
                    TrySetProperty(app, "DisplayAlerts", true);
                    return app;
                }
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

            try
            {
                return documents.Open(path, readOnly);
            }
            catch
            {
                return documents.Open(path);
            }
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

        private void CloseDocument(dynamic doc, bool saveChanges)
        {
            if (doc == null)
                return;

            try
            {
                doc.Close(saveChanges);
            }
            catch
            {
                try { doc.Close(); } catch { }
            }

            TryReleaseComObject(doc);
        }

        private void QuitApplication(object app)
        {
            if (app == null)
                return;

            try
            {
                ((dynamic)app).Quit();
            }
            catch
            {
                // Some GstarCAD versions refuse Quit while COM still releases the instance later.
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


