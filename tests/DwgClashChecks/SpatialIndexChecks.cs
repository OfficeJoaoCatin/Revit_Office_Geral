using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;

internal static class SpatialIndexChecks
{
    public static void Run()
    {
        var random = new Random(4142);
        var regions = new List<DwgRegion>();
        for (int i = 0; i < 180; i++)
        {
            double x = random.NextDouble() * 100 - 50, y = random.NextDouble() * 100 - 50;
            double width = random.NextDouble() * 5 + .01, height = random.NextDouble() * 5 + .01;
            regions.Add(new DwgRegion { IsClosed = i % 2 == 0, Points = new List<Point2D>
                { new Point2D(x, y), new Point2D(x + width, y), new Point2D(x + width, y + height), new Point2D(x, y + height) } });
        }
        regions.Add(new DwgRegion { Points = new List<Point2D> { new Point2D(-1e12, -1e12), new Point2D(1e12, 1e12) } });
        var index = new DwgRegionSpatialIndex(regions);
        var intersection = new IntersectionService();
        for (int i = 0; i < 80; i++)
        {
            double x = random.NextDouble() * 100 - 50, y = random.NextDouble() * 100 - 50;
            var item = Rectangle(x, y, x + 2, y + 3);
            foreach (double tolerance in new[] { 0.0, .01, .5 })
            foreach (DwgClashAnalysisMode mode in Enum.GetValues(typeof(DwgClashAnalysisMode)))
            foreach (DwgClashClosedRegionMode closed in Enum.GetValues(typeof(DwgClashClosedRegionMode)))
            {
                List<int> expected = Enumerable.Range(0, regions.Count)
                    .Where(n => intersection.Matches(item, regions[n], mode, closed, tolerance, out _)).ToList();
                List<int> actual = index.Query(item, tolerance)
                    .Where(n => intersection.Matches(item, regions[n], mode, closed, tolerance, out _)).ToList();
                if (!expected.SequenceEqual(actual)) throw new Exception("O índice alterou os resultados ou sua ordem.");
            }
        }
        var generatedRegions = new DwgRegionBuilder().Build(new[]
        {
            new DwgRegion { Layer = "L", Points = new List<Point2D> { new Point2D(0, 0), new Point2D(2, 0) } },
            new DwgRegion { Layer = "L", Points = new List<Point2D> { new Point2D(100, 100), new Point2D(101, 100) } }
        });
        var input = new List<RevitElementClashInfo> { Rectangle(1, -.5, 1.5, .5) };
        input[0].UniqueId = "elemento-1";
        var settings = new DwgClashSettings { Layer = "L", Mode = DwgClashAnalysisMode.Intersection, ClosedRegionMode = DwgClashClosedRegionMode.BoundaryOnly };
        var generated = new ResultGenerator().Generate(settings, input, generatedRegions);
        if (generated.Results.Count != 1 || generated.Results[0].Number != 1 ||
            string.IsNullOrEmpty(generated.Results[0].CompletionKey) || !string.IsNullOrEmpty(generatedRegions[1].CompletionGeometryKey))
            throw new Exception("A geração indexada ou o cálculo das chaves de memória está incorreto.");
        var huge = Rectangle(-1e15, -1e15, 1e15, 1e15);
        var hugeExpected = Enumerable.Range(0, regions.Count)
            .Where(n => intersection.Matches(huge, regions[n], DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.AreaOverlap, .1, out _)).ToList();
        var hugeActual = index.Query(huge, .1)
            .Where(n => intersection.Matches(huge, regions[n], DwgClashAnalysisMode.Intersection, DwgClashClosedRegionMode.AreaOverlap, .1, out _)).ToList();
        if (!hugeExpected.SequenceEqual(hugeActual)) throw new Exception("A consulta extensa alterou os resultados.");
        var many = new List<DwgRegion>();
        for (int y = 0; y < 100; y++) for (int x = 0; x < 100; x++)
            many.Add(new DwgRegion { Points = new List<Point2D> { new Point2D(x * 10, y * 10), new Point2D(x * 10 + 1, y * 10 + 1) } });
        var manyIndex = new DwgRegionSpatialIndex(many);
        var candidates = manyIndex.Query(Rectangle(501, 501, 502, 502), 0);
        if (candidates.Count >= many.Count / 100) throw new Exception("O índice não reduziu os candidatos do cenário disperso.");
        Console.WriteLine($"Cenário sintético: {many.Count:N0} regiões, {candidates.Count:N0} candidata(s) para um elemento.");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { manyIndex.Query(Rectangle(0, 0, 2, 2), 0, cancelled.Token); throw new Exception("Consulta cancelada continuou."); }
        catch (OperationCanceledException) { }
    }
    private static RevitElementClashInfo Rectangle(double x0, double y0, double x1, double y1) => new RevitElementClashInfo
    {
        MinX = x0, MinY = y0, MaxX = x1, MaxY = y1,
        Center = new Point2D((x0 + x1) / 2, (y0 + y1) / 2),
        Footprint = new List<Point2D> { new Point2D(x0,y0), new Point2D(x1,y0), new Point2D(x1,y1), new Point2D(x0,y1) }
    };
}
