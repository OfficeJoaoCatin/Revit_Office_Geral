using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;

namespace RevitFamilyImporter.LinkConflict.Services
{
    public class LinkConflictAnalysisService
    {
        private const double IntersectionToleranceCubicFeet = 1e-8;
        private const double BoundingBoxToleranceFeet = 1e-6;

        public List<LinkedCategoryOption> GetCategories(Document linkedDocument, Action<string, int, int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (linkedDocument == null) return new List<LinkedCategoryOption>();
            progress?.Invoke("Identificando categorias do link...", 0, 0);
            var categories = new Dictionary<int, LinkedCategoryOption>();
            int processed = 0;
            foreach (Element element in new FilteredElementCollector(linkedDocument).WhereElementIsNotElementType())
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                if (processed % 500 == 0) progress?.Invoke($"Identificando categorias: {processed:N0} elementos...", 0, 0);
                if (!IsModelElement(element)) continue;
                Category category = element.Category;
                int id = category.Id.IntegerValue;
                if (!categories.TryGetValue(id, out LinkedCategoryOption item))
                {
                    item = new LinkedCategoryOption { CategoryId = category.Id, Name = category.Name };
                    categories[id] = item;
                }
                item.Count++;
            }
            return categories.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public LinkConflictReport Analyze(Document hostDocument, RevitLinkInstance linkInstance, IReadOnlyCollection<LinkedCategoryOption> categories,
            Action<string, int, int> progress = null, CancellationToken cancellationToken = default, bool diagnosticMode = false, int diagnosticStartPosition = 1)
        {
            if (hostDocument == null)
                throw new ArgumentNullException(nameof(hostDocument));
            if (linkInstance == null)
                throw new ArgumentNullException(nameof(linkInstance));
            if (categories == null || categories.Count == 0)
                throw new ArgumentException("Selecione pelo menos uma categoria do link.", nameof(categories));

            Document linkedDocument = linkInstance.GetLinkDocument();
            if (linkedDocument == null)
                throw new InvalidOperationException("O modelo vinculado está descarregado ou inacessível.");

            var stopwatch = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Coletando elementos das categorias selecionadas...", 0, 0);
            List<ElementFilter> categoryFilters = categories
                .Select(category => (ElementFilter)new ElementCategoryFilter(category.CategoryId))
                .ToList();
            ElementFilter filter = categoryFilters.Count == 1
                ? categoryFilters[0]
                : new LogicalOrFilter(categoryFilters);
            List<ElementId> linkedIds = new FilteredElementCollector(linkedDocument)
                .WhereElementIsNotElementType()
                .WherePasses(filter)
                .ToElementIds()
                .ToList();

            LinkConflictReport report = new LinkConflictReport
            {
                LinkName = RevitText.ElementName(linkInstance),
                LinkInstanceId = linkInstance.Id.IntegerValue,
                LinkedDocumentTitle = linkedDocument.Title,
                CategoryName = string.Join(", ", categories.Select(category => category.Name))
            };

            var diagnostics = diagnosticMode ? new LinkConflictDiagnostics() : null;
            var coverage = new UnverifiedCoverage();
            string diagnosticStopReason = string.Empty;
            (int startIndex, int analysisLimit) = diagnosticMode
                ? LinkConflictDiagnostics.GetSampleRange(linkedIds.Count, diagnosticStartPosition)
                : (0, linkedIds.Count);
            int analysisCount = analysisLimit - startIndex;
            ElementFilter dwgHostFilter = CreateDwgHostFilter(hostDocument);
            ElementFilter cameraHostFilter = CreateInternalCameraFilter(hostDocument, cancellationToken);
            progress?.Invoke("Preparando busca de elementos físicos...", 0, 0);
            var curvePreparation = Stopwatch.StartNew();
            ElementFilter curveHostFilter = CreateVerifiedCurveFilter(hostDocument, cancellationToken, out int excludedCurveCount, out bool curveScanPartial, out bool curveScanFailed);
            curvePreparation.Stop();
            var emptyRegions = new EmptyRegionCache();
            bool? ProbeEmptyRegion(RegionBounds region)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outline = new Outline(new XYZ(region.MinX, region.MinY, region.MinZ), new XYZ(region.MaxX, region.MaxY, region.MaxZ));
                var collector = new FilteredElementCollector(hostDocument)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementClassFilter(typeof(RevitLinkInstance), true))
                    .WherePasses(new BoundingBoxIntersectsFilter(outline, BoundingBoxToleranceFeet));
                if (dwgHostFilter != null) collector.WherePasses(dwgHostFilter);
                if (cameraHostFilter != null) collector.WherePasses(cameraHostFilter);
                if (curveHostFilter != null) collector.WherePasses(curveHostFilter);
                ElementId first = collector.FirstElementId();
                if (first == null) return null;
                return first == ElementId.InvalidElementId;
            }
            Transform linkTransform = linkInstance.GetTotalTransform();
            Options geometryOptions = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            var conflictKeys = new HashSet<(int Category, int LinkedOwner, int HostOwner)>();
            // Limite evita reter indefinidamente a geometria de um projeto grande.
            const int maxCachedHosts = 1024;
            var hostCache = new BoundedLruCache<int, HostGeometry>(maxCachedHosts);
            // Busca aprofundada é reservada a famílias que não puderam ser verificadas.
            var nestedHostCache = new BoundedLruCache<int, NestedGeometry>(256);
            var ownerCache = new BoundedLruCache<int, Element>(8192);
            var metadataCache = new BoundedLruCache<int, ElementMetadata>(8192);
            long nextProgressMilliseconds = 0;
            long pairsChecked = 0;
            long geometryTicks = 0, searchTicks = 0, comparisonTicks = 0;
            int prefiltered = 0;
            long rejectedNonIntersections = 0, geometryRequests = 0, geometryCacheMisses = 0;
            long currentComparisonStart = 0, currentComparisonGeometryStart = 0;
            void ReportAnalysisProgress(int index)
            {
                if (progress == null || stopwatch.ElapsedMilliseconds < nextProgressMilliseconds) return;
                nextProgressMilliseconds = stopwatch.ElapsedMilliseconds + 1000;
                double searchSeconds = (double)searchTicks / Stopwatch.Frequency;
                double geometrySeconds = (double)geometryTicks / Stopwatch.Frequency;
                long liveComparisonTicks = comparisonTicks;
                if (currentComparisonStart != 0)
                    liveComparisonTicks += Stopwatch.GetTimestamp() - currentComparisonStart - (geometryTicks - currentComparisonGeometryStart);
                double comparisonSeconds = (double)Math.Max(0, liveComparisonTicks) / Stopwatch.Frequency;
                progress($"{(diagnosticMode ? "Diagnóstico parcial" : "Analisando elementos do link")}: {pairsChecked:N0} pares; {(diagnostics?.ReportedConflicts ?? report.Conflicts.Count):N0} conflitos físicos confirmados. " +
                    $"Não verificados: link sem sólidos {coverage.LinkedWithoutSolids:N0}; pares sem sólidos no hospedeiro {coverage.PairsWithoutHostSolids:N0}; geometria incompleta {coverage.PairsWithIncompleteGeometry:N0}; falhas geométricas {coverage.PairsWithBooleanFailure:N0}. " +
                    $"Sem interseção {rejectedNonIntersections:N0}; extrações host {geometryCacheMisses:N0}/{geometryRequests:N0}; regiões vazias reaproveitadas {emptyRegions.EmptyRegionReuses:N0}. " +
                    $"Tempos acumulados: busca {searchSeconds:N0}s; geometria {geometrySeconds:N0}s; comparação {comparisonSeconds:N0}s", index - startIndex, analysisCount);
            }

