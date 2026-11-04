using System;
using System.Diagnostics;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Services
{
    public class MemoryMonitor
    {
        public MemoryMonitor(long limitMb)
        {
            LimitMb = Math.Max(512, limitMb);
        }

        public long LimitMb { get; }

        public MemorySnapshot GetSnapshot()
        {
            using Process process = Process.GetCurrentProcess();
            return new MemorySnapshot
            {
                WorkingSetMb = process.WorkingSet64 / 1024 / 1024,
                ManagedHeapMb = GC.GetTotalMemory(false) / 1024 / 1024,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
        }

        public bool IsAboveLimit()
        {
            return GetSnapshot().WorkingSetMb > LimitMb;
        }

        public void CleanupBetweenJobs(Logger logger)
        {
            MemorySnapshot before = GetSnapshot();
            if (before.WorkingSetMb <= LimitMb)
                return;

            logger.LogMessage($"Memoria acima do limite ({before.WorkingSetMb}MB de {LimitMb}MB). Limpando entre exportacoes.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            MemorySnapshot after = GetSnapshot();
            logger.LogMessage($"Memoria apos limpeza: {after.WorkingSetMb}MB, heap gerenciado {after.ManagedHeapMb}MB.");
        }
    }

    public class MemorySnapshot
    {
        public long WorkingSetMb { get; set; }

        public long ManagedHeapMb { get; set; }

        public int Gen0Collections { get; set; }

        public int Gen1Collections { get; set; }

        public int Gen2Collections { get; set; }
    }
}
