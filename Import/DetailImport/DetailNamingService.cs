using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class DetailNamingService
    {
        public const string OfficialNameLayer = "NOME_DET";

        private readonly ILogger _logger;

        public DetailNamingService(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public void ApplyNames(IList<DetailRegion> regions, string nameLayer = OfficialNameLayer)
        {
            if (regions == null)
                return;

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DetailRegion region in regions)
            {
                string name = PickName(region, nameLayer);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Detalhe_{region.Id:000}";

                name = MakeUnique(name, used);
                used.Add(name);
                region.Name = name;
            }

            _logger.Info($"Nomes aplicados para {regions.Count} detalhes.");
        }

        private static string PickName(DetailRegion region, string nameLayer)
        {
            DwgEntityInfo officialText = region.Entities
                .Where(e =>
                    e.IsText &&
                    !string.IsNullOrWhiteSpace(e.Text) &&
                    string.Equals(e.Layer, NormalizeLayerName(nameLayer, OfficialNameLayer), StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => Math.Abs(region.Bounds.MaxY - e.Center.Y))
                .ThenBy(e => e.Center.X)
                .FirstOrDefault();

            string officialName = CleanName(officialText?.Text);
            if (!string.IsNullOrWhiteSpace(officialName))
                return officialName;

            DwgEntityInfo text = region.Entities
                .Where(e => e.IsText && !string.IsNullOrWhiteSpace(e.Text))
                .OrderBy(e => Math.Abs(region.Bounds.MaxY - e.Center.Y))
                .ThenBy(e => Math.Abs(region.Center.X - e.Center.X))
                .FirstOrDefault();

            return CleanName(text?.Text);
        }


        public static string NormalizeLayerName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
        public static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string name = Regex.Replace(value, @"[\r\n\t]+", " ");
            name = Regex.Replace(name, @"\s+", " ").Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid.ToString(), string.Empty);

            name = name.Replace("\"", string.Empty)
                       .Replace("'", string.Empty)
                       .Trim();

            return name;
        }

        private static string MakeUnique(string name, HashSet<string> used)
        {
            if (!used.Contains(name))
                return name;

            int i = 2;
            string candidate;
            do
            {
                candidate = $"{name}_{i:00}";
                i++;
            }
            while (used.Contains(candidate));

            return candidate;
        }
    }
}


