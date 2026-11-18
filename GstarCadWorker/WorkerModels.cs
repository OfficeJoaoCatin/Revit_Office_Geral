using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RevitFamilyImporter.Models.DetailImport;

namespace RevitFamilyImporter.Models.DetailImport
{
    public sealed class DwgDocumentModel { public string FilePath { get; set; } }
    public sealed class DetailRegion { public int Id { get; set; } public string Name { get; set; } public DwgBounds Bounds { get; set; } }
}
namespace RevitFamilyImporter.Import.DetailImport
{
    internal static class DetailNamingService
    {
        public static string CleanName(string value)
        {
            string name = Regex.Replace(value ?? "", @"[\r\n\t]+", " ");
            name = Regex.Replace(name, @"\s+", " ").Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "");
            return name.Replace("\"", "").Replace("'", "").Trim();
        }
    }
}
