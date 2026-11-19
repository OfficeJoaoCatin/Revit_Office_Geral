using System;
using System.Collections.Generic;

namespace RevitFamilyImporter.Export
{
    internal static class DwgSetupNamePolicy
    {
        internal static string Choose(string sourceName, IEnumerable<string> existingNames)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("Informe o nome do setup DWG de origem.");
            var names = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(sourceName)) return sourceName;
            string copy = sourceName + " - Exportar Lote";
            if (!names.Contains(copy)) return copy;
            for (int i = 2; i < 100000; i++)
            {
                string candidate = copy + " (" + i + ")";
                if (!names.Contains(candidate)) return candidate;
            }
            throw new InvalidOperationException("Não foi possível gerar um nome para a cópia do setup DWG.");
        }
    }
}
