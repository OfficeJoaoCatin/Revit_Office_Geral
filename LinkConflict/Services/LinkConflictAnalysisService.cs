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
            Action<string, int, int> progress = null, CancellationToken cancellationToken = default)
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

            Transform linkTransform = linkInstance.GetTotalTransform();
            Options geometryOptions = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            HashSet<string> conflictKeys = new HashSet<string>(StringComparer.Ordinal);
            // Limite evita reter indefinidamente a geometria de um projeto grande.
            const int maxCachedHosts = 1024;
            var hostCache = new BoundedLruCache<int, HostGeometry>(maxCachedHosts);
            var stopwatch = Stopwatch.StartNew();
            long nextProgressMilliseconds = 0;
            long pairsChecked = 0;
            void ReportAnalysisProgress(int index)
            {
                if (progress == null || stopwatch.ElapsedMilliseconds < nextProgressMilliseconds) return;
                nextProgressMilliseconds = stopwatch.ElapsedMilliseconds + 1000;
                progress($"Analisando elementos do link: {pairsChecked:N0} pares verificados; {report.Conflicts.Count:N0} avisos", index, linkedIds.Count);
            }

            for (int linkedIndex = 0; linkedIndex < linkedIds.Count; linkedIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportAnalysisProgress(linkedIndex);
                Element linkedElement = linkedDocument.GetElement(linkedIds[linkedIndex]);
                if (!IsModelElement(linkedElement)) continue;
                report.LinkedElementCount++;
                List<Solid> linkedSolids = GetTransformedSolids(linkedElement, geometryOptions, linkTransform);
                var linkedBoxes = new Dictionary<Solid, BoundingBoxXYZ>();
                List<Outline> linkedPartOutlines = linkedSolids.Count > 0
                    ? GetSolidOutlines(linkedSolids)
                    : GetGeometryOutlines(linkedElement, geometryOptions, linkTransform);
                Outline outline = GetCombinedOutline(linkedPartOutlines);
                if (outline == null)
                    continue;

                List<Element> hostCandidates = new FilteredElementCollector(hostDocument)
                    .WhereElementIsNotElementType()
                    .WherePasses(new BoundingBoxIntersectsFilter(outline, BoundingBoxToleranceFeet))
                    .Where(IsModelElement)
                    .Where(element => !(element is RevitLinkInstance))
                    .ToList();

                report.CandidateHostElementCount += hostCandidates.Count;

                foreach (Element hostElement in hostCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportAnalysisProgress(linkedIndex);
                    string key = GetConflictKey(linkedElement, hostElement);
                    if (!conflictKeys.Add(key))
                        continue;
                    int hostId = hostElement.Id.IntegerValue;
                    HostGeometry hostGeometry = hostCache.GetOrAdd(hostId,
                        () => new HostGeometry { Solids = GetSolids(hostElement, geometryOptions) });
                    List<Solid> hostSolids = hostGeometry.Solids;
                    pairsChecked++;
                    IntersectionData intersection = GetIntersectionData(linkedSolids, hostSolids,
                        linkedBoxes, hostGeometry.SolidBoxes, cancellationToken);

                    if (intersection.Volume > IntersectionToleranceCubicFeet)
                    {
                        report.Conflicts.Add(CreateConflictItem(
                            linkedElement,
                            hostElement,
                            intersection.Volume,
                            intersection.Point ?? GetOutlineCenter(outline),
                            false));
                        continue;
                    }

                    if (hostGeometry.Outlines == null)
                        hostGeometry.Outlines = hostSolids.Count > 0
                            ? GetSolidOutlines(hostSolids)
                            : GetGeometryOutlines(hostElement, geometryOptions, Transform.Identity);
                    Outline overlap = FindPartOverlap(linkedPartOutlines, hostGeometry.Outlines);
                    if (overlap != null)
                    {
                        report.Conflicts.Add(CreateConflictItem(
                            linkedElement,
                            hostElement,
                            0,
                            GetOutlineCenter(overlap),
                            true));
                    }
                    else
                    {
                        conflictKeys.Remove(key);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Análise concluída.", linkedIds.Count, linkedIds.Count);
            for (int index = 0; index < report.Conflicts.Count; index++)
                report.Conflicts[index].Number = index + 1;

            return report;
        }

        private sealed class HostGeometry
        {
            public List<Solid> Solids { get; set; }
            public List<Outline> Outlines { get; set; }
            public Dictionary<Solid, BoundingBoxXYZ> SolidBoxes { get; } = new Dictionary<Solid, BoundingBoxXYZ>();
        }

        private static LinkConflictItem CreateConflictItem(
            Element linkedElement,
            Element hostElement,
            double volume,
            XYZ markerPoint,
            bool approximate)
        {
            Element linkedOwner = GetTopLevelFamilyInstance(linkedElement);
            Element hostOwner = GetTopLevelFamilyInstance(hostElement);
            return new LinkConflictItem
            {
                CompletionKey = $"{linkedElement.Category?.Id.IntegerValue}|{linkedOwner.UniqueId}|{hostOwner.UniqueId}",
                LinkedElementId = linkedElement.Id.IntegerValue,
                LinkedElementName = RevitText.ElementName(linkedElement),
                LinkedFamily = RevitText.FamilyName(linkedElement.Document, linkedElement),
                LinkedType = RevitText.ElementTypeName(linkedElement.Document, linkedElement),
                LinkedCategory = linkedElement.Category?.Name ?? string.Empty,
                HostElementId = hostElement.Id.IntegerValue,
                HostElementName = RevitText.ElementName(hostElement),
                HostFamily = RevitText.FamilyName(hostElement.Document, hostElement),
                HostType = RevitText.ElementTypeName(hostElement.Document, hostElement),
                HostCategory = hostElement.Category?.Name ?? string.Empty,
                IntersectionVolumeCubicMeters = volume * 0.028316846592,
                MarkerPoint = markerPoint,
                IsApproximate = approximate
            };
        }

        private static string GetConflictKey(Element linkedElement, Element hostElement)
        {
            Element linkedOwner = GetTopLevelFamilyInstance(linkedElement);
            Element hostOwner = GetTopLevelFamilyInstance(hostElement);
            // Preserve o resultado por categoria quando elementos aninhados compartilham um pai.
            return $"{linkedElement.Category?.Id.IntegerValue}:{linkedOwner.Id.IntegerValue}:{hostOwner.Id.IntegerValue}";
        }

        private static Element GetTopLevelFamilyInstance(Element element)
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
                   element.Category.CategoryType == CategoryType.Model;
        }

        private static List<Solid> GetTransformedSolids(Element element, Options options, Transform transform)
        {
            List<Solid> solids = new List<Solid>();
            try
            {
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return solids;

                foreach (GeometryObject geometryObject in geometry)
                    AddSolids(geometryObject, solids, transform);
            }
            catch
            {
                // Alguns elementos de link não expõem geometria completa; eles são ignorados.
            }

            return solids;
        }

        private static List<Solid> GetSolids(Element element, Options options)
        {
            List<Solid> solids = new List<Solid>();
            try
            {
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return solids;

                foreach (GeometryObject geometryObject in geometry)
                    AddSolids(geometryObject, solids, Transform.Identity);
            }
            catch
            {
                // O teste de interferência deve continuar mesmo quando um candidato falha.
            }

            return solids;
        }

        private static void AddSolids(GeometryObject geometryObject, ICollection<Solid> solids, Transform transform)
        {
            if (geometryObject is Solid solid && solid.Volume > IntersectionToleranceCubicFeet)
            {
                try
                {
                    Solid transformed = transform.IsIdentity ? solid : SolidUtils.CreateTransformed(solid, transform);
                    if (transformed != null && transformed.Volume > IntersectionToleranceCubicFeet)
                        solids.Add(transformed);
                }
                catch
                {
                    // Solids inválidos para booleanas não são conflitos confiáveis.
                }

                return;
            }

            if (geometryObject is GeometryInstance instance)
            {
                int initialCount = solids.Count;
                Transform nestedTransform = instance.Transform == null
                    ? transform
                    : transform.Multiply(instance.Transform);

                try
                {
                    GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                    if (symbolGeometry != null)
                    {
                        foreach (GeometryObject nested in symbolGeometry)
                            AddSolids(nested, solids, nestedTransform);
                    }
                }
                catch
                {
                    // Algumas geometrias DirectShape/IFC nao permitem SymbolGeometry.
                }

                if (solids.Count == initialCount)
                {
                    try
                    {
                        GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                        if (instanceGeometry != null)
                        {
                            foreach (GeometryObject nested in instanceGeometry)
                                AddSolids(nested, solids, transform);
                        }
                    }
                    catch
                    {
                        // O fallback por caixa delimitadora continuara disponivel.
                    }
                }
            }
        }

        private static List<Outline> GetSolidOutlines(IEnumerable<Solid> solids)
        {
            List<Outline> outlines = new List<Outline>();

            foreach (Solid solid in solids)
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                if (box == null)
                    continue;

                bool found = false;
                XYZ min = null;
                XYZ max = null;
                AddBoxPoints(box, Transform.Identity, ref found, ref min, ref max);
                if (found && min != null && max != null)
                    outlines.Add(new Outline(min, max));
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

        private static void AddBoxPoints(BoundingBoxXYZ box, Transform parentTransform, ref bool found, ref XYZ min, ref XYZ max)
        {
            Transform boxTransform = box.Transform ?? Transform.Identity;
            Transform totalTransform = parentTransform.Multiply(boxTransform);
            XYZ[] points =
            {
                new XYZ(box.Min.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Max.Z)
            };

            foreach (XYZ point in points)
            {
                XYZ transformed = totalTransform.OfPoint(point);
                min = !found
                    ? transformed
                    : new XYZ(Math.Min(min.X, transformed.X), Math.Min(min.Y, transformed.Y), Math.Min(min.Z, transformed.Z));
                max = !found
                    ? transformed
                    : new XYZ(Math.Max(max.X, transformed.X), Math.Max(max.Y, transformed.Y), Math.Max(max.Z, transformed.Z));
                found = true;
            }
        }

        private static List<Outline> GetGeometryOutlines(Element element, Options options, Transform transform)
        {
            List<Outline> outlines = new List<Outline>();
            try
            {
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null)
                    return outlines;

                foreach (GeometryObject geometryObject in geometry)
                    AddGeometryOutline(geometryObject, outlines, transform);
            }
            catch
            {
                // Alguns DirectShape/IFC nao expõem todos os objetos geometricos.
            }

            return outlines;
        }

        private static void AddGeometryOutline(GeometryObject geometryObject, ICollection<Outline> outlines, Transform transform)
        {
            if (geometryObject is Solid solid)
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                if (box != null)
                {
                    Outline outline = CreateOutline(box, transform);
                    if (outline != null)
                        outlines.Add(outline);
                }
                return;
            }

            if (geometryObject is Mesh mesh)
            {
                Outline meshOutline = GetMeshOutline(mesh, transform);
                if (meshOutline != null)
                    outlines.Add(meshOutline);
                return;
            }

            if (geometryObject is GeometryInstance instance)
            {
                int initialCount = outlines.Count;
                Transform nestedTransform = instance.Transform == null
                    ? transform
                    : transform.Multiply(instance.Transform);

                try
                {
                    GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                    if (symbolGeometry != null)
                    {
                        foreach (GeometryObject nested in symbolGeometry)
                            AddGeometryOutline(nested, outlines, nestedTransform);
                    }
                }
                catch
                {
                    // Usa o fallback de geometria da instancia abaixo.
                }

                if (outlines.Count == initialCount)
                {
                    try
                    {
                        GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                        if (instanceGeometry != null)
                        {
                            foreach (GeometryObject nested in instanceGeometry)
                                AddGeometryOutline(nested, outlines, transform);
                        }
                    }
                    catch
                    {
                        // Sem caixa por parte, o elemento nao gera aviso aproximado.
                    }
                }
            }
        }

        private static Outline CreateOutline(BoundingBoxXYZ box, Transform parentTransform)
        {
            bool found = false;
            XYZ min = null;
            XYZ max = null;
            AddBoxPoints(box, parentTransform, ref found, ref min, ref max);
            return found && min != null && max != null ? new Outline(min, max) : null;
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

        private static Outline FindPartOverlap(IEnumerable<Outline> firstParts, IEnumerable<Outline> secondParts)
        {
            foreach (Outline first in firstParts)
            {
                if (first == null)
                    continue;

                foreach (Outline second in secondParts)
                {
                    if (second == null)
                        continue;

                    Outline overlap = GetOutlineOverlap(first, second);
                    if (overlap != null)
                        return overlap;
                }
            }

            return null;
        }

        private static Outline GetOutlineOverlap(Outline first, Outline second)
        {
            XYZ min = new XYZ(
                Math.Max(first.MinimumPoint.X, second.MinimumPoint.X),
                Math.Max(first.MinimumPoint.Y, second.MinimumPoint.Y),
                Math.Max(first.MinimumPoint.Z, second.MinimumPoint.Z));
            XYZ max = new XYZ(
                Math.Min(first.MaximumPoint.X, second.MaximumPoint.X),
                Math.Min(first.MaximumPoint.Y, second.MaximumPoint.Y),
                Math.Min(first.MaximumPoint.Z, second.MaximumPoint.Z));

            double x = max.X - min.X;
            double y = max.Y - min.Y;
            double z = max.Z - min.Z;
            return x > BoundingBoxToleranceFeet &&
                   y > BoundingBoxToleranceFeet &&
                   z > BoundingBoxToleranceFeet
                ? new Outline(min, max)
                : null;
        }

        private static XYZ GetOutlineCenter(Outline outline)
        {
            return (outline.MinimumPoint + outline.MaximumPoint) * 0.5;
        }

        private sealed class IntersectionData
        {
            public double Volume { get; set; }
            public XYZ Point { get; set; }
        }

        private static IntersectionData GetIntersectionData(IEnumerable<Solid> linkedSolids, IEnumerable<Solid> hostSolids,
            Dictionary<Solid, BoundingBoxXYZ> linkedBoxes, Dictionary<Solid, BoundingBoxXYZ> hostBoxes,
            CancellationToken cancellationToken)
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
                    if (!BoundingBoxesOverlap(GetCachedBoundingBox(linkedSolid, linkedBoxes), GetCachedBoundingBox(hostSolid, hostBoxes)))
                        continue;

                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            linkedSolid,
                            hostSolid,
                            BooleanOperationsType.Intersect);
                        if (intersection == null || intersection.Volume <= IntersectionToleranceCubicFeet)
                            continue;

                        XYZ centroid = GetSolidPoint(intersection);
                        if (centroid != null)
                        {
                            weightedX += centroid.X * intersection.Volume;
                            weightedY += centroid.Y * intersection.Volume;
                            weightedZ += centroid.Z * intersection.Volume;
                        }

                        result.Volume += intersection.Volume;
                    }
                    catch
                    {
                        // A geometria pode não ser adequada para a operação booleana.
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

        private static BoundingBoxXYZ GetCachedBoundingBox(Solid solid, Dictionary<Solid, BoundingBoxXYZ> cache)
        {
            if (solid == null) return null;
            if (!cache.TryGetValue(solid, out BoundingBoxXYZ box))
            {
                box = solid.GetBoundingBox();
                cache[solid] = box;
            }
            return box;
        }

        private static bool BoundingBoxesOverlap(BoundingBoxXYZ firstBox, BoundingBoxXYZ secondBox)
        {
            if (firstBox == null || secondBox == null)
                return true;

            return firstBox.Min.X <= secondBox.Max.X + BoundingBoxToleranceFeet &&
                   firstBox.Max.X + BoundingBoxToleranceFeet >= secondBox.Min.X &&
                   firstBox.Min.Y <= secondBox.Max.Y + BoundingBoxToleranceFeet &&
                   firstBox.Max.Y + BoundingBoxToleranceFeet >= secondBox.Min.Y &&
                   firstBox.Min.Z <= secondBox.Max.Z + BoundingBoxToleranceFeet &&
                   firstBox.Max.Z + BoundingBoxToleranceFeet >= secondBox.Min.Z;
        }
    }
}
