using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal static class VerifiedCurveCandidates
    {
        // Só adiciona IDs cujo tipo foi verificado; resultados parciais são seguros.
        internal static List<ElementId> Collect(IEnumerable<Element> candidates, int maxVisited, int maxIds,
            Func<bool> timeExceeded, CancellationToken cancellationToken, out bool stoppedEarly)
        {
            if (maxVisited <= 0) throw new ArgumentOutOfRangeException(nameof(maxVisited));
            if (maxIds <= 0) throw new ArgumentOutOfRangeException(nameof(maxIds));
            if (timeExceeded == null) throw new ArgumentNullException(nameof(timeExceeded));
            var ids = new List<ElementId>();
            int visited = 0;
            stoppedEarly = false;
            foreach (Element element in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (visited >= maxVisited || ids.Count >= maxIds || timeExceeded())
                {
                    stoppedEarly = true;
                    break;
                }
                visited++;
                if (element is CurveElement && element.Id != null) ids.Add(element.Id);
            }
            return ids;
        }
    }
}
