using System.Collections.Generic;
using MemoryLeakDetector.UI.Models;

namespace MemoryLeakDetector.UI.Services.Data
{
    public interface IProcessDataProvider
    {
        IReadOnlyCollection<ProcessSnapshot> GetProcesses();
    }
}

