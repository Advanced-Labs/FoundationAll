using Orleans;

namespace TestGrains;

public interface ITestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Simple echo method to test grain invocation.
    /// </summary>
    Task<string> SayHello(string name);

    /// <summary>
    /// Returns the current activation count to verify state.
    /// </summary>
    Task<int> GetActivationCount();

    /// <summary>
    /// Simulates a long-running operation (for testing deactivation timeout).
    /// </summary>
    Task DoLongRunningWork(int durationSeconds);
}
