using Autodesk.Revit.DB;
using RevitView = Autodesk.Revit.DB.View;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class SheetImporter
    {
        private const string PreferredLegendSeedName = "RCR_LEGENDA_SEMENTE";

        private readonly Document _doc;
        private readonly ILogger _logger;
        private readonly DetailDwgWriter _writer;

        public SheetImporter(Document doc, ILogger logger)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? new Logger("DetailImport");
            _writer = new DetailDwgWriter(_logger);
        }

        public DetailImportResult Import(
            DwgDocumentModel source,
            IList<DetailRegion> regions,
            ImportSettings settings,
            Action<string> progress = null)
        {
            if (source == null)
                throw new InvalidOperationException("Nenhum DWG foi carregado.");

            List<DetailRegion> selected = (regions ?? new List<DetailRegion>()).ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("Selecione pelo menos um detalhe para importar.");

            settings = settings ?? new ImportSettings();
            settings.Normalize();

            var result = new DetailImportResult
            {
                TotalRequested = selected.Count
            };
            var stopwatch = Stopwatch.StartNew();
            var importStopwatch = Stopwatch.StartNew();
            string tempFolder = CreateTempFolder();

            try
            {
                for (int index = 0; index < selected.Count; index++)
                {
                    DetailRegion region = selected[index];
                    Stopwatch detailStopwatch = Stopwatch.StartNew();
                    string stage = "preparando detalhe";
                    string detailName = string.IsNullOrWhiteSpace(region.Name)
                        ? $"Detalhe {region.Id:000}"
                        : region.Name;

                    try
                    {
                        ReportProgress(progress, $"Detalhe {index + 1}/{selected.Count}: gerando DWG - {detailName}");
                        stage = "gerando DWG temporario";
                        string detailPath = _writer.WriteDetail(source, region, tempFolder, settings);

                        ImportedDetail imported;
                        using (Transaction transaction = new Transaction(_doc, $"Importar detalhe DWG {index + 1}/{selected.Count}"))
                        {
                            transaction.Start();
                            try
                            {
                                ReportProgress(progress, $"Detalhe {index + 1}/{selected.Count}: criando vista - {detailName}");
                                stage = "criando vista";
                                RevitView view = CreateDetailView(region, result);
                                ElementId viewId = view.Id;
                                string viewName = view.Name;
                                bool createdAsLegend = view.ViewType == ViewType.Legend;
                                stage = "importando DWG na vista";
                                ReportProgress(progress, $"Detalhe {index + 1}/{selected.Count}: importando DWG - {detailName}");
                                ElementId importId;
                                try
                                {
                                    importId = ImportDwg(view, detailPath, settings);
                                }
                                catch (Exception ex) when (createdAsLegend)
                                {
                                    string warning = $"A Legend {viewName} foi criada, mas o Revit recusou a importacao do DWG nela; foi criada uma Drafting View como alternativa. Motivo: {ex.Message}";
                                    if (!result.Warnings.Contains(warning))
                                        result.Warnings.Add(warning);
                                    _logger.Warn(warning);

                                    TryDeleteView(viewId);
                                    view = CreateDraftingFallback(GetUniqueViewName(viewName));
                                    viewId = view.Id;
                                    viewName = view.Name;
                                    createdAsLegend = false;

                                    importId = ImportDwg(view, detailPath, settings);
                                }

                                stage = "finalizando importacao";
                                if (!TryNormalizeImportInstance(view, importId, region))
                                {
                                    string warning = $"O detalhe {region.Name} foi importado em {viewName}, mas o Revit nao permitiu reler/reposicionar o elemento importado. A vista foi mantida.";
                                    if (!result.Warnings.Contains(warning))
                                        result.Warnings.Add(warning);
                                    _logger.Warn(warning);
                                }

                                imported = new ImportedDetail
                                {
                                    Name = region.Name,
                                    Position = XYZ.Zero,
                                    Scale = settings.Scale,
                                    ImportInstanceId = importId,
                                    ViewId = viewId,
                                    ViewName = viewName,
                                    CreatedAsLegend = createdAsLegend,
                                    SourceFile = detailPath
                                };

                                TransactionStatus commitStatus = transaction.Commit();
                                if (commitStatus != TransactionStatus.Committed)
                                    throw new InvalidOperationException($"O Revit não confirmou a transação (estado: {commitStatus}).");
                            }
                            catch
                            {
                                if (transaction.GetStatus() == TransactionStatus.Started)
                                    transaction.RollBack();
                                throw;
                            }
                        }

                        result.Imported.Add(imported);
                        detailStopwatch.Stop();
                        _logger.Info($"Detalhe {detailName} concluido em {detailStopwatch.ElapsedMilliseconds} ms.");
                    }
                    catch (Exception ex)
                    {
                        detailStopwatch.Stop();
                        string failure = $"{region.Name ?? "Detalhe"}: {stage}: {ex.Message}";
                        result.Failures.Add(failure);
                        _logger.Error(failure + $" Tempo: {detailStopwatch.ElapsedMilliseconds} ms.", ex);
                    }
                }
            }
            finally
            {
                _writer.Dispose();
            }

            importStopwatch.Stop();
            stopwatch.Stop();
            result.ImportMilliseconds = importStopwatch.ElapsedMilliseconds;
            result.TotalMilliseconds = stopwatch.ElapsedMilliseconds;

            _logger.Info($"Criacao de vistas concluida. Solicitados={result.TotalRequested}; importados={result.Imported.Count}; falhas={result.Failures.Count}; {result.ImportMilliseconds} ms.");
            return result;
        }

        private void ReportProgress(Action<string> progress, string message)
        {
            _logger.Info(message);
            try { progress?.Invoke(message); }
            catch { }
        }

        private ElementId ImportDwg(RevitView view, string path, ImportSettings settings)
        {
            var options = new DWGImportOptions
            {
                ThisViewOnly = true,
                Placement = ImportPlacement.Origin,
                CustomScale = settings.Scale,
                ColorMode = ImportColorMode.Preserved,
                VisibleLayersOnly = false,
                OrientToView = true,
                Unit = ImportUnit.Default
            };

            if (!_doc.Import(path, options, view, out ElementId importedId) ||
                importedId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("A Revit API não conseguiu importar o DWG temporário.");
            }

            return importedId;
        }

        private bool TryNormalizeImportInstance(RevitView view, ElementId importId, DetailRegion region)
        {
            try
            {
                _doc.Regenerate();
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel regenerar a vista do detalhe {region.Name}: {ex.Message}");
                return false;
            }

            Element element;
            try
            {
                element = _doc.GetElement(importId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel reler a importacao do detalhe {region.Name}: {ex.Message}");
                return false;
            }

            if (element == null || !element.IsValidObject)
            {
                _logger.Warn($"A importacao do detalhe {region.Name} retornou um elemento invalido. A vista foi mantida sem reposicionamento adicional.");
                return false;
            }

            try
            {
                BoundingBoxXYZ box = element.get_BoundingBox(view);
                if (box == null)
                    return true;

                XYZ move = new XYZ(-box.Min.X, -box.Min.Y, 0);
                if (move.IsZeroLength())
                    return true;

                if (element.Pinned)
                {
                    element.Pinned = false;
                    _logger.Info($"Importacao {region.Name} veio pinada pelo Revit e foi destravada para posicionamento.");
                }

                if (element.IsValidObject)
                    ElementTransformUtils.MoveElement(_doc, importId, move);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Detalhe {region.Name} foi importado, mas nao foi possivel reposicionar a importacao: {ex.Message}");
            }

            return true;
        }

        private RevitView CreateDetailView(DetailRegion region, DetailImportResult result)
        {
            string viewName = GetUniqueViewName($"DET - {DetailNamingService.CleanName(region.Name)}");
            RevitView seed = FindLegendSeed();
            bool foundLegendSeed = seed != null;

            if (seed != null)
            {
                try
                {
                    int seedElementCount = GetViewSpecificElementIds(seed.Id).Count;
                    ElementId duplicateId = seed.Duplicate(ViewDuplicateOption.Duplicate);
                    _doc.Regenerate();

                    RevitView legend = _doc.GetElement(duplicateId) as RevitView;
                    if (legend == null || !legend.IsValidObject)
                        throw new InvalidOperationException("A legenda semente foi duplicada, mas a nova vista não pôde ser lida.");

                    legend.Name = viewName;
                    TrySetViewScale(legend, 50);

                    if (ShouldCleanLegendSeed(seed, seedElementCount))
                    {
                        ClearViewElements(legend);
                        _doc.Regenerate();

                        legend = _doc.GetElement(duplicateId) as RevitView;
                        if (legend == null || !legend.IsValidObject)
                        {
                            throw new InvalidOperationException(
                                $"A legenda duplicada foi invalidada durante a limpeza de {seedElementCount} elemento(s) herdado(s) da semente.");
                        }
                    }

                    return legend;
                }
                catch (Exception ex)
                {
                    string seedWarning = $"Nao foi possivel preparar uma Legend a partir da semente {seed.Name}; foi criada uma Drafting View como alternativa. Motivo: {ex.Message} Crie uma Legend vazia chamada {PreferredLegendSeedName} para o plugin duplicar sem limpar conteudo herdado.";
                    if (!result.Warnings.Contains(seedWarning))
                        result.Warnings.Add(seedWarning);
                    _logger.Warn(seedWarning);
                }
            }

            ViewDrafting drafting = CreateDraftingFallback(viewName);
            string warning = foundLegendSeed
                ? "A Legend encontrada nao serviu como semente limpa; foi criada uma Drafting View como alternativa."
                : $"Nenhuma vista Legend duplicavel foi encontrada. Crie uma Legend vazia chamada {PreferredLegendSeedName} para gerar Legends automaticamente.";
            if (!result.Warnings.Contains(warning))
                result.Warnings.Add(warning);
            _logger.Warn(warning);
            return drafting;
        }

        private RevitView FindLegendSeed()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitView))
                .Cast<RevitView>()
                .Where(view =>
                    !view.IsTemplate &&
                    view.ViewType == ViewType.Legend &&
                    view.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                .Select(view => new
                {
                    View = view,
                    ElementCount = GetViewSpecificElementIds(view.Id).Count,
                    Rank = GetLegendSeedRank(view)
                })
                .OrderBy(item => item.Rank)
                .ThenBy(item => item.ElementCount)
                .ThenBy(item => item.View.Name)
                .Select(item => item.View)
                .FirstOrDefault();
        }

        private int GetLegendSeedRank(RevitView view)
        {
            if (view.Name.Equals(PreferredLegendSeedName, StringComparison.OrdinalIgnoreCase))
                return 0;

            if (view.Name.IndexOf("SEMENTE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                view.Name.IndexOf("SEED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                view.Name.IndexOf("VAZIA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }

            if (GetViewSpecificElementIds(view.Id).Count == 0)
                return 2;

            return 3;
        }

        private bool ShouldCleanLegendSeed(RevitView seed, int seedElementCount)
        {
            if (seedElementCount == 0)
                return false;

            if (seed.Name.Equals(PreferredLegendSeedName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (seed.Name.IndexOf("SEMENTE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                seed.Name.IndexOf("SEED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                seed.Name.IndexOf("VAZIA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private ViewDrafting CreateDraftingFallback(string viewName)
        {
            ViewFamilyType draftingType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(type => type.ViewFamily == ViewFamily.Drafting);

            if (draftingType == null)
                throw new InvalidOperationException("Nenhum tipo de Drafting View foi encontrado para criar a vista do detalhe.");

            ViewDrafting view = ViewDrafting.Create(_doc, draftingType.Id);
            view.Name = GetUniqueViewName(viewName);
            TrySetViewScale(view, 50);
            return view;
        }

        private void ClearViewElements(RevitView view)
        {
            List<ElementId> ids = GetViewSpecificElementIds(view.Id);

            if (ids.Count == 0)
                return;

            try
            {
                _doc.Delete(ids);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel limpar todos os elementos da legenda semente {view.Name}: {ex.Message}");
            }
        }

        private List<ElementId> GetViewSpecificElementIds(ElementId viewId)
        {
            return new FilteredElementCollector(_doc, viewId)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Where(id => ShouldDeleteSeedElement(id, viewId))
                .ToList();
        }

        private bool ShouldDeleteSeedElement(ElementId elementId, ElementId viewId)
        {
            if (elementId == viewId)
                return false;

            Element element = _doc.GetElement(elementId);
            return element != null &&
                   element.IsValidObject &&
                   !(element is SketchPlane) &&
                   element.Category != null &&
                   element.ViewSpecific &&
                   element.OwnerViewId == viewId;
        }

        private void TryDeleteView(ElementId viewId)
        {
            if (viewId == ElementId.InvalidElementId)
                return;

            try
            {
                Element element = _doc.GetElement(viewId);
                if (element != null && element.IsValidObject)
                    _doc.Delete(viewId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel remover a vista temporaria invalida {viewId.IntegerValue}: {ex.Message}");
            }
        }

        private void TrySetViewScale(RevitView view, int scale)
        {
            try
            {
                view.Scale = scale;
            }
            catch { }
        }

        private string GetUniqueViewName(string baseName)
        {
            baseName = string.IsNullOrWhiteSpace(baseName)
                ? "DET - Detalhe"
                : baseName.Trim();

            HashSet<string> names = new HashSet<string>(
                new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitView))
                .Cast<RevitView>()
                .Select(view => view.Name),
                StringComparer.OrdinalIgnoreCase);

            if (!names.Contains(baseName))
                return baseName;

            for (int i = 2; i < 10000; i++)
            {
                string candidate = $"{baseName} {i:00}";
                if (!names.Contains(candidate))
                    return candidate;
            }

            return $"{baseName} {DateTime.Now:HHmmss}";
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "RevitFamilyImporter",
                "DetailImports",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}




