using System;

namespace RevitFamilyImporter.Services
{
    public class Heartbeat
    {
        public DateTime LastBeat { get; private set; } = DateTime.Now;

        public void Beat()
        {
            LastBeat = DateTime.Now;
        }

        public TimeSpan Age => DateTime.Now - LastBeat;
    }
}
