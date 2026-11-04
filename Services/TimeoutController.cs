using System;
using System.Diagnostics;

namespace RevitFamilyImporter.Services
{
    public class TimeoutController
    {
        public TimeoutController(int timeoutSeconds)
        {
            MaxDuration = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        }

        public TimeSpan MaxDuration { get; }

        public Stopwatch Start()
        {
            return Stopwatch.StartNew();
        }

        public bool HasTimedOut(Stopwatch stopwatch)
        {
            return stopwatch.Elapsed > MaxDuration;
        }
    }
}
