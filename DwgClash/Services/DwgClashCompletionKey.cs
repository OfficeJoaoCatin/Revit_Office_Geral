using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    internal static class DwgClashCompletionKey
    {
        // Usa segmentos sem direção e ordenados: a ordem de leitura do DWG não altera a chave.
        public static string ForRegion(DwgRegion region, CancellationToken cancellationToken = default)
        {
            if (region == null || region.Points.Count < 2)
                throw new ArgumentException("A região do DWG não possui geometria válida.", nameof(region));
            var segments = new List<string>();
            int count = region.IsClosed ? region.Points.Count : region.Points.Count - 1;
            for (int i = 0; i < count; i++)
            {
                if (i % 256 == 0) cancellationToken.ThrowIfCancellationRequested();
                string first = PointKey(region.Points[i]);
                string second = PointKey(region.Points[(i + 1) % region.Points.Count]);
                segments.Add(string.CompareOrdinal(first, second) <= 0 ? first + ">" + second : second + ">" + first);
            }
            segments.Sort(StringComparer.Ordinal);
            string layer = (region.Layer ?? string.Empty).Trim().ToUpperInvariant();
            string geometry = layer.Length.ToString(CultureInfo.InvariantCulture) + ":" + layer + "|" +
                (region.IsClosed ? "1" : "0") + "|" + string.Join("|", segments);
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(geometry)).Select(b => b.ToString("x2")));
        }

        private static string PointKey(Point2D point)
        {
            // A precisão de 10^-6 pés evita diferenças insignificantes de arredondamento.
            double x = Math.Round(point.X, 6, MidpointRounding.AwayFromZero);
            double y = Math.Round(point.Y, 6, MidpointRounding.AwayFromZero);
            if (x == 0) x = 0;
            if (y == 0) y = 0;
            return x.ToString("F6", CultureInfo.InvariantCulture) + "," + y.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}
