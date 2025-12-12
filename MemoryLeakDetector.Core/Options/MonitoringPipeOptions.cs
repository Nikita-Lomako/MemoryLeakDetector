namespace MemoryLeakDetector.Core.Options;

// Настройки Named Pipe для IPC между Service и UI
public sealed class MonitoringPipeOptions
{
    public string PipeName { get; set; } = "MemoryLeakDetectorPipe";
    public int MaxServerConnections { get; set; } = 4;
    public int ClientConnectTimeoutMs { get; set; } = 5000;
    public int ReconnectDelayMilliseconds { get; set; } = 2000;
}
