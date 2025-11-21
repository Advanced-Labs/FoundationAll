using Microsoft.AspNetCore.Mvc;
using Orleans;
using Microsoft.Extensions.Logging;

namespace TestHost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<TestController> _logger;

    public TestController(IGrainFactory grainFactory, ILogger<TestController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    [HttpPost("hello")]
    public async Task<IActionResult> SayHello([FromBody] HelloRequest request)
    {
        try
        {
            // Use reflection to get the grain interface type dynamically
            var grainType = Type.GetType("TestGrains.ITestGrain, TestGrains");
            if (grainType == null)
            {
                return BadRequest(new { Error = "TestGrains assembly not loaded or ITestGrain not found" });
            }

            var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(string) })!
                .MakeGenericMethod(grainType);

            var grain = getGrainMethod.Invoke(_grainFactory, new object[] { request.GrainId });
            if (grain == null)
            {
                return BadRequest(new { Error = "Failed to get grain instance" });
            }

            var sayHelloMethod = grainType.GetMethod("SayHello");
            var resultTask = (Task<string>)sayHelloMethod!.Invoke(grain, new object[] { request.Name })!;
            var result = await resultTask;

            return Ok(new { Result = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling TestGrain");
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("invoke")]
    public async Task<IActionResult> InvokeGrain([FromBody] InvokeRequest request)
    {
        try
        {
            var grainType = Type.GetType("TestGrains.ITestGrain, TestGrains");
            if (grainType == null)
            {
                return BadRequest(new { Error = "TestGrains assembly not loaded" });
            }

            var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(string) })!
                .MakeGenericMethod(grainType);

            var grain = getGrainMethod.Invoke(_grainFactory, new object[] { request.GrainId });

            var sayHelloMethod = grainType.GetMethod("SayHello");
            var resultTask = (Task<string>)sayHelloMethod!.Invoke(grain, new object[] { request.Message })!;
            var result = await resultTask;

            return Ok(new { GrainId = request.GrainId, Result = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking grain");
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("calculator/add")]
    public async Task<IActionResult> Add([FromBody] CalculatorRequest request)
    {
        try
        {
            var grainType = Type.GetType("TestGrains.ICalculatorGrain, TestGrains");
            if (grainType == null)
            {
                return BadRequest(new { Error = "TestGrains assembly not loaded" });
            }

            var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(long) })!
                .MakeGenericMethod(grainType);

            var grain = getGrainMethod.Invoke(_grainFactory, new object[] { 0L });

            var addMethod = grainType.GetMethod("Add");
            var resultTask = (Task<int>)addMethod!.Invoke(grain, new object[] { request.A, request.B })!;
            var result = await resultTask;

            return Ok(new { Operation = "Add", A = request.A, B = request.B, Result = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CalculatorGrain");
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("activate-multiple")]
    public async Task<IActionResult> ActivateMultiple([FromBody] ActivateMultipleRequest request)
    {
        try
        {
            var grainType = Type.GetType("TestGrains.ITestGrain, TestGrains");
            if (grainType == null)
            {
                return BadRequest(new { Error = "TestGrains assembly not loaded" });
            }

            var results = new List<string>();
            for (int i = 1; i <= request.Count; i++)
            {
                var grainId = $"{request.Prefix}-{i}";
                var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(string) })!
                    .MakeGenericMethod(grainType);

                var grain = getGrainMethod.Invoke(_grainFactory, new object[] { grainId });

                var sayHelloMethod = grainType.GetMethod("SayHello");
                var resultTask = (Task<string>)sayHelloMethod!.Invoke(grain, new object[] { $"Activation {i}" })!;
                var result = await resultTask;

                results.Add($"{grainId}: {result}");
            }

            return Ok(new { ActivatedCount = request.Count, Results = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating multiple grains");
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public record HelloRequest(string GrainId, string Name);
public record InvokeRequest(string GrainId, string Message);
public record CalculatorRequest(int A, int B);
public record ActivateMultipleRequest(int Count, string Prefix = "user");
