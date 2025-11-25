using System;
using System.Collections.Generic;
using System.Linq;
using MemoryLeakDetector.UI.Models;

namespace MemoryLeakDetector.UI.Services.Data
{
    public class MockProcessDataProvider : IProcessDataProvider
    {
        private readonly Random _random = new Random();
        private readonly string[] _processNames = new[]
        {
            "chrome.exe", "notepad.exe", "explorer.exe", "devenv.exe",
            "msedge.exe", "winword.exe", "excel.exe", "outlook.exe"
        };

        public IReadOnlyCollection<ProcessSnapshot> GetProcesses()
        {
            var processes = new List<ProcessSnapshot>();

            for (int i = 0; i < 20; i++)
            {
                var name = _processNames[_random.Next(_processNames.Length)];
                var workingSet = 100 + _random.NextDouble() * 2000;
                var baseline = workingSet * (0.8 + _random.NextDouble() * 0.4);
                var isLeakSuspected = _random.Next(10) == 0; // 10% chance

                var trend = GenerateTrendPoints(workingSet);

                processes.Add(new ProcessSnapshot(
                    name: name,
                    processId: 1000 + i,
                    workingSetMb: Math.Round(workingSet, 1),
                    virtualMemoryMb: Math.Round(workingSet * 1.5, 1),
                    handles: 100 + _random.Next(500),
                    baselineMb: Math.Round(baseline, 1),
                    isLeakSuspected: isLeakSuspected,
                    trend: trend
                ));
            }

            return processes;
        }

        private IReadOnlyList<TrendPoint> GenerateTrendPoints(double currentWorkingSet)
        {
            var points = new List<TrendPoint>();
            var now = DateTime.Now;

            for (int i = 10; i >= 0; i--)
            {
                var timestamp = now.AddMinutes(-i * 5);
                var variation = currentWorkingSet * (0.9 + _random.NextDouble() * 0.2);

                points.Add(new TrendPoint(
                    timestamp: timestamp,
                    workingSetMb: Math.Round(variation, 1),
                    virtualMemoryMb: Math.Round(variation * 1.5, 1),
                    handles: 100 + _random.Next(500)
                ));
            }

            return points;
        }
    }
}