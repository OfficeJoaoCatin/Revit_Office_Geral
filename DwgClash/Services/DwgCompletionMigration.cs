using System;
using System.Linq;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.LinkConflict.Services;

namespace RevitFamilyImporter.DwgClash.Services
{
    internal static class DwgCompletionMigration
    {
        internal static bool IsCompleted(LinkConflictCompletionStore store, DwgClashResult result)
        {
            string key = result.CompletionKey;
            bool done = store.IsCompleted(key);
            string migrationKey = key + "|agrupamento-aninhados-v1";
            if (!store.IsCompleted(migrationKey) && result.PreviousCompletionKeys.Any(oldKey =>
                !string.Equals(oldKey, key, StringComparison.Ordinal) && store.IsCompleted(oldKey)))
            {
                if (!done) { store.SetCompleted(key, true); done = true; }
                store.SetCompleted(migrationKey, true);
            }
            return done;
        }
    }
}
