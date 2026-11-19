using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;

namespace RevitFamilyImporter.InternalClash
{
    internal sealed class InternalClashItem : INotifyPropertyChanged
    {
        private bool _isCompleted;
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsCompleted
        {
            get => _isCompleted;
            set { if (_isCompleted == value) return; _isCompleted = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted))); }
        }
        public string CompletionKey { get; set; } = "";
        public XYZ MarkerPoint { get; set; }
        public int FirstId { get; set; }
        public int SecondId { get; set; }
        public string FirstCategory { get; set; } = "";
        public string SecondCategory { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string SecondName { get; set; } = "";
    }

    internal sealed class InternalClashReport
    {
        public List<InternalClashItem> Conflicts { get; } = new List<InternalClashItem>();
        public List<string> Unverified { get; } = new List<string>();
        public int ElementCount, PairCount, ConnectedPairs, UnverifiedCount;
        public void RecordUnverified(string entry)
        {
            UnverifiedCount++;
            if (Unverified.Count < 10000) Unverified.Add(entry);
        }
    }

    internal static class InternalClashService
    {
        private const double VolumeTolerance = 1e-8;
        private sealed class Connections
        {
            public HashSet<int> Owners { get; } = new HashSet<int>();
            public bool Failed { get; set; }
        }

        public static InternalClashReport Analyze(Document doc, IList<LinkedCategoryOption> categories,
            Action<string, int, int> progress, CancellationToken token)
        {
            var report = new InternalClashReport();
            var categoryFilter = new ElementMulticategoryFilter(categories.Select(c => c.CategoryId).ToList());
            progress("Coletando elementos das categorias...", 0, 0);
            var all = new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(categoryFilter)
                .Where(LinkConflictAnalysisService.IsEligibleHost).ToList();
            report.ElementCount = all.Count;
            var options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = true, DetailLevel = ViewDetailLevel.Fine };
            var geometry = new BoundedLruCache<int, LinkConflictAnalysisService.HostGeometry>(1024);
            var connections = new BoundedLruCache<int, Connections>(4096);
            var owners = new BoundedLruCache<int, Element>(8192);
            var reportedOwners = new HashSet<(int, int)>();
            foreach (var pair in all.Select((element, index) => new { element, index }))
            {
                token.ThrowIfCancellationRequested();
                var first = pair.element;
                progress($"Analisando conflitos: {report.PairCount:N0} pares; {report.Conflicts.Count:N0} conflitos; {report.ConnectedPairs:N0} conexões ignoradas", pair.index, all.Count);
                Outline bounds;
                try { bounds = LinkBoundingBoxes.CreateOutline(first.get_BoundingBox(null), Transform.Identity); }
                catch { bounds = null; }
                if (bounds == null || bounds.IsEmpty)
                {
                    report.RecordUnverified($"Elemento #{first.Id.IntegerValue} ({first.Category.Name}): sem caixa confiável para pesquisar candidatos.");
                    continue;
                }
                var candidates = new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(categoryFilter)
                    .WherePasses(new BoundingBoxIntersectsFilter(bounds, 1e-6));
                Element firstOwner = owners.GetOrAdd(first.Id.IntegerValue, () => LinkConflictAnalysisService.GetTopLevelFamilyInstance(first));
                foreach (Element second in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    if (second.Id.IntegerValue <= first.Id.IntegerValue || !LinkConflictAnalysisService.IsEligibleHost(second)) continue;
                    Element secondOwner = owners.GetOrAdd(second.Id.IntegerValue, () => LinkConflictAnalysisService.GetTopLevelFamilyInstance(second));
                    if (!InternalClashPairPolicy.ShouldCompare(first.Id.IntegerValue, second.Id.IntegerValue, firstOwner.Id.IntegerValue, secondOwner.Id.IntegerValue)) continue;
                    report.PairCount++;
                    Connections a = connections.GetOrAdd(first.Id.IntegerValue, () => ReadConnections(first));
                    Connections b = connections.GetOrAdd(second.Id.IntegerValue, () => ReadConnections(second));
                    if (InternalClashPairPolicy.IsConnected(first.Id.IntegerValue, second.Id.IntegerValue,
                        firstOwner.Id.IntegerValue, secondOwner.Id.IntegerValue, a.Owners, b.Owners))
                    {
                        report.ConnectedPairs++;
                        continue;
                    }
                    var ga = geometry.GetOrAdd(first.Id.IntegerValue, () => LinkConflictAnalysisService.ExtractGeometry(first, options, Transform.Identity, token));
                    var gb = geometry.GetOrAdd(second.Id.IntegerValue, () => LinkConflictAnalysisService.ExtractGeometry(second, options, Transform.Identity, token));
                    string label = $"#{first.Id.IntegerValue} ({first.Category.Name}) × #{second.Id.IntegerValue} ({second.Category.Name})";
                    if (ga.Solids.Count == 0 || gb.Solids.Count == 0)
                    {
                        report.RecordUnverified($"{label}: um ou ambos os elementos estão sem sólidos utilizáveis.");
                        continue;
                    }
                    var result = LinkConflictAnalysisService.GetIntersectionData(ga.Solids, gb.Solids,
                        ga.SolidBoxes, gb.SolidBoxes, token, true);
                    if (result.Volume > VolumeTolerance)
                    {
                        if (a.Failed || b.Failed)
                            report.RecordUnverified($"{label}: há interseção física, mas não foi possível verificar os conectores.");
                        else
                        {
                            int x = firstOwner.Id.IntegerValue, y = secondOwner.Id.IntegerValue;
                            if (reportedOwners.Add(x < y ? (x, y) : (y, x)))
                                report.Conflicts.Add(new InternalClashItem { FirstId = first.Id.IntegerValue, SecondId = second.Id.IntegerValue,
                                MarkerPoint = result.Point,
                                CompletionKey = InternalClashPairPolicy.CompletionKey(firstOwner.UniqueId, secondOwner.UniqueId),
                                FirstCategory = first.Category.Name, SecondCategory = second.Category.Name, FirstName = first.Name, SecondName = second.Name });
                        }
                    }
                    else if (result.BooleanFailed || !ga.IsComplete || !gb.IsComplete)
                        report.RecordUnverified($"{label}: geometria incompleta ou falha na comparação física.");
                }
            }
            return report;
        }

        private static Connections ReadConnections(Element element)
        {
            var result = new Connections();
            try
            {
                var pending = new Stack<Element>();
                var seen = new HashSet<int>();
                pending.Push(element);
                while (pending.Count > 0)
                {
                    Element current = pending.Pop();
                    if (!seen.Add(current.Id.IntegerValue)) continue;
                    if (seen.Count > 256) { result.Failed = true; break; }
                    if (current is FamilyInstance nested)
                    {
                        foreach (ElementId id in nested.GetSubComponentIds())
                        {
                            Element child = current.Document.GetElement(id);
                            if (child != null) pending.Push(child);
                            else result.Failed = true;
                        }
                    }
                    ConnectorManager manager = current is MEPCurve curve ? curve.ConnectorManager :
                        current is FamilyInstance family ? family.MEPModel?.ConnectorManager : null;
                    if (manager == null) continue;
                    foreach (Connector connector in manager.Connectors)
                    {
                        if (!IsPhysical(connector) || !connector.IsConnected) continue;
                        foreach (Connector other in connector.AllRefs)
                        {
                            if (!IsPhysical(other) || !connector.IsConnectedTo(other) || other.Owner == null) continue;
                            result.Owners.Add(other.Owner.Id.IntegerValue);
                            Element owner = LinkConflictAnalysisService.GetTopLevelFamilyInstance(other.Owner);
                            result.Owners.Add(owner.Id.IntegerValue);
                        }
                    }
                }
            }
            catch { result.Failed = true; }
            return result;
        }

        private static bool IsPhysical(Connector connector) => connector != null &&
            (connector.ConnectorType == ConnectorType.End || connector.ConnectorType == ConnectorType.Curve || connector.ConnectorType == ConnectorType.Physical);
    }
}
