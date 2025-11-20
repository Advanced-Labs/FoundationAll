# Command Execution Pattern - Meta-Platform Illustrations

**Last Updated**: 2025-11-10  
**VISORA Version Reference**: .NET 9.0  
**Pattern Tier**: Tier 2 (Communication & Execution)

---

## ⚠️ Important Disclaimer

**These are ILLUSTRATIVE EXAMPLES ONLY** - conceptual code to spark imagination and explore possibilities. This is NOT production-ready code or prescriptive design for your meta-platform.

- Examples show *possible* approaches to adapting VISORA's command pattern
- Python and Node.js code is **conceptual** to demonstrate ideas
- Actual implementation will depend on your specific meta-platform architecture
- Use these as thought experiments and starting points for your own designs

---

## Conceptual Adaptation

### Pattern Translation

**VISORA (.NET)** → **Meta-Platform (Polyglot)**

| Aspect | VISORA | Meta-Platform Possibility |
|--------|--------|-------------------------|
| Command Base | `VisoraCommand` abstract class | Decorator-based (@command) or export-based |
| Metadata | `CommandDescriptor` record | JSON-based descriptor across languages |
| Execution | `ExecuteAsync(context)` | Unified async function signature |
| Results | `CommandResult` struct | Result objects in Python/Node.js |
| Discovery | Reflection-based | Decorator scanning, export introspection |

---

## Illustrative Example: Python Command Pattern

### ⚠️ CONCEPTUAL: Python Framework API

```python
# visora_platform/command.py - Illustrative framework code
from dataclasses import dataclass
from typing import Any, Dict, Optional, Callable, Awaitable
from enum import Enum

class CommandKind(Enum):
    GENERAL = "general"
    NAVIGATION = "navigation"
    TOOL = "tool"
    SHELL = "shell"
    AUTOMATION = "automation"

@dataclass
class CommandDescriptor:
    """Metadata describing a command."""
    id: str
    title: str
    description: Optional[str] = None
    kind: CommandKind = CommandKind.GENERAL
    aliases: Optional[list[str]] = None
    keywords: Optional[list[str]] = None
    is_visible: bool = True

class CommandOutcome(Enum):
    SUCCESS = "success"
    CANCELLED = "cancelled"
    FAILED = "failed"

@dataclass
class CommandResult:
    """Result of command execution."""
    outcome: CommandOutcome
    message: Optional[str] = None
    payload: Optional[Any] = None
    
    @staticmethod
    def success(message: Optional[str] = None, payload: Optional[Any] = None):
        return CommandResult(CommandOutcome.SUCCESS, message, payload)
    
    @staticmethod
    def cancelled(message: Optional[str] = None):
        return CommandResult(CommandOutcome.CANCELLED, message, None)
    
    @staticmethod
    def failed(message: Optional[str] = None, payload: Optional[Any] = None):
        return CommandResult(CommandOutcome.FAILED, message, payload)

# Decorator for command registration
def command(id: str, title: str, **kwargs):
    """
    Decorator to mark a function as a platform command.
    
    Usage:
        @command("my.command", "My Command", description="Does something")
        async def my_command(context):
            return CommandResult.success("Done!")
    """
    def decorator(func: Callable[[Any], Awaitable[CommandResult]]):
        descriptor = CommandDescriptor(id=id, title=title, **kwargs)
        func._platform_command_descriptor = descriptor
        return func
    return decorator
```

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Command Implementation

```python
# my_module/commands.py - Example Python module with commands
from visora_platform.command import command, CommandResult, CommandKind, CommandOutcome
from datetime import datetime
import asyncio

@command(
    id="python.ping",
    title="Python Ping",
    description="Ping command implemented in Python",
    kind=CommandKind.AUTOMATION,
    keywords=["diagnostics", "ping", "python"]
)
async def ping_command(context):
    """Simple ping command similar to VISORA's PingCommand."""
    now = datetime.utcnow()
    surface = context.surface
    module_name = context.module.descriptor.get("name", "Unknown")
    
    message = f"Pong from '{module_name}' via {surface} @ {now.isoformat()}"
    payload = {
        "timestamp": now.isoformat(),
        "surface": surface,
        "module_id": context.module.descriptor.get("id")
    }
    
    return CommandResult.success(message, payload)

@command(
    id="python.file.read",
    title="Read File (Python)",
    description="Reads a file asynchronously",
    kind=CommandKind.TOOL
)
async def read_file_command(context):
    """Async file read command with error handling."""
    try:
        # Get path from parameters
        path = context.parameters.get("path")
        if not path:
            return CommandResult.failed("Parameter 'path' is required")
        
        # Check cancellation
        if context.is_cancelled():
            return CommandResult.cancelled("Operation cancelled")
        
        # Read file asynchronously (using asyncio)
        async with aiofiles.open(path, 'r') as f:
            content = await f.read()
        
        payload = {
            "path": path,
            "length": len(content),
            "content": content
        }
        
        return CommandResult.success(
            f"Read {len(content)} characters from {path}",
            payload
        )
        
    except FileNotFoundError:
        return CommandResult.failed(f"File not found: {path}")
    except PermissionError:
        return CommandResult.failed("Access denied")
    except Exception as e:
        return CommandResult.failed(f"Error reading file: {str(e)}")

@command(
    id="python.capability.demo",
    title="Capability Demo",
    description="Demonstrates accessing .NET capabilities from Python"
)
async def capability_demo_command(context):
    """Shows how Python command might access .NET capabilities."""
    # Access capability through context
    logger = context.capabilities.get_optional("ILogger")
    if logger:
        await logger.log_information("Python command accessing .NET logger!")
    
    file_system = context.capabilities.get_required("IFileSystem")
    files = await file_system.list_files("/tmp")
    
    return CommandResult.success(
        f"Found {len(files)} files",
        {"files": files}
    )
```

