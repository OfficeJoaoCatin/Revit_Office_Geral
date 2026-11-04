using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.IO;
using System.Text.Json;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendSettingsService
    {
        private readonly ILogger _logger;

        public LegendSettingsService(ILogger logger)
        {
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public LegendSettings Load()
        {
            try
            {
                string path = GetPath();
                if (!File.Exists(path))
                    return CreateDefault();

                LegendSettings settings = JsonSerializer.Deserialize<LegendSettings>(File.ReadAllText(path)) ?? CreateDefault();
                settings.Normalize();
                return settings;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel carregar configuracoes da legenda CAD: {ex.Message}");
                return CreateDefault();
            }
        }

        public void Save(LegendSettings settings)
        {
            try
            {
                settings = settings ?? CreateDefault();
                settings.Normalize();
                string path = GetPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(settings));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Nao foi possivel salvar configuracoes da legenda CAD: {ex.Message}");
            }
        }

        private static LegendSettings CreateDefault()
        {
            var settings = new LegendSettings();
            settings.SectionTitles.Add("FORCA");
            settings.SectionTitles.Add("ILUMINACAO");
            settings.Normalize();
            return settings;
        }

        private static string GetPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RevitFamilyImporter");
            return Path.Combine(folder, "legend-library-settings.json");
        }
    }
}


