using Orleans;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace TestGrains;

public class TestGrain : Grain, ITestGrain
{
    private readonly ILogger<TestGrain> _logger;
    private static int _activationCounter = 0;
    private int _activationNumber;

    public TestGrain(ILogger<TestGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _activationNumber = Interlocked.Increment(ref _activationCounter);

        _logger.LogInformation(
            "TestGrain {GrainId} activated (activation #{Number})",
            this.GetPrimaryKeyString(),
            _activationNumber);

        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TestGrain {GrainId} deactivating. Reason: {ReasonCode} - {Description}",
            this.GetPrimaryKeyString(),
            reason.ReasonCode,
            reason.Description);

        // Check if being deactivated due to type unloading
        if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
        {
            _logger.LogWarning(
                "TestGrain {GrainId} being unloaded! Saving critical state...",
                this.GetPrimaryKeyString());

            // Simulate quick cleanup
            // In real scenarios: save state, close connections, etc.
        }

        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task<string> SayHello(string name)
    {
        var message = $"Hello, {name}! (from activation #{_activationNumber})";
        _logger.LogInformation("TestGrain {GrainId} says: {Message}",
            this.GetPrimaryKeyString(),
            message);
        return Task.FromResult(message);
    }

    public Task<int> GetActivationCount()
    {
        return Task.FromResult(_activationNumber);
    }

    public async Task DoLongRunningWork(int durationSeconds)
    {
        _logger.LogInformation(
            "TestGrain {GrainId} starting long-running work ({Duration}s)...",
            this.GetPrimaryKeyString(),
            durationSeconds);

        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

        _logger.LogInformation(
            "TestGrain {GrainId} completed long-running work",
            this.GetPrimaryKeyString());
    }
}