---

## Illustrative Example: Node.js Command Pattern

### ⚠️ CONCEPTUAL: Node.js Framework API

```javascript
// visora-platform/command.js - Illustrative framework code
class CommandDescriptor {
    constructor({ id, title, description = null, kind = 'general', 
                 aliases = null, keywords = null, isVisible = true }) {
        this.id = id;
        this.title = title;
        this.description = description;
        this.kind = kind;
        this.aliases = aliases;
        this.keywords = keywords;
        this.isVisible = isVisible;
    }
}

class CommandResult {
    constructor(outcome, message = null, payload = null) {
        this.outcome = outcome;
        this.message = message;
        this.payload = payload;
    }
    
    static success(message = null, payload = null) {
        return new CommandResult('success', message, payload);
    }
    
    static cancelled(message = null) {
        return new CommandResult('cancelled', message, null);
    }
    
    static failed(message = null, payload = null) {
        return new CommandResult('failed', message, payload);
    }
}

// Export-based command registration
function defineCommand(descriptor, handler) {
    return {
        _isPlatformCommand: true,
        descriptor: new CommandDescriptor(descriptor),
        execute: handler
    };
}

module.exports = { CommandDescriptor, CommandResult, defineCommand };
```

### ⚠️ ILLUSTRATIVE EXAMPLE: Node.js Command Implementation

```javascript
// my-module/commands.js - Example Node.js module with commands
const { defineCommand, CommandResult } = require('visora-platform/command');
const fs = require('fs').promises;

const pingCommand = defineCommand(
    {
        id: 'nodejs.ping',
        title: 'Node.js Ping',
        description: 'Ping command implemented in Node.js',
        kind: 'automation',
        keywords: ['diagnostics', 'ping', 'nodejs']
    },
    async (context) => {
        const now = new Date();
        const surface = context.surface;
        const moduleName = context.module.descriptor.name || 'Unknown';
        
        const message = `Pong from '${moduleName}' via ${surface} @ ${now.toISOString()}`;
        const payload = {
            timestamp: now.toISOString(),
            surface: surface,
            moduleId: context.module.descriptor.id
        };
        
        return CommandResult.success(message, payload);
    }
);

const readFileCommand = defineCommand(
    {
        id: 'nodejs.file.read',
        title: 'Read File (Node.js)',
        description: 'Reads a file asynchronously',
        kind: 'tool'
    },
    async (context) => {
        try {
            const path = context.parameters.path;
            if (!path) {
                return CommandResult.failed("Parameter 'path' is required");
            }
            
            // Check cancellation (if context provides cancellation token)
            if (context.isCancelled && context.isCancelled()) {
                return CommandResult.cancelled("Operation cancelled");
            }
            
            const content = await fs.readFile(path, 'utf8');
            
            const payload = {
                path: path,
                length: content.length,
                content: content
            };
            
            return CommandResult.success(
                `Read ${content.length} characters from ${path}`,
                payload
            );
            
        } catch (error) {
            if (error.code === 'ENOENT') {
                return CommandResult.failed(`File not found: ${context.parameters.path}`);
            } else if (error.code === 'EACCES') {
                return CommandResult.failed("Access denied");
            } else {
                return CommandResult.failed(`Error reading file: ${error.message}`);
            }
        }
    }
);

module.exports = {
    pingCommand,
    readFileCommand
};
```

---

## Conceptual: Uniform Invocation from .NET

### ⚠️ ILLUSTRATIVE: Cross-Language Command Invoker

