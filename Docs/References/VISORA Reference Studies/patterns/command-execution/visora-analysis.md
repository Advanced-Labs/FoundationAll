# Command Execution Pattern - VISORA Analysis

**Last Updated**: 2025-11-10  
**VISORA Version**: .NET 9.0  
**Pattern Tier**: Tier 2 (Communication & Execution)

---

## Pattern Overview

The **Command Execution Pattern** in VISORA provides an abstract, uniform way to execute operations across different surfaces (CLI, UI, Automation, Remote) while maintaining type safety and rich result handling.

### What is This Pattern?

Commands are executable units of work that:
- Implement a common abstract base class (`VisoraCommand`)
- Have rich metadata (`CommandDescriptor`)
- Execute asynchronously with cancellation support
- Return structured results (`CommandResult`) instead of throwing exceptions
- Can run on multiple surfaces without code changes

### Why VISORA Uses This Pattern

**Problem**: Need uniform invocation of operations from CLI, UI, automation scripts, and potentially remote systems without duplicating logic.

**Solution**: Abstract command pattern with surface-agnostic execution and metadata-driven discovery.

**Benefits**:
- Same command runs everywhere (CLI, UI, automation)
- Metadata enables discovery and registration
- Result objects enable clean error handling across boundaries
- Async-first design supports I/O-bound operations
- Testable via interface abstraction

---

## VISORA Implementation

### Core Abstractions

**VisoraCommand** (`src/Visora.Contracts/Commands/VisoraCommand.cs:9-15`)

```csharp
public abstract class VisoraCommand
{
    /// <summary>
    /// Metadata describing this command (id, title, kind, UI hints).
    /// </summary>
    public abstract CommandDescriptor Descriptor { get; }
    
    /// <summary>
    /// Execute the command with the given context and cancellation support.
    /// </summary>
    public virtual ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CommandResult.Success());
}
```

**Key Design Decisions**:
- Abstract base class (not interface) provides default `ExecuteAsync` implementation
- `ValueTask<CommandResult>` for performance-optimized async
- Virtual method allows synchronous or async implementations
- `CancellationToken` support throughout

**CommandDescriptor** (`src/Visora.Contracts/Commands/CommandDescriptor.cs`)

```csharp
public sealed record CommandDescriptor(
    string Id,                                       // Unique identifier (e.g., "shell.ping")
    string Title,                                    // Display name
    string? Description = null,                      // User-facing description
    CommandKind Kind = CommandKind.General,          // Categorization
    IReadOnlyCollection<string>? Aliases = null,     // Alternative names
    IReadOnlyCollection<string>? Keywords = null,    // Search keywords
    bool IsVisible = true,                           // Show in UI/help
    bool IsInstanceScoped = false,                   // Per-instance vs shared
    CommandUiHint? Ui = null)                        // UI integration hints
{
    public enum CommandKind 
    { 
        General,      // General-purpose commands
        Navigation,   // Navigation operations
        Tool,         // Developer tools
        Shell,        // Shell-specific commands
        Automation    // Automation/scripting commands
    }
    
    public sealed record CommandUiHint(
        string? MenuPath = null,          // Menu location (e.g., "File/New")
        string? Icon = null,              // Icon identifier
        string? DefaultGesture = null);   // Keyboard shortcut
        
    public static CommandDescriptor Create(
        string id, 
        string title, 
        string? description = null,
        CommandKind kind = CommandKind.General,
        IReadOnlyCollection<string>? aliases = null,
        IReadOnlyCollection<string>? keywords = null,
        bool isVisible = true,
        bool isInstanceScoped = false,
        CommandUiHint? ui = null)
        => new(id, title, description, kind, aliases, keywords, isVisible, isInstanceScoped, ui);
}
```

**CommandResult** (`src/Visora.Contracts/Commands/CommandResult.cs:8-25`)

```csharp
public readonly record struct CommandResult(
    CommandOutcome Outcome,
    string? Message = null,
    object? Payload = null)
{
    public static CommandResult Success(string? message = null, object? payload = null)
        => new(CommandOutcome.Success, message, payload);
        
    public static CommandResult Cancelled(string? message = null)
        => new(CommandOutcome.Cancelled, message, null);
        
    public static CommandResult Failed(string? message = null, object? payload = null)
        => new(CommandOutcome.Failed, message, payload);
        
    public enum CommandOutcome 
    { 
        Success,    // Operation completed successfully
        Cancelled,  // Operation was cancelled by user/system
        Failed      // Operation failed (error in message/payload)
    }
}
```

---

## Code Examples

