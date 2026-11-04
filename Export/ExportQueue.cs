using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.Export
{
    public class ExportQueue
    {
        private readonly Queue<ExportJob> _pendingJobs = new Queue<ExportJob>();

        public ExportQueue(IEnumerable<ExportJob> jobs)
        {
            foreach (ExportJob job in jobs)
            {
                _pendingJobs.Enqueue(job);
            }

            Jobs = jobs.ToList();
        }

        public IReadOnlyList<ExportJob> Jobs { get; }

        public int PendingCount => _pendingJobs.Count;

        public bool TryDequeue(out ExportJob? job)
        {
            if (_pendingJobs.Count == 0)
            {
                job = null;
                return false;
            }

            job = _pendingJobs.Dequeue();
            return true;
        }
    }
}
