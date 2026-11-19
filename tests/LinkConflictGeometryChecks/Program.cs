using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using RevitFamilyImporter.LinkConflict.Services;

class Program
{
    static void Main()
    {
        const double tolerance = 1e-8;
        Check(ConfirmedClashDecision.Classify(0.01, tolerance, 1, 1, true, true, false) == PhysicalPairOutcome.Confirmed, "Volume real positivo deve ser confirmado.");
        Check(ConfirmedClashDecision.Classify(0.01, tolerance, 1, 1, false, false, true) == PhysicalPairOutcome.Confirmed, "Interseção positiva válida deve ser confirmada mesmo com outra parte incompleta.");
        Check(ConfirmedClashDecision.Classify(tolerance, tolerance, 1, 1, true, true, false) == PhysicalPairOutcome.NoIntersection, "Volume no limite não deve ser confirmado.");
        Check(ConfirmedClashDecision.Classify(0, tolerance, 1, 1, true, true, false) == PhysicalPairOutcome.NoIntersection, "Caixas sobrepostas sem volume não devem ser conflito.");
        Check(ConfirmedClashDecision.Classify(0, tolerance, 0, 1, true, true, false) == PhysicalPairOutcome.MissingLinkedSolid, "Link sem sólidos deve ser não verificado.");
        Check(ConfirmedClashDecision.Classify(0, tolerance, 1, 0, true, true, false) == PhysicalPairOutcome.MissingHostSolid, "Hospedeiro sem sólidos deve ser não verificado.");
        Check(ConfirmedClashDecision.Classify(0, tolerance, 1, 1, false, true, false) == PhysicalPairOutcome.IncompleteGeometry, "Geometria parcial não pode ser descartada como sem conflito.");
        Check(ConfirmedClashDecision.Classify(0, tolerance, 1, 1, true, true, true) == PhysicalPairOutcome.BooleanFailure, "Falha booleana deve ser não verificada.");
        Check(ConfirmedClashDecision.Classify(double.NaN, tolerance, 1, 1, true, true, false) == PhysicalPairOutcome.BooleanFailure &&
              ConfirmedClashDecision.Classify(double.PositiveInfinity, tolerance, 1, 1, true, true, false) == PhysicalPairOutcome.BooleanFailure, "Volume inválido não deve ser confirmado.");
        var coverage = new UnverifiedCoverage();
        Check(!coverage.HasIssues, "Cobertura vazia não deve indicar falhas.");
        coverage.RecordLinkedWithoutSolids(10, "Pipes");
        coverage.RecordPair(PhysicalPairOutcome.MissingHostSolid, 11, "Pipes", 20, "Fixtures");
        coverage.RecordPair(PhysicalPairOutcome.BooleanFailure, 12, "Pipes", 21, "Fixtures");
        string coverageSummary = coverage.Format();
        Check(coverage.HasIssues && coverage.LinkedWithoutSolids == 1 && coverage.PairsWithoutHostSolids == 1 && coverage.PairsWithBooleanFailure == 1 &&
              coverageSummary.Contains("Link #10") && coverageSummary.Contains("Link #11") && coverageSummary.Contains("hospedeiro #20") && coverageSummary.Contains("falha na operação"), "Itens não verificados e motivos devem permanecer identificáveis.");
        var largeCoverage = new UnverifiedCoverage();
        for (int i = 0; i < 10001; i++) largeCoverage.RecordLinkedWithoutSolids(i, "Pipes");
        Check(largeCoverage.LinkedWithoutSolids == 10001 && largeCoverage.Format().Contains("lista está limitada"), "Limite da lista deve ser explícito sem perder o contador.");
        var detailedCoverage = new UnverifiedCoverage();
        detailedCoverage.RecordPair(PhysicalPairOutcome.IncompleteGeometry, 1, "Columns", 2, "Fixtures", "caixas sobrepostas 1, falha True");
        Check(detailedCoverage.Format().Contains("caixas sobrepostas 1, falha True"), "Detalhes da comparação incompleta devem aparecer no relatório.");
        for (int i = 0; i < 19; i++) detailedCoverage.RecordLinkedWithoutSolids(i, "Pipes");
        Check(!detailedCoverage.CanAddDiagnosticDetail, "Detalhes adicionais devem ser limitados para preservar desempenho.");
        Check(ClashDecision.GetApproximateReason(1, 1, true, true, false) == ApproximateReason.None, "Sólidos completos sem colisão não devem gerar aviso por caixa.");
        Check(ClashDecision.GetApproximateReason(0, 1, true, true, false) == ApproximateReason.MissingSolid, "Ausência de sólido deve manter aviso aproximado.");
        Check(ClashDecision.GetApproximateReason(1, 0, true, true, false) == ApproximateReason.MissingSolid, "Ausência de sólido no host deve manter aviso aproximado.");
        Check(ClashDecision.GetApproximateReason(1, 1, false, true, false) == ApproximateReason.IncompleteGeometry, "Geometria parcial deve manter aviso aproximado.");
        Check(ClashDecision.GetApproximateReason(1, 1, true, false, false) == ApproximateReason.IncompleteGeometry, "Geometria parcial do host deve manter aviso aproximado.");
        Check(ClashDecision.GetApproximateReason(1, 1, true, true, true) == ApproximateReason.BooleanFailure, "Falha booleana deve manter aviso aproximado.");
        Check(NonPhysicalDefinitionFilter.IsExcluded(new Autodesk.Revit.DB.Plumbing.PipeSegment()), "Definição de segmento de tubo não é elemento físico.");
        Check(NonPhysicalDefinitionFilter.IsExcluded(new MEPSystem()) && NonPhysicalDefinitionFilter.IsExcluded(new PipingSystem()), "Sistemas MEP lógicos não são elementos físicos.");
        Check(!NonPhysicalDefinitionFilter.IsExcluded(new Pipe()) && !NonPhysicalDefinitionFilter.IsExcluded(new Conduit()) && !NonPhysicalDefinitionFilter.IsExcluded(new FamilyInstance()), "Tubos, eletrodutos e famílias devem continuar elegíveis.");
        var rawCandidates = new List<Element>
        {
            new Pipe { Id = new ElementId(1) },
            new CurveElement { Id = new ElementId(2) },
            new Conduit { Id = new ElementId(3) },
            new ModelLine { Id = new ElementId(4) },
            new FamilyInstance { Id = new ElementId(5) }
        };
        var verifiedIds = VerifiedCurveCandidates.Collect(rawCandidates, 10, 10, () => false, CancellationToken.None, out bool partial);
        Check(!partial && verifiedIds.Count == 2 && verifiedIds[0].IntegerValue == 2 && verifiedIds[1].IntegerValue == 4,
            "Exclusão antecipada deve conter somente IDs comprovadamente CurveElement.");
        var partialIds = VerifiedCurveCandidates.Collect(rawCandidates, 2, 10, () => false, CancellationToken.None, out partial);
        Check(partial && partialIds.Count == 1 && partialIds[0].IntegerValue == 2, "Leitura parcial deve manter apenas curvas verificadas.");
        var expiredIds = VerifiedCurveCandidates.Collect(rawCandidates, 10, 10, () => true, CancellationToken.None, out partial);
        Check(partial && expiredIds.Count == 0, "Limite de tempo não deve adicionar elementos não verificados.");
        bool cancellationObserved = false;
        try { VerifiedCurveCandidates.Collect(rawCandidates, 10, 10, () => false, new CancellationToken(true), out _); }
        catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved, "Preparação deve respeitar cancelamento.");
        Check(HostCandidateTypeFilter.IsExcluded(new CurveElement()), "Curva do Revit deve ser excluída.");
        Check(HostCandidateTypeFilter.IsExcluded(new ModelLine()), "Linha derivada de CurveElement deve ser excluída.");
        Check(HostCandidateTypeFilter.IsExcluded(new RevitLinkInstance()), "Outra instância de link deve ser excluída.");
        Check(!HostCandidateTypeFilter.IsExcluded(new Pipe()), "Tubulação deve continuar elegível.");
        Check(!HostCandidateTypeFilter.IsExcluded(new Conduit()), "Eletroduto deve continuar elegível.");
        Check(!HostCandidateTypeFilter.IsExcluded(new FamilyInstance()), "Família deve continuar elegível.");
        var viewCamera = new Element { Category = new Category { Id = new ElementId((int)BuiltInCategory.OST_Cameras) } };
        var physicalFamily = new FamilyInstance { Category = new Category { Id = new ElementId((int)BuiltInCategory.OST_Cameras) } };
        var anotherElement = new Element { Category = new Category { Id = new ElementId(123) } };
        Check(HostCandidateTypeFilter.IsExcluded(viewCamera), "Câmera interna de vista deve ser excluída.");
        Check(!HostCandidateTypeFilter.IsExcluded(physicalFamily), "Família de equipamento deve continuar elegível mesmo na categoria Cameras.");
        Check(!HostCandidateTypeFilter.IsExcluded(anotherElement), "Elementos de outras categorias devem continuar elegíveis.");
        Check(DwgNameMatcher.IsDwgName("AC-TER-TOA.dwg") && DwgNameMatcher.IsDwgName("modelo.DWG "), "DWG não foi identificado.");
        Check(!DwgNameMatcher.IsDwgName("modelo.dxf") && !DwgNameMatcher.IsDwgName("modelo.dwg.dxf") && !DwgNameMatcher.IsDwgName(null), "Formato diferente foi confundido com DWG.");
        Check(LinkConflictDiagnostics.GetSampleRange(20721, 1) == (0, 200), "Amostra inicial incorreta.");
        Check(LinkConflictDiagnostics.GetSampleRange(20721, 3001) == (3000, 3200), "Amostra deslocada incorreta.");
        Check(LinkConflictDiagnostics.GetSampleRange(20721, 20701) == (20700, 20721), "Amostra final incorreta.");
        Check(LinkConflictDiagnostics.GetSampleRange(0, 1) == (0, 0), "Amostra vazia incorreta.");
        bool badStartRejected = false;
        try { LinkConflictDiagnostics.GetSampleRange(20721, 20722); } catch (ArgumentOutOfRangeException) { badStartRejected = true; }
        Check(badStartRejected, "Posição fora da lista deveria ser recusada.");
        var outlinesCache = new SmallOutlineCache<int>();
        int extractions = 0;
        for (int pass = 0; pass < 6; pass++)
            for (int id = 0; id < 3466; id++)
            {
                if (!outlinesCache.TryGetValue(id, out int cached))
                {
                    extractions++;
                    cached = id * 3;
                    Check(outlinesCache.TryStore(id, cached, 1), "Caixa compacta não foi armazenada.");
                }
                Check(cached == id * 3, "Cache retornou conteúdo de outro hospedeiro.");
            }
        Check(extractions == 3466, "Cache compacta não reaproveitou os 3.466 hospedeiros da sequência sintética.");
        Check(!outlinesCache.TryStore(6000, 99, 9) && !outlinesCache.TryGetValue(6000, out _), "Geometria acima do limite não deve ser armazenada.");
        var smallCache = new SmallOutlineCache<int>(2, 2);
        smallCache.TryStore(1, 10, 1); smallCache.TryStore(2, 20, 1); smallCache.TryGetValue(1, out _); smallCache.TryStore(3, 30, 1);
        Check(smallCache.TryGetValue(1, out int reused) && reused == 10 && !smallCache.TryGetValue(2, out _), "Limite ou ordem de uso da cache incorretos.");
        var emptySummary = new LinkConflictDiagnostics().Format("teste", "Pipes", 10, 1, 1, 0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, "teste");
        Check(emptySummary.Contains("Nenhum limite de geometria não resolvido"), "Diagnóstico sem falhas de limites não deve exibir alerta genérico.");
        var spatialCache = new EmptyRegionCache();
        int probeCount = 0;
        var emptyRegionQuery = new RegionBounds(1, 1, 1, 2, 2, 2);
        for (int i = 0; i < 500; i++)
            Check(spatialCache.CanSkip(emptyRegionQuery, region => { probeCount++; return true; }), "Região vazia confirmada deveria ser reaproveitada.");
        Check(probeCount == 1 && spatialCache.EmptyRegionReuses == 499 && spatialCache.EmptyRegionSkips == 500, "Reaproveitamento de região vazia incorreto.");
        var unsafeCache = new EmptyRegionCache();
        Check(!unsafeCache.CanSkip(emptyRegionQuery, region => null), "Falha ou resposta desconhecida não pode dispensar busca.");
        Check(!unsafeCache.CanSkip(emptyRegionQuery, region => false), "Região com candidato não pode ser dispensada.");
        Check(!unsafeCache.CanSkip(new RegionBounds(double.NaN, 0, 0, 1, 1, 1), region => throw new Exception()), "Caixa inválida deve seguir a busca normal.");
        Check(!unsafeCache.CanSkip(new RegionBounds(0, 0, 0, 1000, 1000, 1000), region => throw new Exception()), "Região muito grande deve seguir a busca normal.");
        var exceptionCache = new EmptyRegionCache();
        bool sawProbeException = false;
        try { exceptionCache.CanSkip(emptyRegionQuery, region => throw new InvalidOperationException()); }
        catch (InvalidOperationException) { sawProbeException = true; }
        Check(sawProbeException && !exceptionCache.CanSkip(emptyRegionQuery, region => false), "Falha da consulta não pode armazenar uma região vazia.");
        var negativeBoundaryCache = new EmptyRegionCache();
        var negativeBoundaryHost = new RegionBounds(-32, -2, -2, -31, -1, -1);
        var negativeBoundaryQuery = new RegionBounds(-33, -2, -2, -32, -1, -1);
        Check(!negativeBoundaryCache.CanSkip(negativeBoundaryQuery, region => !Overlaps(region, negativeBoundaryHost)), "Contato no limite negativo não pode ser descartado.");
        var hosts = new List<RegionBounds>();
        var spatialRandom = new Random(72341);
        for (int i = 0; i < 250; i++)
        {
            double x = spatialRandom.NextDouble() * 2000 - 1000, y = spatialRandom.NextDouble() * 2000 - 1000, z = spatialRandom.NextDouble() * 300 - 150;
            hosts.Add(new RegionBounds(x, y, z, x + spatialRandom.NextDouble() * 20, y + spatialRandom.NextDouble() * 20, z + spatialRandom.NextDouble() * 20));
        }
        var randomizedCache = new EmptyRegionCache();
        for (int i = 0; i < 10000; i++)
        {
            double x = spatialRandom.NextDouble() * 2000 - 1000, y = spatialRandom.NextDouble() * 2000 - 1000, z = spatialRandom.NextDouble() * 300 - 150;
            var query = new RegionBounds(x, y, z, x + spatialRandom.NextDouble() * 50, y + spatialRandom.NextDouble() * 50, z + spatialRandom.NextDouble() * 50);
            bool skip = randomizedCache.CanSkip(query, region => !hosts.Exists(host => Overlaps(region, host)));
            Check(!skip || !hosts.Exists(host => Overlaps(query, host)), "Cache descartou um candidato espacial real.");
        }
        var boundaryCache = new EmptyRegionCache();
        var boundaryHost = new RegionBounds(32, 0, 0, 33, 1, 1);
        var boundaryQuery = new RegionBounds(31, 0, 0, 32, 1, 1);
        Check(!boundaryCache.CanSkip(boundaryQuery, region => !Overlaps(region, boundaryHost)), "Contato no limite da célula não pode ser descartado.");
        var diagnosis = new LinkConflictDiagnostics();
        diagnosis.RecordHostRequest(1, true); diagnosis.RecordHostRequest(1, false, true); diagnosis.RecordHostRequest(1, true); diagnosis.RecordHostRequest(2, true);
        diagnosis.RecordEmptySolids(2, true);
        diagnosis.RecordUnboundedLinked(99, "Pipes"); diagnosis.RecordUnboundedLinked(99, "Pipes");
        diagnosis.RecordUnboundedHost(77, "Electrical Fixtures");
        diagnosis.RecordLinkedBounds(10, "Família A", "Tipo A", 10, 5, 2, 1);
        diagnosis.RecordPair(10, "Pipes", "Pipe", 1, "Walls", -2000011, "Wall", true, false);
        diagnosis.RecordPair(11, "Pipes", "Pipe", 2, "Floors", -2000032, "Floor", false, true);
        diagnosis.RecordPair(12, "Fittings", "FamilyInstance", 2, "Floors", -2000032, "Floor", true, true);
        diagnosis.RecordPair(13, "Fittings", "FamilyInstance", 2, "Floors", -2000032, "Floor", false, false);
        Check(diagnosis.UniqueHosts == 2 && diagnosis.HostRequests == 4 && diagnosis.HostExtractions == 3 && diagnosis.RepeatedExtractions == 1 && diagnosis.OutlineCacheHits == 1, "Contagem da cache incorreta.");
        Check(diagnosis.OnlyLinkMissing == 1 && diagnosis.OnlyHostMissing == 1 && diagnosis.BothMissing == 1, "Lados sem sólidos foram contados incorretamente.");
        Check(!LinkConflictDiagnostics.ShouldStop(19999, TimeSpan.FromSeconds(59), out _), "Amostra terminou antes do limite.");
        Check(LinkConflictDiagnostics.ShouldStop(20000, TimeSpan.Zero, out _) && LinkConflictDiagnostics.ShouldStop(0, TimeSpan.FromSeconds(60), out _), "Limites da amostra não foram respeitados.");
        string summary = diagnosis.Format("teste", "Pipes", 1000, 2, 3001, 4, TimeSpan.FromSeconds(2), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, "teste");
        Check(summary.Contains("PARCIAL") && summary.Contains("3.001") && summary.Contains("Link #10") && summary.Contains("ELEMENTOS DO LINK SEM SÓLIDOS") && summary.Contains("Família A") && summary.Contains("caixa combinada") && summary.Contains("Link #99") && summary.Contains("Hospedeiro #77") && summary.Contains("Ocorrências de sólidos vazios ignoradas") && summary.Contains("extrações repetidas do mesmo elemento: 1"), "Resumo não contém dados esperados.");
        Check(GeometryOutlinePolicy.IsEmptySolid(0, 0), "Sólido sem faces e arestas deve ser ignorado.");
        Check(!GeometryOutlinePolicy.IsEmptySolid(1, 0) && !GeometryOutlinePolicy.IsEmptySolid(0, 1), "Geometrias com faces ou arestas não devem ser tratadas como vazias.");
        var reversed = new BoundingBoxXYZ { Min = new XYZ(1e30, 1e30, 1e30), Max = new XYZ(-1e30, -1e30, -1e30), Transform = Transform.Identity };
        Check(!LinkBoundingBoxes.HasValidBounds(reversed) && LinkBoundingBoxes.CreateOutline(reversed, Transform.Identity) == null, "Caixa invertida não pode virar uma caixa gigantesca.");
        bool hadBounds = true;
        XYZ priorMin = new XYZ(1, 2, 3), priorMax = new XYZ(4, 5, 6);
        LinkBoundingBoxes.AddBoxPoints(reversed, Transform.Identity, ref hadBounds, ref priorMin, ref priorMax);
        Check(hadBounds && priorMin.X == 1 && priorMax.X == 4, "Caixa inválida contaminou limites válidos anteriores.");
        var nonfinite = new BoundingBoxXYZ { Min = new XYZ(double.NaN, 0, 0), Max = new XYZ(1, 1, 1), Transform = Transform.Identity };
        Check(!LinkBoundingBoxes.HasValidBounds(nonfinite) && LinkBoundingBoxes.CreateOutline(nonfinite, Transform.Identity) == null, "Limites não finitos devem ser rejeitados.");
        string failedScanSummary = diagnosis.Format("teste", "Pipes", 1000, 2, 3001, 4, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            "teste", 0, 0, 0, 0, false, TimeSpan.Zero, true);
        Check(failedScanSummary.Contains("indisponível; verificação por tipo mantida"), "Falha na preparação deve ser informada, com fallback.");
        var localBox = new BoundingBoxXYZ { Min = new XYZ(0, 0, 0), Max = new XYZ(2, 1, 1), Transform = Transform.RotationTranslation(Math.PI / 2, 10, 0, 0) };
        var rotated = LinkBoundingBoxes.CreateOutline(localBox, Transform.Identity);
        var overlapping = new Outline(new XYZ(9.2, 1.0, 0.2), new XYZ(9.8, 1.8, 0.8));
        var far = new Outline(new XYZ(0, 0, 0), new XYZ(2, 2, 1));
        Check(LinkBoundingBoxes.BoundingBoxesOverlap(rotated, overlapping, 1e-6), "Caixa rotacionada perdeu uma interseção.");
        Check(!LinkBoundingBoxes.BoundingBoxesOverlap(rotated, far, 1e-6), "Caixa local foi usada sem transformação.");
        Check(LinkBoundingBoxes.BoundingBoxesOverlap(null, far, 1e-6), "Caixa ausente deve manter a análise.");
        Check(LinkBoundingBoxes.Contains(rotated, overlapping), "Contenção de caixa válida não foi reconhecida.");
        Check(!LinkBoundingBoxes.Contains(rotated, far), "Caixa fora da triagem seria reutilizada incorretamente.");
        Check(!LinkBoundingBoxes.Contains(null, far), "Triagem ausente deve usar a busca completa.");
        var firstPart = new Outline(new XYZ(0, 0, 0), new XYZ(1, 1, 1));
        var secondPart = new Outline(new XYZ(9, 0, 0), new XYZ(10, 1, 1));
        var combined = new Outline(new XYZ(0, 0, 0), new XYZ(10, 1, 1));
        var middle = new Outline(new XYZ(4, 0, 0), new XYZ(5, 1, 1));
        var partFilter = LinkBoundingBoxes.CreateCandidateFilter(combined, new List<Outline> { firstPart, secondPart }, true, 1e-6);
        Check(partFilter.Passes(firstPart) && partFilter.Passes(secondPart) && !partFilter.Passes(middle), "Filtro por partes retornou candidatos incorretos.");
        var fallback = LinkBoundingBoxes.CreateCandidateFilter(combined, new List<Outline> { firstPart, secondPart }, false, 1e-6);
        Check(fallback.Passes(middle), "Caixas incompletas devem usar filtro combinado.");
        var tooMany = new List<Outline>();
        for (int i = 0; i < 17; i++) tooMany.Add(firstPart);
        Check(LinkBoundingBoxes.CreateCandidateFilter(combined, tooMany, true, 1e-6).Passes(middle), "Muitas partes devem usar filtro combinado.");
        Check(LinkBoundingBoxes.CreateCandidateFilter(combined, new List<Outline> { firstPart, null }, true, 1e-6).Passes(middle), "Parte ausente deve usar filtro combinado.");
        for (int i = 0; i < 1000; i++)
        {
            double x = i / 100.0;
            var hostBox = new Outline(new XYZ(x, 0.1, 0.1), new XYZ(x + 0.02, 0.9, 0.9));
            bool shouldMatch = LinkBoundingBoxes.BoundingBoxesOverlap(firstPart, hostBox, 1e-6) || LinkBoundingBoxes.BoundingBoxesOverlap(secondPart, hostBox, 1e-6);
            Check(partFilter.Passes(hostBox) == shouldMatch, "Filtro por partes perdeu ou acrescentou candidato.");
        }
        var parent = Transform.RotationTranslation(Math.PI / 3, -4, 7, 2);
        var random = new Random(8714);
        for (int i = 0; i < 1000; i++)
        {
            var box = new BoundingBoxXYZ { Min = new XYZ(-2, -3, -1), Max = new XYZ(4, 2, 3), Transform = Transform.RotationTranslation(random.NextDouble() * 6.28, random.NextDouble() * 30, random.NextDouble() * 30, random.NextDouble() * 10) };
            var outline = LinkBoundingBoxes.CreateOutline(box, parent);
            var transform = parent.Multiply(box.Transform);
            foreach (double x in new[] { box.Min.X, box.Max.X }) foreach (double y in new[] { box.Min.Y, box.Max.Y }) foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                var pt = transform.OfPoint(new XYZ(x,y,z));
                Check(pt.X >= outline.MinimumPoint.X - 1e-9 && pt.X <= outline.MaximumPoint.X + 1e-9 && pt.Y >= outline.MinimumPoint.Y - 1e-9 && pt.Y <= outline.MaximumPoint.Y + 1e-9 && pt.Z >= outline.MinimumPoint.Z - 1e-9 && pt.Z <= outline.MaximumPoint.Z + 1e-9, "Caixa não contém todos os cantos transformados.");
            }
        }
        Console.WriteLine("Caixas, colisões em limites, caches e diagnóstico verificados.");
    }
    static bool Overlaps(RegionBounds a, RegionBounds b)
    {
        const double tolerance = 1e-6;
        return a.MinX <= b.MaxX + tolerance && a.MaxX + tolerance >= b.MinX &&
               a.MinY <= b.MaxY + tolerance && a.MaxY + tolerance >= b.MinY &&
               a.MinZ <= b.MaxZ + tolerance && a.MaxZ + tolerance >= b.MinZ;
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory { OST_Cameras = -2000500 }
    public class ElementId { public int IntegerValue; public ElementId(int value) { IntegerValue = value; } }
    public class Category { public ElementId Id; }
    public class Element { public Category Category; public ElementId Id; }
    public class CurveElement : Element { }
    public class ModelLine : CurveElement { }
    public class RevitLinkInstance : Element { }
    public class MEPSystem : Element { }
    public class PipingSystem : MEPSystem { }
    public class Pipe : Element { }
    public class Conduit : Element { }
    public class FamilyInstance : Element { }
    public class XYZ
    {
        public double X, Y, Z;
        public XYZ(double x, double y, double z) { X=x; Y=y; Z=z; }
    }
    public class Outline
    {
        public XYZ MinimumPoint, MaximumPoint;
        public Outline(XYZ min, XYZ max) { MinimumPoint=min; MaximumPoint=max; }
        public bool IsEmpty => false;
    }
    public abstract class ElementFilter { public abstract bool Passes(Outline other); }
    public sealed class BoundingBoxIntersectsFilter : ElementFilter
    {
        private readonly Outline bounds; private readonly double tolerance;
        public BoundingBoxIntersectsFilter(Outline bounds, double tolerance) { this.bounds=bounds; this.tolerance=tolerance; }
        public override bool Passes(Outline other) => LinkBoundingBoxes.BoundingBoxesOverlap(bounds, other, tolerance);
    }
    public sealed class LogicalOrFilter : ElementFilter
    {
        private readonly List<ElementFilter> filters;
        public LogicalOrFilter(List<ElementFilter> filters) { this.filters=filters; }
        public override bool Passes(Outline other) { foreach (var filter in filters) if (filter.Passes(other)) return true; return false; }
    }
    public class BoundingBoxXYZ { public XYZ Min, Max; public Transform Transform; }
    public class Transform
    {
        private readonly double c, s, x, y, z;
        private Transform(double c, double s, double x, double y, double z) { this.c=c; this.s=s; this.x=x; this.y=y; this.z=z; }
        public static Transform Identity => new Transform(1,0,0,0,0);
        public static Transform RotationTranslation(double angle, double x, double y, double z) => new Transform(Math.Cos(angle), Math.Sin(angle), x, y, z);
        public XYZ OfPoint(XYZ p) => new XYZ(c*p.X-s*p.Y+x,s*p.X+c*p.Y+y,p.Z+z);
        public Transform Multiply(Transform other) => new Transform(c*other.c-s*other.s,s*other.c+c*other.s,c*other.x-s*other.y+x,s*other.x+c*other.y+y,z+other.z);
    }
}

namespace Autodesk.Revit.DB.Plumbing
{
    public class PipeSegment : Autodesk.Revit.DB.Element { }
}
