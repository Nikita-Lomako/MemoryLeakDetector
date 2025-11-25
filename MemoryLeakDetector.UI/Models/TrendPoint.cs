using System;

namespace MemoryLeakDetector.UI.Models
{
    public sealed class TrendPoint
    {
        public TrendPoint(DateTime timestamp, double workingSetMb, double virtualMemoryMb, int handles)
        {
            Timestamp = timestamp;
            WorkingSetMb = workingSetMb;
            VirtualMemoryMb = virtualMemoryMb;
            Handles = handles;
        }

        public DateTime Timestamp { get; }
        public double WorkingSetMb { get; }
        public double VirtualMemoryMb { get; }
        public int Handles { get; }
    }
}

