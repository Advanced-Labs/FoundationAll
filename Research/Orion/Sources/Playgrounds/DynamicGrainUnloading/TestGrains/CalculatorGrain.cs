using Orleans;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace TestGrains;

public class CalculatorGrain : Grain, ICalculatorGrain
{
    private readonly ILogger<CalculatorGrain> _logger;

    public CalculatorGrain(ILogger<CalculatorGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
        {
            _logger.LogWarning(
                "CalculatorGrain {GrainId} being unloaded during type unloading!",
                this.GetPrimaryKey());
        }

        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task<int> Add(int a, int b)
    {
        var result = a + b;
        _logger.LogInformation("Add({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }

    public Task<int> Multiply(int a, int b)
    {
        var result = a * b;
        _logger.LogInformation("Multiply({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }

    public Task<double> Divide(int a, int b)
    {
        if (b == 0)
        {
            _logger.LogError("Division by zero attempted!");
            throw new DivideByZeroException();
        }

        var result = (double)a / b;
        _logger.LogInformation("Divide({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }
}