### Example 1: Simple Diagnostic Command

**PingCommand** (`src/Visora.Shell.Commands.Core/Commands/PingCommand.cs:8-33`)

```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.",
        kind: CommandKind.Automation,
        keywords: new[] { "diagnostics", "ping" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var surface = context.Surface.ToString();
        var message = $"Pong from '{context.Module.Descriptor.Name}' via {surface} @ {now:O}";
        
        var payload = new
        {
            Timestamp = now,
            Surface = context.Surface,
            ModuleId = context.Module.Descriptor.Id
        };

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

### Example 2: Command with Capability Usage

**EnvironmentInfoCommand** (`src/Visora.Shell.Commands.Core/Commands/EnvironmentInfoCommand.cs`)

```csharp
public sealed class EnvironmentInfoCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.env.info",
        title: "Environment Information",
        description: "Displays current environment details.",
        kind: CommandKind.Tool);

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        // Access capabilities if available
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Gathering environment information...");

        var payload = new Dictionary<string, object?>
        {
            ["ProcessId"] = Environment.ProcessId,
            ["MachineName"] = Environment.MachineName,
            ["OSVersion"] = Environment.OSVersion.ToString(),
            ["RuntimeVersion"] = Environment.Version.ToString(),
            ["WorkingDirectory"] = Environment.CurrentDirectory,
            ["Is64BitProcess"] = Environment.Is64BitProcess,
            ["ProcessorCount"] = Environment.ProcessorCount
        };

        var message = $"Process {Environment.ProcessId} on {Environment.MachineName} " +
                     $"({Environment.OSVersion})";

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

### Example 3: Async Command with Error Handling

```csharp
public sealed class FileReadCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "file.read",
        title: "Read File",
        description: "Reads content from a file.");

    public override CommandDescriptor Descriptor => Info;

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get file path from parameters
            if (!context.Parameters.TryGetValue("path", out var pathObj) || pathObj is not string path)
                return CommandResult.Failed("Parameter 'path' is required.");

            // Check cancellation before I/O
            cancellationToken.ThrowIfCancellationRequested();

            // Read file asynchronously
            var content = await File.ReadAllTextAsync(path, cancellationToken);

            var payload = new { Path = path, Length = content.Length, Content = content };
            return CommandResult.Success($"Read {content.Length} characters from {path}", payload);
        }
        catch (OperationCanceledException)
        {
            return CommandResult.Cancelled("File read was cancelled.");
        }
        catch (FileNotFoundException ex)
        {
            return CommandResult.Failed($"File not found: {ex.FileName}");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult.Failed("Access denied.");
        }
        catch (Exception ex)
        {
            return CommandResult.Failed($"Error reading file: {ex.Message}");
        }
    }
}
```

---

## Command Discovery and Registration

Commands are discovered through their parent components:

**ComponentCommand Creation** (`src/Visora.Contracts/Components/VisoraComponent.cs:35-37`)

```csharp
public abstract class VisoraComponent
{
    // ... other members ...
    
    /// <summary>
    /// Create commands provided by this component.
    /// Called during component inspection.
    /// </summary>
    public virtual IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
        => Array.Empty<VisoraCommand>();
}
```

**Example Component with Commands**:

```csharp
public sealed class CoreUtilitiesComponent : Component
{
    private static readonly ComponentDescriptor Info = 
        ComponentDescriptor.Create(
            id: "visora.shell.commands.core.utilities",
            name: "Core Utilities",
            kind: ComponentKind.Console);

    public override ComponentDescriptor Descriptor => Info;

    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new PingCommand();
        yield return new EnvironmentInfoCommand();
        yield return new ModuleProbeCommand();
    }
}
```

**Inspection Flow** (`src/Visora.Core/Modules/ModuleHandle.cs:94-135`):

```csharp
public async Task<ModuleInspection> InspectAsync(CancellationToken cancellationToken = default)
{
    await EnsureInitializedAsync(cancellationToken);

    var discoveryContext = new ModuleDiscoveryContext(Assembly);
    var componentTypes = Module.DiscoverComponents(discoveryContext).ToArray();
    var inspections = new List<ComponentInspection>();

    foreach (var componentType in componentTypes)
    {
        // Instantiate component
        if (Activator.CreateInstance(componentType) is not VisoraComponent component)
            continue;

        await component.InitializeAsync(componentContext, cancellationToken);

        var commandInfos = new List<CommandInspection>();

        // Discover commands from component
        foreach (var command in component.CreateCommands(componentContext) ?? Array.Empty<VisoraCommand>())
        {
            if (command is null) continue;

            commandInfos.Add(new CommandInspection(
                command.Descriptor, 
                command.GetType(), 
                componentType));
            
            // Dispose command after inspection
            if (command is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (command is IDisposable disposable)
                disposable.Dispose();
        }

        inspections.Add(new ComponentInspection(componentType, component.Descriptor, commandInfos));
    }

    return new ModuleInspection(AssemblyPath, Descriptor, inspections);
}
```

