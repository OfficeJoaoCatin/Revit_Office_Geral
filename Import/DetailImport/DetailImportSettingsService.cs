using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class DetailImportSettingsService
    {
        private readonly ILogger _logger;

        public DetailImportSettingsService(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
        }

        public string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RevitFamilyImporter");
                return Path.Combine(folder, "DetailImportSettings.json");
            }
        }

        public ImportSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new ImportSettings();

                string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                var settings = new ImportSettings
                {
                    Margin = ReadDouble(json, "Margin", 0.05),
                    Spacing = ReadDouble(json, "Spacing", 0.03),
                    Scale = ReadDouble(json, "Scale", 1.0),
                    BoundaryLayer = ReadString(json, "BoundaryLayer", BoundaryDetector.DefaultBoundaryLayer),
                    NameLayer = ReadString(json, "NameLayer", DetailNamingService.OfficialNameLayer),
                    Columns = ReadInt(json, "Columns", 3),
                    AutomaticInsertion = ReadBool(json, "AutomaticInsertion", true),
                    AdjustSpacing = ReadBool(json, "AdjustSpacing", true),
                    UniformScale = ReadBool(json, "UniformScale", true),
                    UseGstarCadHighFidelity = ReadBool(json, "UseGstarCadHighFidelity", false),
                    MaxEntitiesPerDetail = ReadInt(json, "MaxEntitiesPerDetail", 10000),
                    LayoutMode = ReadString(json, "LayoutMode", "Grid").Equals("Vertical", StringComparison.OrdinalIgnoreCase)
                        ? ImportLayoutMode.Vertical
                        : ImportLayoutMode.Grid
                };
                settings.Normalize();
                return settings;
            }
            catch (Exception ex)
            {
                _logger.Warn("Falha ao ler configuracoes de importacao: " + ex.Message);
                return new ImportSettings();
            }
        }

        public void Save(ImportSettings settings)
        {
            if (settings == null)
                return;

            settings.Normalize();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string json =
                    "{\n" +
                    $"  \"Margin\": {ToJsonNumber(settings.Margin)},\n" +
                    $"  \"Spacing\": {ToJsonNumber(settings.Spacing)},\n" +
                    $"  \"Scale\": {ToJsonNumber(settings.Scale)},\n" +
                    $"  \"BoundaryLayer\": \"{EscapeJson(settings.BoundaryLayer)}\",\n" +
                    $"  \"NameLayer\": \"{EscapeJson(settings.NameLayer)}\",\n" +
                    $"  \"Columns\": {settings.Columns},\n" +
                    $"  \"AutomaticInsertion\": {ToJsonBool(settings.AutomaticInsertion)},\n" +
                    $"  \"AdjustSpacing\": {ToJsonBool(settings.AdjustSpacing)},\n" +
                    $"  \"UniformScale\": {ToJsonBool(settings.UniformScale)},\n" +
                    $"  \"UseGstarCadHighFidelity\": {ToJsonBool(settings.UseGstarCadHighFidelity)},\n" +
                    $"  \"MaxEntitiesPerDetail\": {settings.MaxEntitiesPerDetail},\n" +
                    $"  \"LayoutMode\": \"{settings.LayoutMode}\"\n" +
                    "}\n";
                File.WriteAllText(SettingsPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.Warn("Falha ao salvar configuracoes de importacao: " + ex.Message);
            }
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ToJsonNumber(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static double ReadDouble(string json, string name, double defaultValue)
        {
            string value = MatchValue(json, name);
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return defaultValue;
        }

        private static int ReadInt(string json, string name, int defaultValue)
        {
            string value = MatchValue(json, name);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;
            return defaultValue;
        }

        private static bool ReadBool(string json, string name, bool defaultValue)
        {
            string value = MatchValue(json, name);
            if (bool.TryParse(value, out bool result))
                return result;
            return defaultValue;
        }

        private static string ReadString(string json, string name, string defaultValue)
        {
            string value = MatchValue(json, name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static string MatchValue(string json, string name)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            Match match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*(\"(?<s>[^\"]*)\"|(?<v>[-+a-zA-Z0-9\\.]+))",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return string.Empty;

            return match.Groups["s"].Success
                ? match.Groups["s"].Value
                : match.Groups["v"].Value;
        }
    }
}


