using Orleans;

namespace Orion.DynamicKvGrains;

/// <summary>
/// Key-value grain interface - dynamically loaded at runtime
/// </summary>
public interface IKvGrain : IGrainWithStringKey
{
    /// <summary>
    /// Sets the value for this grain's key
    /// </summary>
    Task SetAsync(string value);

    /// <summary>
    /// Gets the value for this grain's key
    /// </summary>
    Task<string?> GetAsync();
}