---

## Command Execution Flow

1. **Discovery**: Module inspection finds commands via `CreateCommands()`
2. **Registration**: Host registers commands (in registry, menu, CLI parser)
3. **Invocation**: User/system invokes command by ID
4. **Context Creation**: Host builds `CommandContext` with surface, capabilities, parameters
5. **Execution**: `ExecuteAsync()` called with context and cancellation token
6. **Result Handling**: Host processes `CommandResult` based on outcome

---

## Testing Commands

### Unit Testing Example

```csharp
[TestClass]
public class PingCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_ReturnsSuccessWithTimestamp()
    {
        // Arrange
        var moduleDescriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0));
        
        var mockModule = new Mock<VisoraModule>();
        mockModule.Setup(m => m.Descriptor).Returns(moduleDescriptor);
        
        var context = new CommandContext(
            module: mockModule.Object,
            component: null,
            surface: CommandSurface.Programmatic,
            capabilities: CapabilityProviders.Empty,
            parameters: new Dictionary<string, object?>());
        
        var command = new PingCommand();

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Message);
        Assert.IsNotNull(result.Payload);
        
        // Verify payload structure
        dynamic payload = result.Payload!;
        Assert.IsNotNull(payload.Timestamp);
        Assert.AreEqual(CommandSurface.Programmatic, payload.Surface);
    }

    [TestMethod]
    public async Task ExecuteAsync_SupportsCancellation()
    {
        // Arrange
        var command = new LongRunningCommand();
        var cts = new CancellationTokenSource();
        var context = CreateTestContext();

        // Act
        var task = command.ExecuteAsync(context, cts.Token);
        cts.Cancel();
        var result = await task;

        // Assert
        Assert.AreEqual(CommandOutcome.Cancelled, result.Outcome);
    }
}
```

---

## Best Practices

### Command Design

1. **Keep Commands Focused**: Each command should do one thing well
2. **Descriptive IDs**: Use hierarchical IDs like `<subsystem>.<verb>[.<object>]`
3. **Rich Metadata**: Provide description, keywords for discoverability
4. **Cancellation Support**: Always respect `CancellationToken`
5. **Stateless**: Commands should be stateless or use context for state

### Error Handling

1. **Use Results for Expected Failures**: File not found, validation errors
2. **Use Exceptions for Unexpected Failures**: Programming errors, infrastructure failures
3. **Clear Error Messages**: User-facing, actionable messages
4. **Include Context**: What failed, why, what to do next

### Async Patterns

1. **Use `ValueTask`**: For potentially synchronous paths
2. **Async All the Way**: Don't block on async operations
3. **Check Cancellation**: Before expensive operations
4. **Proper Disposal**: Dispose resources in finally blocks or using statements

### Parameter Handling

```csharp
// Type-safe parameter access
if (context.Parameters.TryGetValue("count", out var countObj) && countObj is int count)
{
    // Use count
}
else
{
    return CommandResult.Failed("Parameter 'count' must be an integer.");
}

// Or use helper methods
private static T GetRequiredParameter<T>(CommandContext context, string name)
{
    if (context.Parameters.TryGetValue(name, out var value) && value is T typed)
        return typed;
    throw new InvalidOperationException($"Required parameter '{name}' of type {typeof(T)} not found.");
}
```

---

## Related Patterns

- **[Multi-Surface Execution](../multi-surface-execution/visora-analysis.md)**: How commands adapt to different surfaces
- **[Result Objects](../result-objects/visora-analysis.md)**: Deep dive on CommandResult pattern
- **[Context Objects](../context-objects/visora-analysis.md)**: Understanding CommandContext
- **[Async Patterns](../async-patterns/visora-analysis.md)**: Async/await best practices

---

## Summary

The Command Execution Pattern in VISORA provides:
- ✅ Uniform abstraction for operations across surfaces
- ✅ Rich metadata for discovery and registration
- ✅ Result objects for clean error handling
- ✅ Async-first design with cancellation support
- ✅ Testable through interface abstraction

**Key Takeaway**: Commands are the universal interaction fabric in VISORA, enabling the same logic to run from CLI, UI, automation, or remote invocation.
