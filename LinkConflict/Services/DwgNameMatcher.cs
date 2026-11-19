using System;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal static class DwgNameMatcher
    {
        internal static bool IsDwgName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name.Trim().EndsWith(".dwg", StringComparison.OrdinalIgnoreCase);
    }
}
