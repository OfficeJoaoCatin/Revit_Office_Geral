using System;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal enum PhysicalPairOutcome { Confirmed, NoIntersection, MissingLinkedSolid, MissingHostSolid, IncompleteGeometry, BooleanFailure }

    internal static class ConfirmedClashDecision
    {
        // Só um volume finito positivo obtido entre sólidos confirma um conflito físico.
        internal static PhysicalPairOutcome Classify(double volume, double tolerance, int linkedSolids, int hostSolids,
            bool linkedComplete, bool hostComplete, bool booleanFailed)
        {
            if (linkedSolids <= 0) return PhysicalPairOutcome.MissingLinkedSolid;
            if (hostSolids <= 0) return PhysicalPairOutcome.MissingHostSolid;
            if (double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0) return PhysicalPairOutcome.BooleanFailure;
            if (volume > tolerance) return PhysicalPairOutcome.Confirmed;
            if (!linkedComplete || !hostComplete) return PhysicalPairOutcome.IncompleteGeometry;
            if (booleanFailed) return PhysicalPairOutcome.BooleanFailure;
            return PhysicalPairOutcome.NoIntersection;
        }
    }
}
