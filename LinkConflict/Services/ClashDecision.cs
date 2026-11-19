namespace RevitFamilyImporter.LinkConflict.Services
{
    internal enum ApproximateReason { None, MissingSolid, IncompleteGeometry, BooleanFailure }

    internal static class ClashDecision
    {
        // Um resultado negativo só é definitivo quando todas as partes foram verificadas.
        internal static ApproximateReason GetApproximateReason(int linkedSolidCount, int hostSolidCount,
            bool linkedComplete, bool hostComplete, bool booleanFailed)
        {
            if (linkedSolidCount == 0 || hostSolidCount == 0) return ApproximateReason.MissingSolid;
            if (!linkedComplete || !hostComplete) return ApproximateReason.IncompleteGeometry;
            if (booleanFailed) return ApproximateReason.BooleanFailure;
            return ApproximateReason.None;
        }
    }
}
