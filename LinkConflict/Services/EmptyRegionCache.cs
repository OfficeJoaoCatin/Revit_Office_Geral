using System;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal readonly struct RegionBounds
    {
        public readonly double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public RegionBounds(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        { MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ; }
        public bool Contains(RegionBounds inner) => MinX <= inner.MinX && MinY <= inner.MinY && MinZ <= inner.MinZ &&
            MaxX >= inner.MaxX && MaxY >= inner.MaxY && MaxZ >= inner.MaxZ;
    }

    // Só permite dispensar uma consulta se a região maior foi consultada e estava vazia.
    // A cache vive apenas durante uma análise no contexto permitido da API do Revit.
    internal sealed class EmptyRegionCache
    {
        private const double CellFeet = 32.0;
        private const double MaxCellMagnitude = 1e12;
        private readonly BoundedLruCache<(long, long, long, long, long, long), CachedRegion> _regions =
            new BoundedLruCache<(long, long, long, long, long, long), CachedRegion>(2048);
        private readonly struct CachedRegion
        {
            public readonly RegionBounds Bounds;
            public readonly bool Empty;
            public CachedRegion(RegionBounds bounds, bool empty) { Bounds = bounds; Empty = empty; }
        }
        private int _eligibleQueries;
        public int RegionQueries { get; private set; }
        public int EmptyRegionReuses { get; private set; }
        public int EmptyRegionSkips { get; private set; }

        public bool CanSkip(RegionBounds query, Func<RegionBounds, bool?> isEmpty)
        {
            if (!TryRegion(query, out var key, out var region)) return false;
            _eligibleQueries++;
            if (_regions.TryGetValue(key, out CachedRegion cached))
            {
                if (!cached.Bounds.Contains(query)) return false;
                if (cached.Empty) { EmptyRegionReuses++; EmptyRegionSkips++; }
                return cached.Empty;
            }
            // Limita o custo extra em modelos sem repetição espacial. Reutilizações continuam habilitadas.
            if (RegionQueries >= 32 + _eligibleQueries / 4) return false;
            RegionQueries++;
            bool? result = isEmpty(region);
            if (!result.HasValue) return false; // Consulta desconhecida jamais dispensa comparação.
            _regions.Set(key, new CachedRegion(region, result.Value));
            if (result.Value) EmptyRegionSkips++;
            return result.Value;
        }

        private static bool TryRegion(RegionBounds box, out (long, long, long, long, long, long) key, out RegionBounds region)
        {
            key = default; region = default;
            if (!Finite(box.MinX) || !Finite(box.MinY) || !Finite(box.MinZ) || !Finite(box.MaxX) || !Finite(box.MaxY) || !Finite(box.MaxZ) ||
                box.MinX > box.MaxX || box.MinY > box.MaxY || box.MinZ > box.MaxZ) return false;
            double ax = Math.Floor(box.MinX / CellFeet), ay = Math.Floor(box.MinY / CellFeet), az = Math.Floor(box.MinZ / CellFeet);
            double bx = Math.Floor(box.MaxX / CellFeet), by = Math.Floor(box.MaxY / CellFeet), bz = Math.Floor(box.MaxZ / CellFeet);
            if (Math.Abs(ax) > MaxCellMagnitude || Math.Abs(ay) > MaxCellMagnitude || Math.Abs(az) > MaxCellMagnitude ||
                Math.Abs(bx) > MaxCellMagnitude || Math.Abs(by) > MaxCellMagnitude || Math.Abs(bz) > MaxCellMagnitude ||
                bx - ax > 1 || by - ay > 1 || bz - az > 1) return false;
            long x0 = (long)ax, y0 = (long)ay, z0 = (long)az, x1 = (long)bx, y1 = (long)by, z1 = (long)bz;
            double magnitude = Math.Max(Math.Max(Math.Max(Math.Abs(box.MinX), Math.Abs(box.MaxX)), Math.Max(Math.Abs(box.MinY), Math.Abs(box.MaxY))), Math.Max(Math.Abs(box.MinZ), Math.Abs(box.MaxZ)));
            double padding = Math.Max(1e-4, magnitude * 1e-12);
            region = new RegionBounds(x0 * CellFeet - padding, y0 * CellFeet - padding, z0 * CellFeet - padding,
                (x1 + 1) * CellFeet + padding, (y1 + 1) * CellFeet + padding, (z1 + 1) * CellFeet + padding);
            if (!region.Contains(box)) return false;
            key = (x0, y0, z0, x1, y1, z1);
            return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
