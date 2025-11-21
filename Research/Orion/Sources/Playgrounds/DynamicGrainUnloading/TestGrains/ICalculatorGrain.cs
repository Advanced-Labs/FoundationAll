using Orleans;

namespace TestGrains;

public interface ICalculatorGrain : IGrainWithIntegerKey
{
    Task<int> Add(int a, int b);
    Task<int> Multiply(int a, int b);
    Task<double> Divide(int a, int b);
}
