using Orleans;
using Orleans.Runtime;

namespace Orion.DynamicKvGrains;

/// <summary>
/// Key-value grain implementation with RavenDB persistence
/// This grain is loaded dynamically at runtime via IDynamicGrainLoader
/// </summary>
public class KvGrain : Grain, IKvGrain
{
    private readonly IPersistentState<KvState> _state;

    public KvGrain(
        [PersistentState("kv", "OrionStore")]
        IPersistentState<KvState> state)
    {
        _state = state;
    }

    public async Task SetAsync(string value)
    {
        _state.State.Value = value;
        await _state.WriteStateAsync();
    }

    public Task<string?> GetAsync()
    {
        return Task.FromResult(_state.State.Value);
    }
}
