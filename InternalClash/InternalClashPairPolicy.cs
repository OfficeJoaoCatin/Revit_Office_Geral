using System.Collections.Generic;

namespace RevitFamilyImporter.InternalClash
{
    internal static class InternalClashPairPolicy
    {
        internal static string CompletionKey(string first, string second) =>
            string.CompareOrdinal(first, second) <= 0 ? first + "|" + second : second + "|" + first;
        internal static bool ShouldCompare(int first, int second, int firstOwner, int secondOwner) =>
            first < second && firstOwner != secondOwner;

        internal static bool IsConnected(int first, int second, int firstOwner, int secondOwner,
            ISet<int> firstConnections, ISet<int> secondConnections) =>
            firstConnections.Contains(second) || firstConnections.Contains(secondOwner) ||
            secondConnections.Contains(first) || secondConnections.Contains(firstOwner);
    }
}
