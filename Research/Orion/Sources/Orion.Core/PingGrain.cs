using Orleans;
using Orleans.Runtime;

namespace Orion.Core;

/// <summary>
/// Implementation of the ping grain with RavenDB persistence
/// </summary>
public class PingGrain : Grain, IPingGrain
{
    private readonly IPersistentState<PingState> _state;

    public PingGrain(
        [PersistentState("ping", "OrionStore")]
        IPersistentState<PingState> state)
    {
        _state = state;
    }

    public async Task<string> PingAsync(string message)
    {
        _state.State.PingCount++;
        _state.State.LastMessage = message;
        _state.State.LastPingTime = DateTime.UtcNow;

        await _state.WriteStateAsync();

        return $"Pong! (count: {_state.State.PingCount}, message: '{message}')";
    }

    public Task<int> GetPingCountAsync()
    {
        return Task.FromResult(_state.State.PingCount);
    }
}
