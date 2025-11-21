using Microsoft.AspNetCore.Mvc;
using Orleans.Runtime.DynamicGrains;
using Microsoft.Extensions.Logging;
using System.IO;

namespace TestHost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GrainManagementController : ControllerBase
{
    private readonly IDynamicGrainLoader _loader;
    private readonly IDynamicGrainUnloader _unloader;
    private readonly ILogger<GrainManagementController> _logger;

    public GrainManagementController(
        IDynamicGrainLoader loader,
        IDynamicGrainUnloader unloader,
        ILogger<GrainManagementController> logger)
    {
        _loader = loader;
        _unloader = unloader;
        _logger = logger;
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadGrainAssembly([FromBody] LoadGrainRequest request)
    {
        _logger.LogInformation("Loading grain assembly from: {Path}", request.AssemblyPath);

        if (!System.IO.File.Exists(request.AssemblyPath))
        {
            return BadRequest(new { Error = $"Assembly not found: {request.AssemblyPath}" });
        }

        var result = await _loader.LoadGrainAssemblyAsync(request.AssemblyPath);

        if (result.Success)
        {
            return Ok(new
            {
                Success = true,
                AssemblyName = result.Assembly.GetName().Name,
                GrainTypes = result.GrainTypes,
                Duration = result.LoadDuration.TotalMilliseconds
            });
        }
        else
        {
            return BadRequest(new
            {
                Success = false,
                Errors = result.Errors
            });
        }
    }

    [HttpPost("unload")]
    public async Task<IActionResult> UnloadGrainAssembly([FromBody] UnloadGrainRequest request)
    {
        _logger.LogInformation("Unloading grain assembly from: {Path}", request.AssemblyPath);

        var timeout = request.TimeoutSeconds.HasValue
            ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
            : TimeSpan.FromSeconds(30);

        var result = await _unloader.UnloadGrainAssemblyAsync(
            request.AssemblyPath,
            timeout);

        if (result.Success)
        {
            return Ok(new
            {
                Success = true,
                UnloadedTypes = result.UnloadedGrainTypes.Select(t => t.ToString()),
                ActiveGrainsDeactivated = result.ActiveGrainsDeactivated,
                Duration = result.UnloadDuration.TotalMilliseconds,
                MemoryReclaimed = result.MemoryReclaimed,
                DeactivationDetails = new
                {
                    result.DeactivationResult?.TotalGrainsDeactivated,
                    result.DeactivationResult?.ForcedDeactivations,
                    result.DeactivationResult?.DeactivatedPerType
                }
            });
        }
        else
        {
            return BadRequest(new
            {
                Success = false,
                Errors = result.Errors
            });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Running",
            DynamicGrainLoadingEnabled = true,
            Message = "Dynamic grain loading and unloading is active"
        });
    }

    [HttpGet("memory")]
    public IActionResult GetMemoryStats()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryInfo = GC.GetGCMemoryInfo();

        return Ok(new
        {
            TotalMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
            HeapSizeMB = memoryInfo.HeapSizeBytes / 1024.0 / 1024.0,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        });
    }
}

public record LoadGrainRequest(string AssemblyPath);
public record UnloadGrainRequest(string AssemblyPath, int? TimeoutSeconds);
