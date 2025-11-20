# Module Lifecycle Pattern - Meta-Platform Illustrations

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Plugin Architecture, Reflection Discovery, Resource Management

---

## ⚠️ IMPORTANT DISCLAIMER

**This document contains ILLUSTRATIVE EXAMPLES ONLY.**

The code examples in this document are:
- **Conceptual demonstrations** of how module lifecycle could work across different language runtimes
- **NOT production-ready implementations**
- **NOT tested or validated**
- **NOT official VISORA components**
- **Intended to illustrate patterns**, not provide working code

VISORA is a .NET/C# platform. These examples show how the four-phase lifecycle pattern might be adapted to manage Python and Node.js runtimes in a conceptual meta-platform scenario. They are provided for educational and architectural discussion purposes only.

**Do NOT use these examples in production without:**
1. Thorough testing and validation
2. Security review
3. Performance profiling
4. Error handling implementation
5. Proper integration with your specific runtime

---

## Table of Contents

1. [Cross-Runtime Lifecycle Challenges](#cross-runtime-lifecycle-challenges)
2. [Python Runtime Lifecycle Illustration](#python-runtime-lifecycle-illustration)
3. [Node.js Runtime Lifecycle Illustration](#nodejs-runtime-lifecycle-illustration)
4. [Cross-Runtime Coordination](#cross-runtime-coordination)
5. [State Management Across Runtimes](#state-management-across-runtimes)
6. [Hot-Swap Across Runtimes](#hot-swap-across-runtimes)
7. [Testing Cross-Runtime Lifecycle](#testing-cross-runtime-lifecycle)
8. [Architectural Patterns](#architectural-patterns)

---

## Cross-Runtime Lifecycle Challenges

### The Multi-Runtime Problem

When VISORA hosts modules in different language runtimes, lifecycle management becomes more complex:

```
┌───────────────────────────────────────────────────────────────┐
│                     VISORA Host (.NET)                        │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │         .NET Module Lifecycle                           │  │
│  │  Load → Initialize → Shutdown → Dispose                 │  │
│  │  - In-process                                           │  │
│  │  - AssemblyLoadContext                                  │  │
│  │  - Direct method calls                                  │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │         Python Module Lifecycle                         │  │
│  │  Start Runtime → Import → Initialize → Shutdown →       │  │
│  │  Cleanup → Stop Runtime                                 │  │
│  │  - Separate process OR embedded interpreter             │  │
│  │  - IPC or FFI                                           │  │
│  │  - Marshal data across boundary                         │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │         Node.js Module Lifecycle                        │  │
│  │  Start Runtime → Require → Initialize → Shutdown →      │  │
│  │  Cleanup → Stop Runtime                                 │  │
│  │  - Separate process OR embedded engine                  │  │
│  │  - IPC or native bindings                              │  │
│  │  - Event loop management                                │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
```

### Key Challenges

1. **Runtime Initialization**
   - .NET: No separate runtime (already running)
   - Python: Must start interpreter (process or embedded)
   - Node.js: Must start V8 engine and event loop

2. **Lifecycle Coordination**
   - Different async models (.NET Task, Python asyncio, Node.js Promise)
   - Different initialization patterns
   - Need to coordinate across process boundaries

3. **State Management**
   - .NET: In-process state
   - Python: Separate process state or embedded state
   - Node.js: Separate process state or embedded state
   - How to save/restore during hot-swap?

4. **Resource Cleanup**
   - .NET: GC + Dispose pattern
   - Python: GC + context managers
   - Node.js: GC + cleanup callbacks
   - Must ensure all runtimes clean up properly

---

## Python Runtime Lifecycle Illustration

### Conceptual Approach

**Two Options:**
1. **Subprocess:** Python runs in separate process, communicate via IPC
2. **Embedded:** Python embedded via Python.NET, runs in-process

### Illustrative Example: Subprocess Python Lifecycle

```csharp
// ==========================================
// ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
// ==========================================

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ILLUSTRATIVE: Manages lifecycle of Python module in subprocess
/// </summary>
public class PythonModuleHandle : IAsyncDisposable
{
    private readonly string _modulePath;
    private readonly PythonRuntimeOptions _options;
    private Process? _pythonProcess;
    private StreamWriter? _processInput;
    private StreamReader? _processOutput;
    private bool _initialized;

    public PythonModuleHandle(string modulePath, PythonRuntimeOptions options)
    {
        _modulePath = modulePath;
        _options = options;
    }

    /// <summary>
    /// PHASE 1: LOAD - Start Python process and import module
    /// </summary>
    public static async Task<PythonModuleHandle> LoadAsync(
        string modulePath,
        PythonRuntimeOptions options,
        CancellationToken ct)
    {
        var handle = new PythonModuleHandle(modulePath, options);

        // Start Python process
        var psi = new ProcessStartInfo
        {
            FileName = options.PythonExecutable,
            Arguments = "-u",  // Unbuffered output
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        handle._pythonProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Python process");

        handle._processInput = handle._pythonProcess.StandardInput;
        handle._processOutput = handle._pythonProcess.StandardOutput;

        // Send import command
        var importCommand = new
        {
            command = "import_module",
            module_path = modulePath
        };

        await handle.SendCommandAsync(importCommand, ct);
        var response = await handle.ReceiveResponseAsync(ct);

        if (response?.Status != "success")
        {
            await handle.DisposeAsync();
            throw new InvalidOperationException(
                $"Failed to import Python module: {response?.Error}");
        }

        return handle;
    }

    /// <summary>
    /// PHASE 2: INITIALIZE - Call module's initialize method
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;  // Idempotent

        // Send initialize command
        var initCommand = new
        {
            command = "initialize",
            context = new
            {
                capabilities = _options.Capabilities,
                properties = _options.Properties
            }
        };

        await SendCommandAsync(initCommand, ct);
        var response = await ReceiveResponseAsync(ct);

        if (response?.Status != "success")
        {
            throw new InvalidOperationException(
                $"Python module initialization failed: {response?.Error}");
        }

        _initialized = true;
    }

    /// <summary>
    /// PHASE 3: SHUTDOWN - Call module's shutdown method
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (!_initialized)
            return;  // Idempotent

        try
        {
            // Send shutdown command
            var shutdownCommand = new { command = "shutdown" };
            await SendCommandAsync(shutdownCommand, ct);
            var response = await ReceiveResponseAsync(ct);

            if (response?.Status != "success")
            {
                Console.WriteLine($"Python shutdown warning: {response?.Error}");
                // Log but don't throw
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Python shutdown error: {ex.Message}");
            // Log but don't throw
        }
        finally
        {
            _initialized = false;
        }
    }

    /// <summary>
    /// PHASE 4: DISPOSE - Stop Python process
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Ensure shutdown
            await ShutdownAsync();

            // Send exit command
            if (_processInput != null && _pythonProcess != null && !_pythonProcess.HasExited)
            {
                await _processInput.WriteLineAsync("exit");
                await _processInput.FlushAsync();

                // Wait for graceful exit (with timeout)
                var exitTask = _pythonProcess.WaitForExitAsync();
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
                {
                    // Timeout - force kill
                    _pythonProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Python disposal error: {ex.Message}");
        }
        finally
        {
            _processInput?.Dispose();
            _processOutput?.Dispose();
            _pythonProcess?.Dispose();
        }
    }

    private async Task SendCommandAsync(object command, CancellationToken ct)
    {
        if (_processInput == null)
            throw new InvalidOperationException("Process not started");

        var json = JsonSerializer.Serialize(command);
        await _processInput.WriteLineAsync(json);
        await _processInput.FlushAsync();
    }

    private async Task<PythonResponse?> ReceiveResponseAsync(CancellationToken ct)
    {
        if (_processOutput == null)
            throw new InvalidOperationException("Process not started");

        var line = await _processOutput.ReadLineAsync();
        if (line == null)
            return null;

        return JsonSerializer.Deserialize<PythonResponse>(line);
    }
}

public class PythonRuntimeOptions
{
    public string PythonExecutable { get; set; } = "python";
    public Dictionary<string, object> Capabilities { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class PythonResponse
{
    public string? Status { get; set; }
    public string? Error { get; set; }
    public object? Data { get; set; }
}

// ==========================================
// Python side (illustrative module wrapper)
// ==========================================
/*
import sys
import json
import importlib.util

class ModuleHost:
    def __init__(self):
        self.module = None
        self.module_instance = None

    def import_module(self, module_path):
        try:
            spec = importlib.util.spec_from_file_location("visora_module", module_path)
            if spec is None or spec.loader is None:
                return {"status": "error", "error": "Failed to load module spec"}

            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)

            # Find module class
            module_class = None
            for name in dir(module):
                obj = getattr(module, name)
                if hasattr(obj, '_visora_module_metadata'):
                    module_class = obj
                    break

            if module_class is None:
                return {"status": "error", "error": "No module class found"}

            self.module = module
            self.module_instance = module_class()
            return {"status": "success"}
        except Exception as e:
            return {"status": "error", "error": str(e)}

    def initialize(self, context):
        try:
            if hasattr(self.module_instance, 'initialize'):
                self.module_instance.initialize(context)
            return {"status": "success"}
        except Exception as e:
            return {"status": "error", "error": str(e)}

    def shutdown(self):
        try:
            if hasattr(self.module_instance, 'shutdown'):
                self.module_instance.shutdown()
            return {"status": "success"}
        except Exception as e:
            return {"status": "error", "error": str(e)}

    def run(self):
        # Main loop - read commands from stdin, execute, write response to stdout
        while True:
            try:
                line = sys.stdin.readline()
                if not line or line.strip() == "exit":
                    break

                command = json.loads(line)
                cmd_type = command.get("command")

                if cmd_type == "import_module":
                    response = self.import_module(command["module_path"])
                elif cmd_type == "initialize":
                    response = self.initialize(command.get("context", {}))
                elif cmd_type == "shutdown":
                    response = self.shutdown()
                else:
                    response = {"status": "error", "error": f"Unknown command: {cmd_type}"}

                print(json.dumps(response), flush=True)
            except Exception as e:
                print(json.dumps({"status": "error", "error": str(e)}), flush=True)

if __name__ == "__main__":
    host = ModuleHost()
    host.run()
*/
```

---

## Node.js Runtime Lifecycle Illustration

### Conceptual Approach

**Two Options:**
1. **Subprocess:** Node.js runs in separate process, communicate via IPC
2. **Embedded:** Node.js embedded via edge-js or similar, runs in-process

### Illustrative Example: Subprocess Node.js Lifecycle

```csharp
// ==========================================
// ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
// ==========================================

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ILLUSTRATIVE: Manages lifecycle of Node.js module in subprocess
/// </summary>
public class NodeJsModuleHandle : IAsyncDisposable
{
    private readonly string _modulePath;
    private readonly NodeJsRuntimeOptions _options;
    private Process? _nodeProcess;
    private StreamWriter? _processInput;
    private StreamReader? _processOutput;
    private bool _initialized;

    public NodeJsModuleHandle(string modulePath, NodeJsRuntimeOptions options)
    {
        _modulePath = modulePath;
        _options = options;
    }

    /// <summary>
    /// PHASE 1: LOAD - Start Node.js process and require module
    /// </summary>
    public static async Task<NodeJsModuleHandle> LoadAsync(
        string modulePath,
        NodeJsRuntimeOptions options,
        CancellationToken ct)
    {
        var handle = new NodeJsModuleHandle(modulePath, options);

        // Create a wrapper script that will load the module and handle IPC
        var wrapperScript = CreateWrapperScript(modulePath);
        var wrapperPath = Path.Combine(Path.GetTempPath(), "visora-node-wrapper.js");
        await File.WriteAllTextAsync(wrapperPath, wrapperScript, ct);

        // Start Node.js process
        var psi = new ProcessStartInfo
        {
            FileName = options.NodeExecutable,
            Arguments = wrapperPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        handle._nodeProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Node.js process");

        handle._processInput = handle._nodeProcess.StandardInput;
        handle._processOutput = handle._nodeProcess.StandardOutput;

        // Wait for ready signal
        var readyResponse = await handle.ReceiveResponseAsync(ct);
        if (readyResponse?.Status != "ready")
        {
            await handle.DisposeAsync();
            throw new InvalidOperationException(
                $"Node.js module failed to load: {readyResponse?.Error}");
        }

        return handle;
    }

    /// <summary>
    /// PHASE 2: INITIALIZE - Call module's initialize method
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;  // Idempotent

        // Send initialize command
        var initCommand = new
        {
            command = "initialize",
            context = new
            {
                capabilities = _options.Capabilities,
                properties = _options.Properties
            }
        };

        await SendCommandAsync(initCommand, ct);
        var response = await ReceiveResponseAsync(ct);

        if (response?.Status != "success")
        {
            throw new InvalidOperationException(
                $"Node.js module initialization failed: {response?.Error}");
        }

        _initialized = true;
    }

    /// <summary>
    /// PHASE 3: SHUTDOWN - Call module's shutdown method
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (!_initialized)
            return;  // Idempotent

        try
        {
            // Send shutdown command
            var shutdownCommand = new { command = "shutdown" };
            await SendCommandAsync(shutdownCommand, ct);
            var response = await ReceiveResponseAsync(ct);

            if (response?.Status != "success")
            {
                Console.WriteLine($"Node.js shutdown warning: {response?.Error}");
                // Log but don't throw
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Node.js shutdown error: {ex.Message}");
            // Log but don't throw
        }
        finally
        {
            _initialized = false;
        }
    }

    /// <summary>
    /// PHASE 4: DISPOSE - Stop Node.js process
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Ensure shutdown
            await ShutdownAsync();

            // Send exit command
            if (_processInput != null && _nodeProcess != null && !_nodeProcess.HasExited)
            {
                var exitCommand = new { command = "exit" };
                await SendCommandAsync(exitCommand, CancellationToken.None);

                // Wait for graceful exit (with timeout)
                var exitTask = _nodeProcess.WaitForExitAsync();
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
                {
                    // Timeout - force kill
                    _nodeProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Node.js disposal error: {ex.Message}");
        }
        finally
        {
            _processInput?.Dispose();
            _processOutput?.Dispose();
            _nodeProcess?.Dispose();
        }
    }

    private async Task SendCommandAsync(object command, CancellationToken ct)
    {
        if (_processInput == null)
            throw new InvalidOperationException("Process not started");

        var json = JsonSerializer.Serialize(command);
        await _processInput.WriteLineAsync(json);
        await _processInput.FlushAsync();
    }

    private async Task<NodeResponse?> ReceiveResponseAsync(CancellationToken ct)
    {
        if (_processOutput == null)
            throw new InvalidOperationException("Process not started");

        var line = await _processOutput.ReadLineAsync();
        if (line == null)
            return null;

        return JsonSerializer.Deserialize<NodeResponse>(line);
    }

    private static string CreateWrapperScript(string modulePath)
    {
        // ILLUSTRATIVE: Wrapper script to load module and handle IPC
        return $@"
const readline = require('readline');
const path = require('path');

let moduleInstance = null;

// Load the module
try {{
    const modulePath = '{modulePath.Replace("\\", "\\\\")}';
    const ModuleClass = require(modulePath);

    // Instantiate module
    if (typeof ModuleClass === 'function') {{
        moduleInstance = new ModuleClass();
    }} else if (ModuleClass.VisoraModule) {{
        moduleInstance = new ModuleClass.VisoraModule();
    }} else {{
        console.log(JSON.stringify({{ status: 'error', error: 'No module class found' }}));
        process.exit(1);
    }}

    // Signal ready
    console.log(JSON.stringify({{ status: 'ready' }}));
}} catch (error) {{
    console.log(JSON.stringify({{ status: 'error', error: error.message }}));
    process.exit(1);
}}

// Setup IPC via stdin/stdout
const rl = readline.createInterface({{
    input: process.stdin,
    output: process.stdout,
    terminal: false
}});

rl.on('line', async (line) => {{
    try {{
        const command = JSON.parse(line);

        if (command.command === 'initialize') {{
            if (typeof moduleInstance.initialize === 'function') {{
                await moduleInstance.initialize(command.context);
            }}
            console.log(JSON.stringify({{ status: 'success' }}));
        }} else if (command.command === 'shutdown') {{
            if (typeof moduleInstance.shutdown === 'function') {{
                await moduleInstance.shutdown();
            }}
            console.log(JSON.stringify({{ status: 'success' }}));
        }} else if (command.command === 'exit') {{
            process.exit(0);
        }} else {{
            console.log(JSON.stringify({{ status: 'error', error: 'Unknown command' }}));
        }}
    }} catch (error) {{
        console.log(JSON.stringify({{ status: 'error', error: error.message }}));
    }}
}});
";
    }
}

public class NodeJsRuntimeOptions
{
    public string NodeExecutable { get; set; } = "node";
    public Dictionary<string, object> Capabilities { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class NodeResponse
{
    public string? Status { get; set; }
    public string? Error { get; set; }
    public object? Data { get; set; }
}
```

---

## Cross-Runtime Coordination

### Coordinating Lifecycle Across Runtimes

**Challenge:** Initialize modules in the correct order across different runtimes

```csharp
// ILLUSTRATIVE EXAMPLE

public class MultiRuntimeModuleCatalog : IAsyncDisposable
{
    private readonly List<IModuleHandle> _modules = new();

    /// <summary>
    /// PHASE 1: LOAD - Load modules from all runtimes
    /// </summary>
    public async Task LoadAllModulesAsync(CancellationToken ct)
    {
        var tasks = new List<Task<IModuleHandle>>();

        // Load .NET modules
        foreach (var dotnetFile in EnumerateDotNetModules())
        {
            tasks.Add(ModuleHandle.LoadAsync(dotnetFile, _dotnetOptions, ct)
                .ContinueWith(t => (IModuleHandle)t.Result, ct));
        }

        // Load Python modules
        foreach (var pythonFile in EnumeratePythonModules())
        {
            tasks.Add(PythonModuleHandle.LoadAsync(pythonFile, _pythonOptions, ct)
                .ContinueWith(t => (IModuleHandle)t.Result, ct));
        }

        // Load Node.js modules
        foreach (var nodeFile in EnumerateNodeJsModules())
        {
            tasks.Add(NodeJsModuleHandle.LoadAsync(nodeFile, _nodeOptions, ct)
                .ContinueWith(t => (IModuleHandle)t.Result, ct));
        }

        // Wait for all to load
        var loadedModules = await Task.WhenAll(tasks);
        _modules.AddRange(loadedModules);
    }

    /// <summary>
    /// PHASE 2: INITIALIZE - Initialize in dependency order
    /// </summary>
    public async Task InitializeAllAsync(CancellationToken ct)
    {
        // Resolve dependency order
        var orderedModules = ResolveDependencyOrder(_modules);

        // Initialize in order
        foreach (var module in orderedModules)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await module.EnsureInitializedAsync(ct);
                Console.WriteLine($"Initialized: {module.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize module: {ex.Message}");
                // Continue with other modules
            }
        }
    }

    /// <summary>
    /// PHASE 3: SHUTDOWN - Shutdown in reverse dependency order
    /// </summary>
    public async Task ShutdownAllAsync(CancellationToken ct)
    {
        // Reverse dependency order
        var orderedModules = ResolveDependencyOrder(_modules);
        orderedModules.Reverse();

        foreach (var module in orderedModules)
        {
            try
            {
                await module.ShutdownAsync(ct);
                Console.WriteLine($"Shutdown: {module.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shutdown warning: {ex.Message}");
                // Continue with other modules
            }
        }
    }

    /// <summary>
    /// PHASE 4: DISPOSE - Dispose all modules
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var module in _modules)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Disposal error: {ex.Message}");
                // Continue with other modules
            }
        }

        _modules.Clear();
    }

    private List<IModuleHandle> ResolveDependencyOrder(List<IModuleHandle> modules)
    {
        // ILLUSTRATIVE: Topological sort based on dependencies
        // In reality, would need dependency metadata from modules
        return modules; // Simplified
    }
}

public interface IModuleHandle : IAsyncDisposable
{
    Task EnsureInitializedAsync(CancellationToken ct);
    Task ShutdownAsync(CancellationToken ct);
}
```

---

## State Management Across Runtimes

### Saving and Restoring State During Hot-Swap

```csharp
// ILLUSTRATIVE EXAMPLE

public interface IStatefulModuleHandle : IModuleHandle
{
    Task<ModuleState> SaveStateAsync(CancellationToken ct);
    Task RestoreStateAsync(ModuleState state, CancellationToken ct);
}

public class ModuleState
{
    public string Runtime { get; set; }
    public string ModuleName { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class StatefulPythonModuleHandle : PythonModuleHandle, IStatefulModuleHandle
{
    public async Task<ModuleState> SaveStateAsync(CancellationToken ct)
    {
        // Send save_state command to Python module
        var command = new { command = "save_state" };
        await SendCommandAsync(command, ct);
        var response = await ReceiveResponseAsync(ct);

        return new ModuleState
        {
            Runtime = "python",
            ModuleName = Path.GetFileNameWithoutExtension(_modulePath),
            Data = response?.Data as Dictionary<string, object> ?? new()
        };
    }

    public async Task RestoreStateAsync(ModuleState state, CancellationToken ct)
    {
        // Send restore_state command to Python module
        var command = new
        {
            command = "restore_state",
            state = state.Data
        };
        await SendCommandAsync(command, ct);
        var response = await ReceiveResponseAsync(ct);

        if (response?.Status != "success")
        {
            throw new InvalidOperationException("Failed to restore state");
        }
    }
}

// Hot-swap with state preservation
public async Task HotSwapModuleAsync(
    string moduleName,
    string newModulePath,
    CancellationToken ct)
{
    // 1. Find current module
    var oldModule = _modules.OfType<IStatefulModuleHandle>()
        .FirstOrDefault(m => m.ModuleName == moduleName);

    if (oldModule == null)
        throw new InvalidOperationException("Module not found");

    // 2. Save state
    var state = await oldModule.SaveStateAsync(ct);

    // 3. Shutdown and dispose old module
    await oldModule.ShutdownAsync(ct);
    await oldModule.DisposeAsync();

    // 4. Load new module (detect runtime from file extension)
    IStatefulModuleHandle newModule;
    if (newModulePath.EndsWith(".vixm.dll"))
    {
        newModule = (IStatefulModuleHandle)await ModuleHandle.LoadAsync(
            newModulePath, _dotnetOptions, ct);
    }
    else if (newModulePath.EndsWith("_vixm.py"))
    {
        newModule = new StatefulPythonModuleHandle(newModulePath, _pythonOptions);
        await ((PythonModuleHandle)newModule).LoadAsync(newModulePath, _pythonOptions, ct);
    }
    else
    {
        throw new NotSupportedException("Unknown module runtime");
    }

    // 5. Initialize new module
    await newModule.EnsureInitializedAsync(ct);

    // 6. Restore state
    await newModule.RestoreStateAsync(state, ct);

    // 7. Replace in catalog
    _modules.Remove(oldModule);
    _modules.Add(newModule);
}
```

---

## Hot-Swap Across Runtimes

### Cross-Runtime Hot-Swap Scenario

**Scenario:** Replace a .NET module with a Python module (or vice versa)

```csharp
// ILLUSTRATIVE EXAMPLE

public class CrossRuntimeHotSwap
{
    public async Task SwapDotNetToPythonAsync(
        string dotnetModuleName,
        string pythonModulePath,
        CancellationToken ct)
    {
        // 1. Find .NET module
        var dotnetModule = _modules.OfType<ModuleHandle>()
            .FirstOrDefault(m => m.Descriptor.Name == dotnetModuleName);

        if (dotnetModule == null)
            throw new InvalidOperationException("Module not found");

        // 2. Save state (serialize to JSON)
        var state = await SaveDotNetStateAsync(dotnetModule, ct);

        // 3. Shutdown .NET module
        await dotnetModule.ShutdownAsync(ct);
        await dotnetModule.DisposeAsync();

        // 4. Load Python module
        var pythonModule = await PythonModuleHandle.LoadAsync(
            pythonModulePath,
            _pythonOptions,
            ct);

        // 5. Initialize Python module
        await pythonModule.EnsureInitializedAsync(ct);

        // 6. Restore state (deserialize from JSON, send to Python)
        await RestorePythonStateAsync(pythonModule, state, ct);

        // 7. Replace in catalog
        _modules.Remove(dotnetModule);
        _modules.Add(pythonModule);

        Console.WriteLine($"Hot-swapped .NET → Python: {dotnetModuleName}");
    }

    private async Task<string> SaveDotNetStateAsync(
        ModuleHandle module,
        CancellationToken ct)
    {
        // ILLUSTRATIVE: Extract state from .NET module
        // In reality, module would implement IStateful interface
        var state = new
        {
            runtime = "dotnet",
            module = module.Descriptor.Name,
            version = module.Descriptor.Version,
            // Module-specific state would go here
        };

        return JsonSerializer.Serialize(state);
    }

    private async Task RestorePythonStateAsync(
        PythonModuleHandle module,
        string stateJson,
        CancellationToken ct)
    {
        // ILLUSTRATIVE: Send state to Python module
        var command = new
        {
            command = "restore_state",
            state = JsonSerializer.Deserialize<object>(stateJson)
        };

        await module.SendCommandAsync(command, ct);
        var response = await module.ReceiveResponseAsync(ct);

        if (response?.Status != "success")
        {
            throw new InvalidOperationException("Failed to restore state in Python module");
        }
    }
}
```

---

## Testing Cross-Runtime Lifecycle

### Testing Strategy

```csharp
// ILLUSTRATIVE EXAMPLE

[Fact]
public async Task MultiRuntime_FullLifecycle()
{
    // Arrange
    var catalog = new MultiRuntimeModuleCatalog();

    // PHASE 1: LOAD
    await catalog.LoadAllModulesAsync(CancellationToken.None);

    // Assert: All runtimes loaded
    Assert.Contains(catalog.Modules, m => m is ModuleHandle);  // .NET
    Assert.Contains(catalog.Modules, m => m is PythonModuleHandle);  // Python
    Assert.Contains(catalog.Modules, m => m is NodeJsModuleHandle);  // Node.js

    // PHASE 2: INITIALIZE
    await catalog.InitializeAllAsync(CancellationToken.None);

    // Assert: All initialized

    // PHASE 3: SHUTDOWN
    await catalog.ShutdownAllAsync(CancellationToken.None);

    // Assert: All shutdown

    // PHASE 4: DISPOSE
    await catalog.DisposeAsync();

    // Assert: All disposed
}

[Fact]
public async Task CrossRuntime_HotSwap()
{
    // Arrange
    var catalog = new MultiRuntimeModuleCatalog();
    await catalog.LoadAllModulesAsync(CancellationToken.None);
    await catalog.InitializeAllAsync(CancellationToken.None);

    // Act: Hot-swap .NET module with Python module
    await catalog.HotSwapModuleAsync(
        "MyModule",
        "my_module_vixm.py",
        CancellationToken.None);

    // Assert: Python module now loaded
    var pythonModule = catalog.Modules.OfType<PythonModuleHandle>()
        .FirstOrDefault(m => m.ModuleName == "MyModule");
    Assert.NotNull(pythonModule);

    // Cleanup
    await catalog.DisposeAsync();
}
```

---

## Architectural Patterns

### Pattern 1: Runtime Abstraction Layer

```csharp
// ILLUSTRATIVE EXAMPLE

public interface IModuleRuntime : IAsyncDisposable
{
    string RuntimeName { get; }
    bool CanHandle(string filePath);

    Task<IModuleHandle> LoadModuleAsync(
        string modulePath,
        ModuleCatalogOptions options,
        CancellationToken ct);
}

public class DotNetRuntime : IModuleRuntime
{
    public string RuntimeName => "dotnet";

    public bool CanHandle(string filePath) =>
        filePath.EndsWith(".vixm.dll", StringComparison.OrdinalIgnoreCase);

    public async Task<IModuleHandle> LoadModuleAsync(
        string modulePath,
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        return await ModuleHandle.LoadAsync(modulePath, options, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class PythonRuntime : IModuleRuntime
{
    private readonly PythonRuntimeOptions _options;

    public string RuntimeName => "python";

    public bool CanHandle(string filePath) =>
        filePath.EndsWith("_vixm.py", StringComparison.OrdinalIgnoreCase);

    public async Task<IModuleHandle> LoadModuleAsync(
        string modulePath,
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        return await PythonModuleHandle.LoadAsync(modulePath, _options, ct);
    }

    public ValueTask DisposeAsync()
    {
        // Cleanup Python runtime resources
        return ValueTask.CompletedTask;
    }
}

public class NodeJsRuntime : IModuleRuntime
{
    private readonly NodeJsRuntimeOptions _options;

    public string RuntimeName => "nodejs";

    public bool CanHandle(string filePath) =>
        filePath.EndsWith(".vixm.js", StringComparison.OrdinalIgnoreCase);

    public async Task<IModuleHandle> LoadModuleAsync(
        string modulePath,
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        return await NodeJsModuleHandle.LoadAsync(modulePath, _options, ct);
    }

    public ValueTask DisposeAsync()
    {
        // Cleanup Node.js runtime resources
        return ValueTask.CompletedTask;
    }
}

// Usage
public class RuntimeAwareModuleCatalog
{
    private readonly List<IModuleRuntime> _runtimes = new();

    public RuntimeAwareModuleCatalog()
    {
        _runtimes.Add(new DotNetRuntime());
        _runtimes.Add(new PythonRuntime());
        _runtimes.Add(new NodeJsRuntime());
    }

    public async Task<IModuleHandle> LoadModuleAsync(
        string modulePath,
        CancellationToken ct)
    {
        // Find appropriate runtime
        var runtime = _runtimes.FirstOrDefault(r => r.CanHandle(modulePath));
        if (runtime == null)
        {
            throw new NotSupportedException(
                $"No runtime available for: {modulePath}");
        }

        // Load using runtime
        return await runtime.LoadModuleAsync(modulePath, _options, ct);
    }
}
```

---

### Pattern 2: Lifecycle Event Bus

```csharp
// ILLUSTRATIVE EXAMPLE

public enum LifecyclePhase
{
    Loading,
    Loaded,
    Initializing,
    Initialized,
    ShuttingDown,
    Shutdown,
    Disposing,
    Disposed
}

public class LifecycleEvent
{
    public string ModuleName { get; set; }
    public string Runtime { get; set; }
    public LifecyclePhase Phase { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LifecycleEventBus
{
    public event EventHandler<LifecycleEvent>? PhaseChanged;

    public void PublishPhaseChange(LifecycleEvent evt)
    {
        PhaseChanged?.Invoke(this, evt);
    }
}

public class ObservableModuleHandle : IModuleHandle
{
    private readonly IModuleHandle _inner;
    private readonly LifecycleEventBus _eventBus;
    private readonly string _moduleName;
    private readonly string _runtime;

    public async Task EnsureInitializedAsync(CancellationToken ct)
    {
        _eventBus.PublishPhaseChange(new LifecycleEvent
        {
            ModuleName = _moduleName,
            Runtime = _runtime,
            Phase = LifecyclePhase.Initializing,
            Timestamp = DateTime.UtcNow
        });

        try
        {
            await _inner.EnsureInitializedAsync(ct);

            _eventBus.PublishPhaseChange(new LifecycleEvent
            {
                ModuleName = _moduleName,
                Runtime = _runtime,
                Phase = LifecyclePhase.Initialized,
                Timestamp = DateTime.UtcNow,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _eventBus.PublishPhaseChange(new LifecycleEvent
            {
                ModuleName = _moduleName,
                Runtime = _runtime,
                Phase = LifecyclePhase.Initialized,
                Timestamp = DateTime.UtcNow,
                Success = false,
                Error = ex.Message
            });
            throw;
        }
    }

    // Similar for Shutdown, Dispose...
}

// Usage
var eventBus = new LifecycleEventBus();
eventBus.PhaseChanged += (sender, evt) =>
{
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.Runtime}/{evt.ModuleName}: " +
                     $"{evt.Phase} - {(evt.Success ? "OK" : evt.Error)}");
};
```

---

## Summary

### Key Takeaways

1. **Cross-Runtime Lifecycle Complexity**
   - .NET: In-process, direct method calls
   - Python: Subprocess or embedded, IPC required
   - Node.js: Subprocess or embedded, event loop management
   - Each runtime has unique initialization requirements

2. **Four-Phase Pattern Applies to All**
   - Load → Initialize → Shutdown → Dispose
   - Pattern is universal, implementation varies
   - Subprocess communication adds latency

3. **State Management Challenges**
   - Must serialize state for cross-runtime hot-swap
   - JSON is common serialization format
   - State transfer happens during shutdown/initialize

4. **Coordination Required**
   - Dependency ordering across runtimes
   - Lifecycle event propagation
   - Error handling and isolation

5. **Testing Complexity**
   - Must test each runtime separately
   - Integration tests for cross-runtime coordination
   - State preservation tests for hot-swap

---

### Implementation Recommendations

If building a meta-platform VISORA:

1. **Use Subprocess Isolation**
   - Start Python/Node.js in separate processes
   - Communicate via stdin/stdout (JSON-RPC)
   - Easier to debug and manage

2. **Implement Runtime Adapters**
   - Abstract runtime-specific details
   - Common IModuleHandle interface
   - Each adapter manages its runtime lifecycle

3. **Design for State Transfer**
   - All modules should support state serialization
   - Use common format (JSON)
   - Document state schema

4. **Monitor Lifecycle Events**
   - Implement event bus for visibility
   - Track phase transitions
   - Aid debugging and diagnostics

5. **Handle Failures Gracefully**
   - Runtime crashes shouldn't affect host
   - Automatic restart for crashed runtimes
   - Preserve state across restarts

---

### Further Reading

- **Related Pattern:** Plugin Architecture - Isolation mechanisms
- **Related Pattern:** Reflection Discovery - Finding modules across runtimes
- **IPC Patterns:** JSON-RPC, Protocol Buffers, gRPC
- **Process Management:** Subprocess lifetime, graceful shutdown
- **State Management:** Serialization, persistence, recovery
