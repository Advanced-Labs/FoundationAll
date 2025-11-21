namespace Orion.Core;

/// <summary>
/// State for the ping grain
/// </summary>
public class PingState
{
    public int PingCount { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastPingTime { get; set; }
}
