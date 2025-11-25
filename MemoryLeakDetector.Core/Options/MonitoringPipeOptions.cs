namespace MemoryLeakDetector.Core.Options;

public sealed class MonitoringPipeOptions
{
    public string PipeName { get; set; } = "MemoryLeakDetectorPipe";
    public int MaxServerConnections { get; set; } = 4;
    public int ClientConnectTimeoutMs { get; set; } = 5000;
}