```csharp
// Conceptual C# code for invoking Python/Node.js commands
public interface ICommandInvoker
{
    Task<CommandResult> InvokeAsync(
        string commandId,
        CommandContext context,
        CancellationToken cancellationToken = default);
}

public class PolyglotCommandInvoker : ICommandInvoker
{
    private readonly Dictionary<string, ICommandRuntime> _runtimeByLanguage = new();
    
    public async Task<CommandResult> InvokeAsync(
        string commandId, 
        CommandContext context, 
        CancellationToken cancellationToken)
    {
        // Determine runtime from command ID prefix or registry
        var runtime = DetermineRuntime(commandId);
        
        // Invoke command in appropriate runtime
        return await runtime.ExecuteCommandAsync(commandId, context, cancellationToken);
    }
}

// Python runtime bridge
public class PythonCommandRuntime : ICommandRuntime
{
    private readonly Python.Runtime.PyObject _commandRegistry;
    
    public async Task<CommandResult> ExecuteCommandAsync(
        string commandId,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        // Marshal context to Python
        using var pyContext = MarshalContext(context);
        
        // Get command function from registry
        using var commandFunc = _commandRegistry[commandId];
        
        // Invoke async Python function
        using var resultTask = commandFunc.InvokeAsync(pyContext);
        var pyResult = await resultTask;
        
        // Marshal result back to .NET
        return UnmarshalResult(pyResult);
    }
    
    private PyObject MarshalContext(CommandContext context)
    {
        // Convert .NET context to Python dict
        var pyDict = new PyDict();
        pyDict["surface"] = new PyString(context.Surface.ToString());
        pyDict["parameters"] = MarshalParameters(context.Parameters);
        pyDict["capabilities"] = new CapabilityProxy(context.Capabilities);
        // ... etc
        return pyDict;
    }
}

// Node.js runtime bridge  
public class NodeJsCommandRuntime : ICommandRuntime
{
    private readonly EdgeJs.Func _invokeCommand;
    
    public async Task<CommandResult> ExecuteCommandAsync(
        string commandId,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        // Marshal context to JavaScript object
        var jsContext = new
        {
            commandId,
            surface = context.Surface.ToString(),
            parameters = context.Parameters,
            capabilities = new CapabilityProxy(context.Capabilities)
        };
        
        // Invoke via Edge.js bridge
        var result = await _invokeCommand(jsContext);
        
        // Unmarshal result
        return UnmarshalResult(result);
    }
}
```

---

## Conceptual: Command Discovery Across Languages

### ⚠️ ILLUSTRATIVE: Multi-Runtime Command Registry

```csharp
public class MultiRuntimeCommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _commands = new();
    
    public async Task DiscoverCommandsAsync()
    {
        // Discover .NET commands (existing VISORA pattern)
        await DiscoverDotNetCommandsAsync();
        
        // Discover Python commands via decorator scanning
        await DiscoverPythonCommandsAsync();
        
        // Discover Node.js commands via export scanning
        await DiscoverNodeJsCommandsAsync();
    }
    
    private async Task DiscoverPythonCommandsAsync()
    {
        // Illustrative: Scan Python modules for @command decorators
        foreach (var pythonModule in _pythonModules)
        {
            using (Py.GIL())
            {
                var module = Py.Import(pythonModule.Name);
                var members = module.Dir();
                
                foreach (var member in members)
                {
                    var obj = module.GetAttr(member);
                    
                    // Check if has _platform_command_descriptor attribute
                    if (obj.HasAttr("_platform_command_descriptor"))
                    {
                        var descriptor = obj.GetAttr("_platform_command_descriptor");
                        var commandId = descriptor.GetAttr("id").ToString();
                        
                        _commands[commandId] = new CommandRegistration
                        {
                            CommandId = commandId,
                            Runtime = RuntimeType.Python,
                            Descriptor = UnmarshalDescriptor(descriptor),
                            Handler = obj
                        };
                    }
                }
            }
        }
    }
}
```

---

## Challenges in Cross-Language Command Execution

### 1. Type Marshaling
- **Challenge**: Converting .NET types ↔ Python types ↔ Node.js types
- **Considerations**: Primitives are easy, complex objects need serialization
- **Approach**: JSON-based serialization for parameters/results, proxies for capabilities

### 2. Async Model Differences
- **Challenge**: .NET Task/ValueTask vs Python asyncio vs Node.js Promise
- **Considerations**: Event loop coordination, cancellation propagation
- **Approach**: Runtime bridges handle async translation

### 3. Error Handling
- **Challenge**: Exception semantics differ across languages
- **Considerations**: Stack traces, error types, serialization
- **Approach**: Result objects avoid exception marshaling complexity

### 4. Performance
- **Challenge**: Cross-runtime invocation overhead
- **Considerations**: Serialization cost, process boundaries, IPC
- **Approach**: In-process hosting where possible, efficient serialization

---

## Summary

These illustrative examples show **possible approaches** to adapting VISORA's command pattern for polyglot scenarios:

- ✅ Decorator-based (@command) or export-based command registration
- ✅ Unified descriptor format (JSON-based across languages)
- ✅ Result objects for clean cross-boundary error handling
- ✅ Runtime bridges for .NET ↔ Python ↔ Node.js invocation
- ✅ Capability proxies for accessing .NET services from other languages

**Remember**: These are thought experiments, not production designs. Your actual implementation will depend on your meta-platform's specific requirements and architecture.

---

**Related**: [command-execution/visora-analysis.md](visora-analysis.md), [multi-surface-execution](../multi-surface-execution/), [result-objects](../result-objects/)
