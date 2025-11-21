using Orleans;

namespace Orion.Core;

/// <summary>
/// Grain interface for ping functionality
/// </summary>
public interface IPingGrain : IGrainWithStringKey
{
    /// <summary>
    /// Pings the grain and returns a response
    /// </summary>
    Task<string> PingAsync(string message);

    /// <summary>
    /// Gets the number of times this grain has been pinged
    /// </summary>
    Task<int> GetPingCountAsync();
}