            for (int linkedIndex = startIndex; linkedIndex < analysisLimit; linkedIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (diagnostics != null && LinkConflictDiagnostics.ShouldStop(pairsChecked, stopwatch.Elapsed, out diagnosticStopReason)) break;
                ReportAnalysisProgress(linkedIndex);
                Element linkedElement = linkedDocument.GetElement(linkedIds[linkedIndex]);
                if (!IsModelElement(linkedElement)) continue;
                report.LinkedElementCount++;
                // A caixa do elemento envolve a geometria do modelo. Se não estiver disponível,
                // for inválida ou não puder ser consultada, preserva-se a análise completa.
                long stageStart = Stopwatch.GetTimestamp();
                PreliminaryCandidates preliminary = GetPreliminaryCandidates(hostDocument, linkedElement, linkTransform, dwgHostFilter, cameraHostFilter, curveHostFilter, emptyRegions, ProbeEmptyRegion, cancellationToken);
                searchTicks += Stopwatch.GetTimestamp() - stageStart;
                if (preliminary?.Ids != null && preliminary.Ids.Count == 0) { prefiltered++; continue; }

                stageStart = Stopwatch.GetTimestamp();
                HostGeometry linkedGeometry = ExtractGeometry(linkedElement, geometryOptions, linkTransform, cancellationToken);
                List<Solid> linkedSolids = linkedGeometry.Solids;
                var linkedBoxes = new Dictionary<Solid, Outline>();
                bool linkedUnresolved = linkedGeometry.HasUnresolvedBounds;
                List<Outline> linkedPartOutlines = linkedSolids.Count > 0
                    ? GetSolidOutlines(linkedElement, linkedSolids, linkTransform, out linkedUnresolved)
                    : linkedGeometry.Outlines;
                diagnostics?.RecordEmptySolids(linkedGeometry.EmptySolidOccurrences, true);
                Outline outline = GetCombinedOutline(linkedPartOutlines);
                geometryTicks += Stopwatch.GetTimestamp() - stageStart;
                if (outline == null || linkedUnresolved)
                {
                    report.UnboundedLinkedElementCount++;
                    diagnostics?.RecordUnboundedLinked(linkedElement.Id.IntegerValue, linkedElement.Category?.Name ?? "(sem categoria)");
                    coverage.RecordLinkedBounds(linkedElement.Id.IntegerValue, linkedElement.Category?.Name ?? "(sem categoria)");
                }
                if (linkedSolids.Count == 0)
                {
                    coverage.RecordLinkedWithoutSolids(linkedElement.Id.IntegerValue, linkedElement.Category?.Name ?? "(sem categoria)");
                    if (!diagnosticMode) continue;
                }
                if (outline == null)
                    continue;
                if (diagnostics != null && linkedSolids.Count == 0)
                {
                    XYZ minimum = outline.MinimumPoint, maximum = outline.MaximumPoint;
                    string family = string.Empty, type = string.Empty;
                    try
                    {
                        family = RevitText.FamilyName(linkedElement.Document, linkedElement);
                        type = RevitText.ElementTypeName(linkedElement.Document, linkedElement);
                    }
                    catch { /* A coleta de metadados não pode interromper o diagnóstico. */ }
                    diagnostics.RecordLinkedBounds(linkedElement.Id.IntegerValue, family, type,
                        maximum.X - minimum.X, maximum.Y - minimum.Y, maximum.Z - minimum.Z, linkedPartOutlines.Count);
                }

                stageStart = Stopwatch.GetTimestamp();
                bool completeParts = linkedSolids.Count == 0 || linkedPartOutlines.Count == linkedSolids.Count;
                List<Element> hostCandidates = FindHostCandidates(hostDocument, outline, linkedPartOutlines,
                    completeParts, preliminary, dwgHostFilter, cameraHostFilter, curveHostFilter, cancellationToken);
                searchTicks += Stopwatch.GetTimestamp() - stageStart;

                report.CandidateHostElementCount += hostCandidates.Count;
                stageStart = Stopwatch.GetTimestamp();
                long geometryBeforeCandidates = geometryTicks;
                currentComparisonStart = stageStart;
                currentComparisonGeometryStart = geometryBeforeCandidates;
                Element linkedOwner = hostCandidates.Count == 0 ? null : GetTopLevelFamilyInstance(linkedElement);
                int categoryId = linkedElement.Category.Id.IntegerValue;
                ElementMetadata linkedMetadata = null;

                foreach (Element hostElement in hostCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (diagnostics != null && LinkConflictDiagnostics.ShouldStop(pairsChecked, stopwatch.Elapsed, out diagnosticStopReason)) break;
                    ReportAnalysisProgress(linkedIndex);
                    int hostId = hostElement.Id.IntegerValue;
                    Element hostOwner = ownerCache.GetOrAdd(hostId, () => GetTopLevelFamilyInstance(hostElement));
                    var key = (categoryId, linkedOwner.Id.IntegerValue, hostOwner.Id.IntegerValue);
                    if (!conflictKeys.Add(key))
                        continue;
                    geometryRequests++;
                    bool cacheMiss = false;
                    HostGeometry hostGeometry = hostCache.GetOrAdd(hostId, () =>
                    {
                        geometryCacheMisses++;
                        cacheMiss = true;
                        long geometryStart = Stopwatch.GetTimestamp();
                        try
                        {
                            HostGeometry extracted = ExtractGeometry(hostElement, geometryOptions, Transform.Identity, cancellationToken);
                            diagnostics?.RecordEmptySolids(extracted.EmptySolidOccurrences, false);
                            return extracted;
                        }
                        finally { geometryTicks += Stopwatch.GetTimestamp() - geometryStart; }
                    });
                    diagnostics?.RecordHostRequest(hostId, cacheMiss);
                    pairsChecked++;
                    if (diagnostics != null && (linkedSolids.Count == 0 || hostGeometry.Solids.Count == 0))
                        diagnostics.RecordPair(linkedElement.Id.IntegerValue, linkedElement.Category?.Name ?? "(sem categoria)", linkedElement.GetType().Name,
                            hostId, hostElement.Category?.Name ?? "(sem categoria)", hostElement.Category?.Id?.IntegerValue ?? 0,
                            hostElement.GetType().Name, linkedSolids.Count == 0, hostGeometry.Solids.Count == 0);
                    IntersectionData intersection = linkedSolids.Count == 0 || hostGeometry.Solids.Count == 0
                        ? new IntersectionData()
                        : GetIntersectionData(linkedSolids, hostGeometry.Solids, linkedBoxes, hostGeometry.SolidBoxes, cancellationToken);
                    int effectiveHostSolidCount = hostGeometry.Solids.Count;
                    bool capturePairDetails = coverage.CanAddDiagnosticDetail;
                    NestedGeometry nestedDiagnostic = null;
                    IntersectionData nestedIntersection = null;
                    if (intersection.Volume <= IntersectionToleranceCubicFeet && linkedSolids.Count > 0 &&
                        (hostGeometry.Solids.Count == 0 || !hostGeometry.IsComplete) && hostElement is FamilyInstance hostFamily)
                    {
                        nestedDiagnostic = nestedHostCache.GetOrAdd(hostId, () =>
                        {
                            var found = new NestedGeometry();
                            long started = Stopwatch.GetTimestamp();
                            try { CollectSubcomponentSolids(hostFamily, geometryOptions, Transform.Identity, found.Solids, cancellationToken, true, capturePairDetails ? found.Details : null); }
                            finally { geometryTicks += Stopwatch.GetTimestamp() - started; }
                            return found;
                        });
                        var nestedSolids = nestedDiagnostic.Solids;
                        if (nestedSolids.Count > 0)
                        {
                            effectiveHostSolidCount = Math.Max(effectiveHostSolidCount, nestedSolids.Count);
                            nestedIntersection = GetIntersectionData(linkedSolids, nestedSolids, linkedBoxes,
                                new Dictionary<Solid, Outline>(), cancellationToken, true);
                            if (nestedIntersection.Volume > IntersectionToleranceCubicFeet) intersection = nestedIntersection;
                            else intersection.BooleanFailed |= nestedIntersection.BooleanFailed;
                        }
                    }
                    PhysicalPairOutcome outcome = ConfirmedClashDecision.Classify(intersection.Volume, IntersectionToleranceCubicFeet,
                            linkedSolids.Count, effectiveHostSolidCount, linkedGeometry.IsComplete, hostGeometry.IsComplete && effectiveHostSolidCount == hostGeometry.Solids.Count, intersection.BooleanFailed);
                    if (outcome != PhysicalPairOutcome.Confirmed)
                    {
                        if (outcome == PhysicalPairOutcome.NoIntersection) rejectedNonIntersections++;
                        else
                        {
                            coverage.RecordPair(outcome, linkedElement.Id.IntegerValue, linkedElement.Category?.Name ?? "(sem categoria)",
                                hostId, hostElement.Category?.Name ?? "(sem categoria)",
                                !capturePairDetails ? null : $"sólidos: link {linkedSolids.Count}, hospedeiro {hostGeometry.Solids.Count}, aninhados {nestedDiagnostic?.Solids.Count ?? 0}; " +
                                $"comparações entre sólidos: caixas sobrepostas {intersection.OverlappingBoxes}, booleanas {intersection.BooleanAttempts}, falha {intersection.BooleanFailed}; " +
                                $"busca aninhada: caixas sobrepostas {nestedIntersection?.OverlappingBoxes ?? 0}, booleanas {nestedIntersection?.BooleanAttempts ?? 0}, falha {nestedIntersection?.BooleanFailed ?? false}. " +
                                $"Caixa do primeiro sólido do link: {DescribeSolidBounds(linkedSolids.FirstOrDefault())}. " +
                                $"Subcomponentes: {(nestedDiagnostic == null ? "não consultados" : nestedDiagnostic.Details.Count == 0 ? "nenhum retornado" : string.Join(" | ", nestedDiagnostic.Details))}");
                            if (hostGeometry.HasUnresolvedBounds)
                            {
                                report.UnboundedHostPairCount++;
                                diagnostics?.RecordUnboundedHost(hostId, hostElement.Category?.Name ?? "(sem categoria)");
                                coverage.RecordHostBounds(hostId, hostElement.Category?.Name ?? "(sem categoria)");
                            }
                        }
                        conflictKeys.Remove(key);
                        continue;
                    }
                    XYZ point = intersection.Point ?? GetOutlineCenter(outline);
                    if (diagnostics != null)
                        diagnostics.ReportedConflicts++;
                    else
                    {
                        if (linkedMetadata == null) linkedMetadata = new ElementMetadata(linkedElement);
                        ElementMetadata hostMetadata = metadataCache.GetOrAdd(hostId, () => new ElementMetadata(hostElement));
                        report.Conflicts.Add(CreateConflictItem(linkedElement, hostElement, linkedOwner, hostOwner,
                            linkedMetadata, hostMetadata, intersection.Volume, point, intersection.IsPartial));
                    }
                }
                comparisonTicks += Stopwatch.GetTimestamp() - stageStart - (geometryTicks - geometryBeforeCandidates);
                currentComparisonStart = 0;
                if (!string.IsNullOrEmpty(diagnosticStopReason)) break;
            }

