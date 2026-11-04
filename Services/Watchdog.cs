using System;

namespace RevitFamilyImporter.Services
{
    public class Watchdog
    {
        private readonly TimeSpan _staleThreshold;

        public Watchdog(TimeSpan? staleThreshold = null)
        {
            _staleThreshold = staleThreshold ?? TimeSpan.FromSeconds(30);
        }

        public bool IsStale(Heartbeat heartbeat)
        {
            return heartbeat.Age > _staleThreshold;
        }

        public string BuildWarning(Heartbeat heartbeat)
        {
            return $"Heartbeat sem atualizacao ha {heartbeat.Age.TotalSeconds:F0}s.";
        }
    }
}