            stopwatch.Stop();
            report.AnalysisDuration = stopwatch.Elapsed;
            report.GeometryDuration = TimeSpan.FromSeconds((double)geometryTicks / Stopwatch.Frequency);
            report.SearchDuration = TimeSpan.FromSeconds((double)searchTicks / Stopwatch.Frequency);
            report.ComparisonDuration = TimeSpan.FromSeconds((double)comparisonTicks / Stopwatch.Frequency);
            report.PrefilteredLinkedElementCount = prefiltered;
            report.EmptyRegionReuses = emptyRegions.EmptyRegionReuses;
            report.EmptyRegionQueries = emptyRegions.RegionQueries;
            report.EmptyRegionSkips = emptyRegions.EmptyRegionSkips;
            report.ExcludedCurveCount = excludedCurveCount;
            report.CurveScanPartial = curveScanPartial;
            report.CurveScanFailed = curveScanFailed;
            report.CurvePreparationDuration = curvePreparation.Elapsed;
            report.ComparedPairCount = pairsChecked;
            report.HasUnverified = coverage.HasIssues;
            report.UnverifiedSummary = coverage.HasIssues ? coverage.Format() : string.Empty;
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostics != null)
            {
                if (string.IsNullOrEmpty(diagnosticStopReason))
                    diagnosticStopReason = linkedIds.Count > analysisLimit
                        ? $"Limite de {LinkConflictDiagnostics.MaxLinkedElements:N0} elementos do link atingido."
                        : "Todos os elementos selecionados foram percorridos.";
                report.DiagnosticText = diagnostics.Format(report.LinkName, report.CategoryName, linkedIds.Count,
                    report.LinkedElementCount, diagnosticStartPosition, pairsChecked, report.AnalysisDuration, report.SearchDuration,
                    report.GeometryDuration, report.ComparisonDuration, diagnosticStopReason,
                    report.EmptyRegionQueries, report.EmptyRegionReuses, report.EmptyRegionSkips,
                    report.ExcludedCurveCount, report.CurveScanPartial, report.CurvePreparationDuration, report.CurveScanFailed) +
                    (coverage.HasIssues ? "\n" + coverage.Format(20) : "");
            }
            progress?.Invoke("Análise concluída.", analysisCount, analysisCount);
            for (int index = 0; index < report.Conflicts.Count; index++)
                report.Conflicts[index].Number = index + 1;

            return report;
        }

        internal sealed class HostGeometry
        {
            public List<Solid> Solids { get; set; }
            public bool IsComplete { get; set; }
            public bool HasUnresolvedBounds { get; set; }
            public int EmptySolidOccurrences { get; set; }
            public List<Outline> Outlines { get; set; }
            public Dictionary<Solid, Outline> SolidBoxes { get; } = new Dictionary<Solid, Outline>();
        }

        private sealed class NestedGeometry
        {
            public List<Solid> Solids { get; } = new List<Solid>();
            public List<string> Details { get; } = new List<string>();
        }

        private sealed class ElementMetadata
        {
            public readonly string Name, Family, Type, Category;
            public ElementMetadata(Element element)
            {
                Name = RevitText.ElementName(element);
                Family = RevitText.FamilyName(element.Document, element);
                Type = RevitText.ElementTypeName(element.Document, element);
                Category = element.Category?.Name ?? string.Empty;
            }
        }

        private static LinkConflictItem CreateConflictItem(Element linkedElement, Element hostElement,
            Element linkedOwner, Element hostOwner, ElementMetadata linked, ElementMetadata host,
            double volume, XYZ markerPoint, bool partialVolume)
        {
            return new LinkConflictItem
            {
                CompletionKey = $"{linkedElement.Category?.Id.IntegerValue}|{linkedOwner.UniqueId}|{hostOwner.UniqueId}",
                LinkedElementId = linkedElement.Id.IntegerValue,
                LinkedElementName = linked.Name,
                LinkedFamily = linked.Family,
                LinkedType = linked.Type,
                LinkedCategory = linked.Category,
                HostElementId = hostElement.Id.IntegerValue,
                HostElementName = host.Name,
                HostFamily = host.Family,
                HostType = host.Type,
                HostCategory = host.Category,
                IntersectionVolumeCubicMeters = volume * 0.028316846592,
                IsPartialVolume = partialVolume,
                MarkerPoint = markerPoint,
                IsApproximate = false
            };
        }

        internal static Element GetTopLevelFamilyInstance(Element element)
        {
            Element current = element;
            while (current is FamilyInstance familyInstance && familyInstance.SuperComponent != null)
                current = familyInstance.SuperComponent;

            return current;
        }

        private static bool IsModelElement(Element element)
        {
            return element != null &&
                   element.Category != null &&
                   element.Category.CategoryType == CategoryType.Model &&
                   !NonPhysicalDefinitionFilter.IsExcluded(element);
        }

        internal static bool IsEligibleHost(Element element)
        {
            if (!IsModelElement(element) || HostCandidateTypeFilter.IsExcluded(element)) return false;
            if (!(element is ImportInstance)) return true;
            if (DwgNameMatcher.IsDwgName(element.Category?.Name) || DwgNameMatcher.IsDwgName(RevitText.ElementName(element))) return false;
            try
            {
                ElementId typeId = element.GetTypeId();
                return !DwgNameMatcher.IsDwgName(element.Document.GetElement(typeId)?.Name);
            }
            catch { return true; } // Não descarta formatos que não puder identificar.
        }

        private static ElementFilter CreateVerifiedCurveFilter(Document hostDocument, CancellationToken cancellationToken,
            out int excludedCount, out bool stoppedEarly, out bool scanFailed)
        {
            excludedCount = 0;
            stoppedEarly = false;
            scanFailed = false;
            try
            {
                // A API usa Invalid/invertido para COLETAR curvas. Nunca usamos esse filtro
                // para excluir diretamente: cada candidato é verificado antes de usar seu ID.
                var stopwatch = Stopwatch.StartNew();
                var collector = new FilteredElementCollector(hostDocument).WhereElementIsNotElementType()
                    .WherePasses(new CurveElementFilter(CurveElementType.Invalid, true));
                List<ElementId> ids = VerifiedCurveCandidates.Collect(collector, 10000, 10000,
                    () => stopwatch.Elapsed >= TimeSpan.FromSeconds(2), cancellationToken, out stoppedEarly);
                if (ids.Count == 0) return null;
                ElementFilter filter = new ExclusionFilter(ids);
                excludedCount = ids.Count;
                return filter;
            }
            catch (OperationCanceledException) { throw; }
            catch { excludedCount = 0; stoppedEarly = false; scanFailed = true; return null; }
        }

        private static ElementFilter CreateInternalCameraFilter(Document hostDocument, CancellationToken cancellationToken)
        {
            try
            {
                var ids = new List<ElementId>();
                var cameras = new FilteredElementCollector(hostDocument)
                    .WhereElementIsNotElementType()
                    .OfCategory(BuiltInCategory.OST_Cameras);
                foreach (Element camera in cameras)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (camera.GetType() == typeof(Element) && HostCandidateTypeFilter.IsExcluded(camera)) ids.Add(camera.Id);
                }
                return ids.Count == 0 ? null : new ExclusionFilter(ids);
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; } // A exclusão por tipo no hospedeiro continua em vigor.
        }

        private static ElementFilter CreateDwgHostFilter(Document hostDocument)
        {
            try
            {
                var categories = new List<ElementId>();
                foreach (Category category in hostDocument.Settings.Categories)
                    if (category != null && DwgNameMatcher.IsDwgName(category.Name)) categories.Add(category.Id);
                if (categories.Count == 0) return null;
                // A ∨ B: mantém tudo que não é ImportInstance ou não pertence às categorias DWG.
                return new LogicalOrFilter(new List<ElementFilter>
                {
                    new ElementClassFilter(typeof(ImportInstance), true),
                    new ElementMulticategoryFilter(categories, true)
                });
            }
            catch { return null; } // O filtro por elemento continua em vigor.
        }

        private sealed class PreliminaryCandidates
        {
            public Outline Bounds { get; set; }
            // Null significa que o limite foi excedido ou a consulta não é reutilizável.
            public List<ElementId> Ids { get; set; }
        }

        private static PreliminaryCandidates GetPreliminaryCandidates(Document hostDocument, Element linkedElement,
            Transform linkTransform, ElementFilter dwgHostFilter, ElementFilter cameraHostFilter, ElementFilter curveHostFilter, EmptyRegionCache emptyRegions,
            Func<RegionBounds, bool?> probeEmptyRegion, CancellationToken cancellationToken)
        {
            const int maxReusableCandidates = 256;
            try
            {
                BoundingBoxXYZ box = linkedElement.get_BoundingBox(null);
                if (box == null) return null;
                Outline outline = LinkBoundingBoxes.CreateOutline(box, linkTransform);
                if (outline == null || outline.IsEmpty) return null;
                XYZ min = outline.MinimumPoint, max = outline.MaximumPoint;
                var bounds = new RegionBounds(min.X, min.Y, min.Z, max.X, max.Y, max.Z);
                if (emptyRegions.CanSkip(bounds, probeEmptyRegion))
                    return new PreliminaryCandidates { Bounds = outline, Ids = new List<ElementId>() };
                var ids = new List<ElementId>();
                var collector = new FilteredElementCollector(hostDocument)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementClassFilter(typeof(RevitLinkInstance), true))
                    .WherePasses(new BoundingBoxIntersectsFilter(outline, BoundingBoxToleranceFeet));
                if (dwgHostFilter != null) collector.WherePasses(dwgHostFilter);
                if (cameraHostFilter != null) collector.WherePasses(cameraHostFilter);
                if (curveHostFilter != null) collector.WherePasses(curveHostFilter);
                using (FilteredElementIdIterator iterator = collector.GetElementIdIterator())
                {
                    while (iterator.MoveNext())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ids.Count == maxReusableCandidates)
                            return new PreliminaryCandidates { Bounds = outline, Ids = null };
                        ids.Add(iterator.Current);
                    }
                }
                return new PreliminaryCandidates { Bounds = outline, Ids = ids };
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        private static List<Element> FindHostCandidates(Document hostDocument, Outline outline,
            List<Outline> parts, bool completeParts, PreliminaryCandidates preliminary, ElementFilter dwgHostFilter, ElementFilter cameraHostFilter, ElementFilter curveHostFilter, CancellationToken cancellationToken)
        {
            // Um filtro mais preciso só pode reutilizar candidatos da caixa anterior se
            // estiver inteiramente contido nela. Em qualquer dúvida, consulta o projeto.
            ElementFilter exactFilter = LinkBoundingBoxes.CreateCandidateFilter(outline, parts, completeParts, BoundingBoxToleranceFeet);
            if (preliminary?.Ids != null && LinkBoundingBoxes.Contains(preliminary.Bounds, outline))
            {
                try
                {
                    var matches = new List<Element>();
                    foreach (ElementId id in preliminary.Ids)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!exactFilter.PassesFilter(hostDocument, id)) continue;
                        Element candidate = hostDocument.GetElement(id);
                        if (IsEligibleHost(candidate)) matches.Add(candidate);
                    }
                    return matches;
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Recorre à busca completa quando o Revit recusa o filtro. */ }
            }
            var collector = new FilteredElementCollector(hostDocument)
                .WhereElementIsNotElementType()
                .WherePasses(exactFilter);
            if (dwgHostFilter != null)
            {
                try { collector.WherePasses(dwgHostFilter); }
                catch { /* O filtro por elemento continua em vigor. */ }
            }
            if (cameraHostFilter != null)
            {
                try { collector.WherePasses(cameraHostFilter); }
                catch { /* A exclusão por tipo no hospedeiro continua em vigor. */ }
            }
            if (curveHostFilter != null)
            {
                try { collector.WherePasses(curveHostFilter); }
                catch { /* A verificação por tipo continua em vigor. */ }
            }
            return collector.Where(IsEligibleHost).ToList();
        }

        internal static HostGeometry ExtractGeometry(Element element, Options options, Transform transform, CancellationToken cancellationToken)
        {
            var result = new HostGeometry { Solids = new List<Solid>(), IsComplete = true };
            GeometryElement geometry = null;
            bool complete = true;
            try
            {
                geometry = element.get_Geometry(options);
                if (geometry == null) complete = false;
                else foreach (GeometryObject geometryObject in geometry)
                    AddSolids(geometryObject, result.Solids, transform, ref complete);
            }
            catch { complete = false; }
            // Alguns componentes aninhados são elementos próprios no documento e não aparecem
            // na geometria da família pai. Evita somar duas vezes a geometria já encontrada.
            if (result.Solids.Count == 0 && element is FamilyInstance family)
            {
                CollectSubcomponentSolids(family, options, transform, result.Solids, cancellationToken);
                // Mesmo com sólidos recuperados, outros componentes podem não estar expostos.
                // Uma interseção positiva confirma o conflito; ausência dela não comprova limpeza.
                if (result.Solids.Count > 0) complete = false;
            }
            result.IsComplete = complete;
            if (result.Solids.Count == 0)
            {
                if (geometry == null)
                {
                    try { geometry = element.get_Geometry(options); }
                    catch { /* Mantém a alternativa aproximada vazia. */ }
                }
                bool unreliable;
                int emptySolids;
                result.Outlines = GetGeometryOutlines(geometry, transform, out unreliable, out emptySolids);
                result.EmptySolidOccurrences = emptySolids;
                if (unreliable || result.Outlines.Count == 0)
                {
                    Outline fallback = TryGetElementOutline(element, transform);
                    if (fallback != null) result.Outlines.Add(fallback);
                    else result.HasUnresolvedBounds = true;
                }
            }
            return result;
        }

        private static void CollectSubcomponentSolids(FamilyInstance root, Options options, Transform transform,
            ICollection<Solid> solids, CancellationToken cancellationToken, bool visitAll = false, List<string> details = null)
        {
            const int maxComponents = 256;
            var visited = new HashSet<ElementId> { root.Id };
            var pending = new Stack<FamilyInstance>();
            pending.Push(root);
            while (pending.Count > 0 && visited.Count <= maxComponents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FamilyInstance parent = pending.Pop();
                ICollection<ElementId> ids;
                try { ids = parent.GetSubComponentIds(); }
                catch { if (details != null && details.Count < 12) details.Add($"#{parent.Id.IntegerValue}: erro ao obter subcomponentes"); continue; }
                if (ids == null) continue;
                foreach (ElementId id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (visited.Count >= maxComponents) { details?.Add("limite de subcomponentes atingido"); return; }
                    if (id == null || !visited.Add(id)) continue;
                    try
                    {
                        Element child = root.Document.GetElement(id);
                        if (child == null) continue;
                        if (visitAll && child is FamilyInstance nestedFamily) pending.Push(nestedFamily);
                        int before = solids.Count;
                        bool childComplete = true;
                        GeometryElement geometry = child.get_Geometry(options);
                        if (geometry != null)
                            foreach (GeometryObject item in geometry)
                                AddSolids(item, solids, transform, ref childComplete);
                        if (details != null && details.Count < 12)
                            details.Add($"#{id.IntegerValue} {child.Category?.Name ?? "(sem categoria)"} {child.Name}: {solids.Count - before} sólido(s), extração {(childComplete ? "completa" : "incompleta")}, caixa do primeiro: {DescribeSolidBounds(solids.Skip(before).FirstOrDefault())}");
                        // Só desce quando a geometria do componente ainda não trouxe sólidos.
                        if (!visitAll && solids.Count == before && child is FamilyInstance childFamily)
                            pending.Push(childFamily);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { if (details != null && details.Count < 12) details.Add($"#{id.IntegerValue}: erro ao ler subcomponente"); }
                }
            }
        }

        private static void AddSolids(GeometryObject geometryObject, ICollection<Solid> solids, Transform transform, ref bool complete)
        {
            if (geometryObject is Solid solid && solid.Volume > IntersectionToleranceCubicFeet)
            {
                try
                {
                    Solid transformed = transform.IsIdentity ? solid : SolidUtils.CreateTransformed(solid, transform);
                    if (transformed == null) complete = false;
                    else if (transformed.Volume > IntersectionToleranceCubicFeet) solids.Add(transformed);
                }
                catch { complete = false; }
                return;
            }
            if (geometryObject is Mesh)
            {
                // A malha não pode ser descartada como se tivesse sido testada por sólidos.
                complete = false;
                return;
            }
            if (geometryObject is GeometryInstance instance)
            {
                int initialCount = solids.Count;
                Transform nestedTransform = instance.Transform == null ? transform : transform.Multiply(instance.Transform);
                try
                {
                    GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                    if (symbolGeometry != null)
                        foreach (GeometryObject nested in symbolGeometry)
                            AddSolids(nested, solids, nestedTransform, ref complete);
                }
                catch { complete = false; }
                if (solids.Count == initialCount)
                {
                    try
                    {
                        GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                        if (instanceGeometry == null) complete = false;
                        else foreach (GeometryObject nested in instanceGeometry)
                            AddSolids(nested, solids, transform, ref complete);
                    }
                    catch { complete = false; }
                }
            }
        }

        private static string DescribeSolidBounds(Solid solid)
        {
            if (solid == null) return "indisponível";
            try
            {
                Outline box = LinkBoundingBoxes.CreateOutline(solid.GetBoundingBox(), Transform.Identity);
                if (box == null) return "indisponível";
                XYZ a = box.MinimumPoint, b = box.MaximumPoint;
                return $"mm min({a.X * 304.8:F0},{a.Y * 304.8:F0},{a.Z * 304.8:F0}) max({b.X * 304.8:F0},{b.Y * 304.8:F0},{b.Z * 304.8:F0})";
            }
            catch { return "indisponível"; }
        }

        private static Outline TryGetElementOutline(Element element, Transform transform)
        {
            try
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                Outline fallback = LinkBoundingBoxes.CreateOutline(box, transform);
                return fallback == null || fallback.IsEmpty ? null : fallback;
            }
            catch { return null; }
        }

        private static List<Outline> GetSolidOutlines(Element element, IEnumerable<Solid> solids,
            Transform transform, out bool unresolved)
        {
            var outlines = new List<Outline>();
            bool invalid = false;
            foreach (Solid solid in solids)
            {
                try
                {
                    // Estes sólidos já foram transformados para o sistema do projeto hospedeiro.
                    Outline part = LinkBoundingBoxes.CreateOutline(solid.GetBoundingBox(), Transform.Identity);
                    if (part == null) invalid = true;
                    else outlines.Add(part);
                }
                catch { invalid = true; }
            }
            unresolved = false;
            if (invalid || outlines.Count == 0)
            {
                Outline fallback = TryGetElementOutline(element, transform);
                if (fallback != null) outlines.Add(fallback);
                else unresolved = true;
            }
            return outlines;
        }

        private static Outline GetCombinedOutline(IEnumerable<Outline> outlines)
        {
            bool found = false;
            XYZ min = null;
            XYZ max = null;

            foreach (Outline outline in outlines)
            {
                if (outline == null)
                    continue;

                min = !found
                    ? outline.MinimumPoint
                    : new XYZ(
                        Math.Min(min.X, outline.MinimumPoint.X),
                        Math.Min(min.Y, outline.MinimumPoint.Y),
                        Math.Min(min.Z, outline.MinimumPoint.Z));
                max = !found
                    ? outline.MaximumPoint
                    : new XYZ(
                        Math.Max(max.X, outline.MaximumPoint.X),
                        Math.Max(max.Y, outline.MaximumPoint.Y),
                        Math.Max(max.Z, outline.MaximumPoint.Z));
                found = true;
            }

            return found && min != null && max != null ? new Outline(min, max) : null;
        }

        private static List<Outline> GetGeometryOutlines(GeometryElement geometry, Transform transform,
            out bool unreliable, out int emptySolids)
        {
            var outlines = new List<Outline>();
            unreliable = false;
            emptySolids = 0;
            try
            {
                if (geometry == null) { unreliable = true; return outlines; }
                foreach (GeometryObject geometryObject in geometry)
                    AddGeometryOutline(geometryObject, outlines, transform, ref unreliable, ref emptySolids);
            }
            catch { unreliable = true; }
            return outlines;
        }

        private static void AddGeometryOutline(GeometryObject geometryObject, ICollection<Outline> outlines,
            Transform transform, ref bool unreliable, ref int emptySolids)
        {
            if (geometryObject is Solid solid)
            {
                if (GeometryOutlinePolicy.IsEmptySolid(solid.Faces.Size, solid.Edges.Size))
                {
                    emptySolids++;
                    return;
                }
                Outline part = LinkBoundingBoxes.CreateOutline(solid.GetBoundingBox(), transform);
                if (part != null) outlines.Add(part);
                else unreliable = true;
                return;
            }
            if (geometryObject is Mesh mesh)
            {
                Outline part = GetMeshOutline(mesh, transform);
                if (part != null) outlines.Add(part);
                else unreliable = true;
                return;
            }
            if (geometryObject is GeometryInstance instance)
            {
                int initialCount = outlines.Count;
                Transform nestedTransform = instance.Transform == null ? transform : transform.Multiply(instance.Transform);
                try
                {
                    GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                    if (symbolGeometry != null)
                        foreach (GeometryObject nested in symbolGeometry)
                            AddGeometryOutline(nested, outlines, nestedTransform, ref unreliable, ref emptySolids);
                }
                catch { unreliable = true; }
                if (outlines.Count == initialCount)
                {
                    try
                    {
                        GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                        if (instanceGeometry == null) unreliable = true;
                        else foreach (GeometryObject nested in instanceGeometry)
                            AddGeometryOutline(nested, outlines, transform, ref unreliable, ref emptySolids);
                    }
                    catch { unreliable = true; }
                }
            }
        }

        private static Outline GetMeshOutline(Mesh mesh, Transform transform)
        {
            bool found = false;
            XYZ min = null;
            XYZ max = null;
            foreach (XYZ vertex in mesh.Vertices)
            {
                XYZ point = transform.OfPoint(vertex);
                min = !found
                    ? point
                    : new XYZ(Math.Min(min.X, point.X), Math.Min(min.Y, point.Y), Math.Min(min.Z, point.Z));
                max = !found
                    ? point
                    : new XYZ(Math.Max(max.X, point.X), Math.Max(max.Y, point.Y), Math.Max(max.Z, point.Z));
                found = true;
            }

            return found && min != null && max != null ? new Outline(min, max) : null;
        }

        private static XYZ GetOutlineCenter(Outline outline)
        {
            return (outline.MinimumPoint + outline.MaximumPoint) * 0.5;
        }

        internal sealed class IntersectionData
        {
            public double Volume { get; set; }
            public XYZ Point { get; set; }
            public bool BooleanFailed { get; set; }
            public bool IsPartial { get; set; }
            public int OverlappingBoxes { get; set; }
            public int BooleanAttempts { get; set; }
        }

        internal static IntersectionData GetIntersectionData(IEnumerable<Solid> linkedSolids, IEnumerable<Solid> hostSolids,
            Dictionary<Solid, Outline> linkedBoxes, Dictionary<Solid, Outline> hostBoxes,
            CancellationToken cancellationToken, bool stopOnFirst = false)
        {
            IntersectionData result = new IntersectionData();
            double weightedX = 0;
            double weightedY = 0;
            double weightedZ = 0;
            foreach (Solid linkedSolid in linkedSolids)
            {
                foreach (Solid hostSolid in hostSolids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!LinkBoundingBoxes.BoundingBoxesOverlap(GetCachedBoundingBox(linkedSolid, linkedBoxes), GetCachedBoundingBox(hostSolid, hostBoxes), BoundingBoxToleranceFeet))
                        continue;

                    result.OverlappingBoxes++;
                    result.BooleanAttempts++;

                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            linkedSolid,
                            hostSolid,
                            BooleanOperationsType.Intersect);
                        if (intersection == null)
                        {
                            result.BooleanFailed = true;
                            continue;
                        }
                        double volume = intersection.Volume;
                        if (double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0)
                        {
                            result.BooleanFailed = true;
                            continue;
                        }
                        if (volume <= IntersectionToleranceCubicFeet)
                            continue;

                        XYZ centroid = GetSolidPoint(intersection);
                        if (centroid != null)
                        {
                            weightedX += centroid.X * volume;
                            weightedY += centroid.Y * volume;
                            weightedZ += centroid.Z * volume;
                        }

                        result.Volume += volume;
                        if (stopOnFirst)
                        {
                            result.Point = centroid;
                            result.IsPartial = true;
                            return result;
                        }
                    }
                    catch
                    {
                        result.BooleanFailed = true;
                    }
                }
            }

            if (result.Volume > IntersectionToleranceCubicFeet)
                result.Point = new XYZ(weightedX / result.Volume, weightedY / result.Volume, weightedZ / result.Volume);

            return result;
        }

        private static XYZ GetSolidPoint(Solid solid)
        {
            try
            {
                return solid.ComputeCentroid();
            }
            catch
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                if (box == null)
                    return null;

                XYZ localCenter = (box.Min + box.Max) * 0.5;
                return (box.Transform ?? Transform.Identity).OfPoint(localCenter);
            }
        }

        private static Outline GetCachedBoundingBox(Solid solid, Dictionary<Solid, Outline> cache)
        {
            if (solid == null) return null;
            if (!cache.TryGetValue(solid, out Outline outline))
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                try { outline = box == null ? null : LinkBoundingBoxes.CreateOutline(box, Transform.Identity); }
                catch { outline = null; }
                if (outline != null && outline.IsEmpty) outline = null;
                cache[solid] = outline;
            }
            return outline;
        }

    }
}
