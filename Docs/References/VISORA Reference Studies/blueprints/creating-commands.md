# Creating VISORA Commands - Step-by-Step Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Skill Level:** Beginner to Intermediate
**Estimated Time:** 15-30 minutes

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Command Fundamentals](#command-fundamentals)
4. [Step-by-Step Implementation](#step-by-step-implementation)
5. [Command Descriptor Design](#command-descriptor-design)
6. [ExecuteAsync Implementation](#executeasync-implementation)
7. [Command Context](#command-context)
8. [Result and Error Handling](#result-and-error-handling)
9. [Parameter Handling](#parameter-handling)
10. [Testing Commands](#testing-commands)
11. [Common Mistakes](#common-mistakes)
12. [Advanced Topics](#advanced-topics)
13. [Best Practices](#best-practices)
14. [Reference Examples](#reference-examples)
15. [Cross-References](#cross-references)

---

## Overview

### What is a VISORA Command?

A **command** is the smallest unit of executable functionality in VISORA. Commands:

- Encapsulate a single action or operation
- Receive input through `CommandContext`
- Return results through `CommandResult`
- Can be invoked from CLI, UI, or programmatically
- Are registered by components

**Key Characteristics:**
- **Stateless**: Commands should not maintain state between executions
- **Idempotent**: Same input should produce same output (where applicable)
- **Async**: All execution is asynchronous
- **Surface-agnostic**: Can run in CLI, Terminal UI, or WPF

### Command Architecture

```
Component
  └─ CreateCommands()
       └─ PingCommand
            ├─ CommandDescriptor (metadata)
            │    ├─ Id, Title, Description
            │    ├─ Kind, Keywords, Aliases
            │    └─ UI Hints (menu path, icon)
            └─ ExecuteAsync(context, cancellationToken)
                 ├─ Read parameters from context
                 ├─ Access capabilities from context
                 ├─ Perform operation
                 └─ Return CommandResult
```

### Command Lifecycle

```
User triggers command
    ↓
Host resolves command by ID
    ↓
Host creates CommandContext
    ↓
Host calls ExecuteAsync(context, cancellationToken)
    ↓
Command executes
    ↓
Command returns CommandResult
    ↓
Host processes result (display, log, etc.)
```

---

## Prerequisites

### Required Knowledge

- **Components**: Understanding of VISORA components ([Creating Components](./creating-components.md))
- **C# fundamentals**: Classes, async/await, records
- **Async programming**: ValueTask, CancellationToken

### Required References

Commands are defined within module projects:

```xml
<ItemGroup>
  <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
</ItemGroup>
```

---

## Command Fundamentals

### The Command Base Class

**File**: `/src/Visora.Contracts/Commands/VisoraCommand.cs`

```csharp
public abstract class VisoraCommand
{
    // REQUIRED: Metadata describing this command
    public abstract CommandDescriptor Descriptor { get; }

    // REQUIRED: Execute the command
    public virtual ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CommandResult.Success());
}
```

**Minimal command:**

```csharp
public sealed class HelloCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "hello",
        title: "Hello");

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(
            CommandResult.Success("Hello, World!"));
    }
}
```

### Command Discovery and Registration

Commands are registered by components:

```
Module loaded
    ↓
Components discovered and initialized
    ↓
Component.CreateCommands() called
    ↓
Commands returned to host
    ↓
Host registers commands in command registry
    ↓
Commands available for execution
```

---

## Step-by-Step Implementation

### Step 1: Create Command Class

In your module project, create a new file:

**File**: `Commands/StatusCommand.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Commands;

namespace MyFeature.Module.Commands;

/// <summary>
/// Displays the current status of MyFeature.
/// </summary>
public sealed class StatusCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "myfeature.status",
        title: "MyFeature Status",
        description: "Displays the current status and configuration.",
        kind: CommandKind.Tool,
        keywords: new[] { "status", "info" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement command logic
        return ValueTask.FromResult(CommandResult.Success("Status: OK"));
    }
}
```

### Step 2: Register Command in Component

Add the command to your component's `CreateCommands()`:

```csharp
// In MyFeatureComponent.cs
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new StatusCommand();
    yield return new ConfigureCommand();
    // ... other commands
}
```

### Step 3: Implement Command Logic

Add the actual implementation:

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Build status information
    var status = new
    {
        IsConfigured = CheckConfiguration(),
        LastRun = GetLastRunTime(),
        ItemCount = GetItemCount()
    };

    var message = $"MyFeature is {(status.IsConfigured ? "configured" : "not configured")}";

    return ValueTask.FromResult(
        CommandResult.Success(message, status));
}
```

### Step 4: Build and Test

```bash
dotnet build
```

Test the command:

```csharp
var command = new StatusCommand();
var context = CreateTestContext();
var result = await command.ExecuteAsync(context);

Assert.True(result.IsSuccess);
```

---

## Command Descriptor Design

### CommandDescriptor Structure

**File**: `/src/Visora.Contracts/Commands/CommandDescriptor.cs`

```csharp
public sealed record CommandDescriptor(
    string Id,
    string Title,
    string? Description = null,
    CommandKind Kind = CommandKind.General,
    IReadOnlyCollection<string>? Aliases = null,
    IReadOnlyCollection<string>? Keywords = null,
    bool IsVisible = true,
    bool IsInstanceScoped = false,
    CommandUiHint? Ui = null);

public enum CommandKind
{
    General,      // General-purpose command
    Navigation,   // Navigation/switching command
    Tool,         // Diagnostic or utility tool
    Shell,        // Shell/terminal command
    Automation    // Automation/scripting command
}

public sealed record CommandUiHint(
    string? MenuPath = null,
    string? Icon = null,
    string? DefaultGesture = null);
```

### Descriptor Fields Explained

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| **Id** | ✅ Yes | Unique identifier | `"shell.ping"` |
| **Title** | ✅ Yes | Display name | `"Ping"` |
| **Description** | ❌ No | Detailed description | `"Checks connectivity..."` |
| **Kind** | ❌ No | Command category | `CommandKind.Tool` |
| **Aliases** | ❌ No | Alternative names | `["p", "test-connection"]` |
| **Keywords** | ❌ No | Search keywords | `["diagnostics", "ping"]` |
| **IsVisible** | ❌ No | Show in UI/menus | `true` (default) |
| **IsInstanceScoped** | ❌ No | Per-instance command | `false` (default) |
| **Ui** | ❌ No | UI presentation hints | See below |

### Naming Your Command (ID Field)

Commands use dot-notation similar to modules and components:

```
<scope>.<action>[.<variant>]
```

**Examples:**

```csharp
// Good examples
id: "shell.ping"                    // Shell ping command
id: "git.commit"                    // Git commit
id: "git.commit.amend"              // Git commit with amend
id: "myfeature.configure"           // MyFeature configuration
id: "diagnostics.network.trace"     // Network trace diagnostic

// Bad examples
❌ "Ping"                           // Not hierarchical
❌ "shell_ping"                     // Wrong separator
❌ "shell.Ping"                     // Wrong casing (use lowercase)
```

**Naming Rules:**
- Use **lowercase** for all segments
- Use **dots** as separators
- Start with scope (module/feature name)
- Follow with action verb
- Optional variant/specialization

### Command Kinds

Choose the appropriate `CommandKind`:

| Kind | Use Case | Example |
|------|----------|---------|
| **General** | Default, general-purpose | Utility commands |
| **Navigation** | Movement/switching | "Go to definition" |
| **Tool** | Diagnostics/utilities | Ping, environment info |
| **Shell** | Shell/terminal commands | Run script, execute |
| **Automation** | Scripting/automation | Batch operations |

### Aliases

Provide alternative names for convenience:

```csharp
CommandDescriptor.Create(
    id: "myfeature.status",
    title: "MyFeature Status",
    aliases: new[] { "status", "stat", "mf-status" })
```

Users can invoke via any alias:
- `myfeature.status` (full ID)
- `status` (alias)
- `stat` (alias)
- `mf-status` (alias)

### Keywords for Search

Help users find commands:

```csharp
keywords: new[] { "status", "info", "diagnostics", "health" }
```

### UI Hints

Specify how the command appears in UI:

```csharp
CommandDescriptor.Create(
    id: "myfeature.configure",
    title: "Configure MyFeature",
    ui: new CommandUiHint(
        menuPath: "Tools/MyFeature/Configure",
        icon: "settings.png",
        defaultGesture: "Ctrl+Shift+M"))
```

**UI Hint Fields:**
- **MenuPath**: Where command appears in menus (e.g., `"Tools/MyFeature/Configure"`)
- **Icon**: Icon resource name
- **DefaultGesture**: Keyboard shortcut (e.g., `"Ctrl+Shift+M"`)

### Example from VISORA Codebase

**File**: `src/Visora.Shell.Commands.Core/Commands/PingCommand.cs:10-15`

```csharp
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping",
    description: "Checks connectivity with the Visora host.",
    kind: CommandKind.Automation,
    keywords: new[] { "diagnostics", "ping" });
```

---

## ExecuteAsync Implementation

### Method Signature

```csharp
public virtual ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
```

### Return Type: ValueTask

Commands use `ValueTask<CommandResult>` for performance:

```csharp
// Synchronous result (no async work)
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    var result = ComputeResult();
    return ValueTask.FromResult(result);
}

// Asynchronous result (with async work)
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    var data = await FetchDataAsync(cancellationToken);
    return CommandResult.Success("Done", data);
}
```

### Simple Synchronous Command

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    var now = DateTimeOffset.UtcNow;
    var message = $"Current time: {now:O}";
    var payload = new { Timestamp = now };

    return ValueTask.FromResult(
        CommandResult.Success(message, payload));
}
```

### Asynchronous Command

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Get capability
    var httpClient = context.Capabilities.GetRequired<HttpClient>();

    // Perform async operation
    var response = await httpClient.GetAsync(
        "https://api.example.com/status",
        cancellationToken);

    // Check cancellation
    cancellationToken.ThrowIfCancellationRequested();

    // Read response
    var content = await response.Content.ReadAsStringAsync(cancellationToken);

    return CommandResult.Success("Request completed", new { Content = content });
}
```

### Handling Cancellation

Always respect cancellation tokens:

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Check before starting
    cancellationToken.ThrowIfCancellationRequested();

    // Pass to async operations
    await Step1Async(cancellationToken);

    // Check periodically in loops
    for (int i = 0; i < 100; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ProcessItemAsync(i, cancellationToken);
    }

    return CommandResult.Success("Complete");
}
```

---

## Command Context

### CommandContext Structure

**File**: `/src/Visora.Contracts/Commands/CommandContext.cs`

```csharp
public sealed class CommandContext
{
    // Parent module
    public VisoraModule Module { get; }

    // Parent component
    public VisoraComponent Component { get; }

    // Command descriptor
    public CommandDescriptor Descriptor { get; }

    // Execution surface (CLI, WPF, etc.)
    public Surface Surface { get; }

    // Capability provider (access to host APIs)
    public ICapabilityProvider Capabilities { get; }

    // Command parameters
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    // Host properties
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
```

### Accessing Context Properties

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Get module/component info
    var moduleName = context.Module.Descriptor.Name;
    var componentName = context.Component.Descriptor.Name;

    // Get execution surface
    var surface = context.Surface;
    var isCli = surface == Surface.Cli;

    // Access capabilities
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.LogInformation("Executing {CommandId}", context.Descriptor.Id);

    // Get command parameters
    var name = context.Parameters.GetValueOrDefault("name") as string ?? "World";

    return ValueTask.FromResult(
        CommandResult.Success($"Hello, {name}!"));
}
```

### Execution Surfaces

Commands can adapt behavior based on execution surface:

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    return context.Surface switch
    {
        Surface.Cli => ExecuteForCliAsync(context, cancellationToken),
        Surface.Wpf => ExecuteForWpfAsync(context, cancellationToken),
        Surface.Terminal => ExecuteForTerminalAsync(context, cancellationToken),
        _ => ValueTask.FromResult(
            CommandResult.Failure("Unsupported surface"))
    };
}
```

---

## Result and Error Handling

### CommandResult Structure

```csharp
public sealed class CommandResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }
    public object? Payload { get; }
    public CommandResultKind Kind { get; }
}

public enum CommandResultKind
{
    Success,
    Failure,
    Cancelled,
    NotAvailable
}
```

### Success Results

```csharp
// Simple success
return CommandResult.Success();

// Success with message
return CommandResult.Success("Operation completed");

// Success with message and payload
var data = new { Count = 42, Status = "OK" };
return CommandResult.Success("Operation completed", data);
```

### Failure Results

```csharp
// Simple failure
return CommandResult.Failure();

// Failure with message
return CommandResult.Failure("Invalid input");

// Failure with message and error details
var error = new { ErrorCode = "E001", Details = "..." };
return CommandResult.Failure("Operation failed", error);
```

### Other Result Types

```csharp
// Cancelled
return CommandResult.Cancelled("Operation cancelled by user");

// Not available (e.g., feature not supported on this platform)
return CommandResult.NotAvailable("This command requires Windows");
```

### Error Handling Patterns

**Pattern 1: Return failure result (preferred)**

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    try
    {
        await PerformOperationAsync(cancellationToken);
        return CommandResult.Success("Success");
    }
    catch (InvalidOperationException ex)
    {
        return CommandResult.Failure($"Invalid operation: {ex.Message}");
    }
    catch (Exception ex)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogError(ex, "Command failed");
        return CommandResult.Failure("An error occurred");
    }
}
```

**Pattern 2: Let exceptions propagate (for fatal errors)**

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    // Fatal error - let it propagate
    var required = context.Capabilities.GetRequired<IRequiredService>();

    // Continue with operation
    await required.DoWorkAsync(cancellationToken);
    return CommandResult.Success("Success");
}
```

### Handling Cancellation

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    try
    {
        await LongRunningOperationAsync(cancellationToken);
        return CommandResult.Success("Complete");
    }
    catch (OperationCanceledException)
    {
        return CommandResult.Cancelled("Operation was cancelled");
    }
}
```

---

## Parameter Handling

### Reading Parameters

Parameters are passed via `CommandContext.Parameters`:

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Get required parameter
    if (!context.Parameters.TryGetValue("name", out var nameObj))
    {
        return ValueTask.FromResult(
            CommandResult.Failure("Missing required parameter: name"));
    }
    var name = nameObj as string;

    // Get optional parameter with default
    var count = context.Parameters.GetValueOrDefault("count") as int? ?? 1;

    return ValueTask.FromResult(
        CommandResult.Success($"Hello {name}, count={count}"));
}
```

### Parameter Validation

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // Validate required parameter
    if (!context.Parameters.TryGetValue("url", out var urlObj) ||
        urlObj is not string url ||
        string.IsNullOrWhiteSpace(url))
    {
        return ValueTask.FromResult(
            CommandResult.Failure("Invalid or missing parameter: url"));
    }

    // Validate URL format
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return ValueTask.FromResult(
            CommandResult.Failure($"Invalid URL format: {url}"));
    }

    // Continue with validated parameters
    return ExecuteWithUrlAsync(uri, cancellationToken);
}
```

### Type-Safe Parameter Access

```csharp
private static T? GetParameter<T>(CommandContext context, string key)
{
    if (context.Parameters.TryGetValue(key, out var value) && value is T typed)
    {
        return typed;
    }
    return default;
}

public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    var name = GetParameter<string>(context, "name") ?? "World";
    var count = GetParameter<int>(context, "count");

    return ValueTask.FromResult(
        CommandResult.Success($"Hello {name}, count={count}"));
}
```

---

## Testing Commands

### Unit Testing Setup

```csharp
using Xunit;
using Moq;
using MyFeature.Module.Commands;

public class StatusCommandTests
{
    [Fact]
    public void Command_HasValidDescriptor()
    {
        // Arrange & Act
        var command = new StatusCommand();

        // Assert
        Assert.NotNull(command.Descriptor);
        Assert.Equal("myfeature.status", command.Descriptor.Id);
        Assert.Equal("MyFeature Status", command.Descriptor.Title);
    }
}
```

### Testing Execution

```csharp
[Fact]
public async Task ExecuteAsync_ReturnsSuccess()
{
    // Arrange
    var command = new StatusCommand();
    var context = CreateMockContext();

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Message);
}

private CommandContext CreateMockContext()
{
    var mockModule = new Mock<VisoraModule>();
    var mockComponent = new Mock<VisoraComponent>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    return new CommandContext(
        module: mockModule.Object,
        component: mockComponent.Object,
        descriptor: new StatusCommand().Descriptor,
        surface: Surface.Cli,
        capabilities: mockCapabilities.Object,
        parameters: new Dictionary<string, object?>(),
        properties: new Dictionary<string, object?>());
}
```

### Testing with Parameters

```csharp
[Theory]
[InlineData("Alice", "Hello Alice")]
[InlineData("Bob", "Hello Bob")]
public async Task ExecuteAsync_WithName_ReturnsGreeting(string name, string expected)
{
    // Arrange
    var command = new GreetCommand();
    var parameters = new Dictionary<string, object?> { ["name"] = name };
    var context = CreateMockContext(parameters);

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Contains(expected, result.Message);
}
```

### Testing Error Cases

```csharp
[Fact]
public async Task ExecuteAsync_WithMissingParameter_ReturnsFailure()
{
    // Arrange
    var command = new RequiresParameterCommand();
    var context = CreateMockContext(); // No parameters

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Missing required parameter", result.Message);
}
```

### Testing Cancellation

```csharp
[Fact]
public async Task ExecuteAsync_WithCancellation_ReturnsCancelled()
{
    // Arrange
    var command = new LongRunningCommand();
    var context = CreateMockContext();
    var cts = new CancellationTokenSource();

    // Act
    var task = command.ExecuteAsync(context, cts.Token);
    cts.Cancel();
    var result = await task;

    // Assert
    Assert.Equal(CommandResultKind.Cancelled, result.Kind);
}
```

---

## Common Mistakes

### Mistake 1: Blocking ExecuteAsync

**Problem:**
```csharp
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    Thread.Sleep(1000);  ❌  // Blocking
    return ValueTask.FromResult(CommandResult.Success());
}
```

**Solution:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    await Task.Delay(1000, cancellationToken);  ✅
    return CommandResult.Success();
}
```

### Mistake 2: Ignoring Cancellation Token

**Problem:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    await LongOperationAsync();  ❌  // Doesn't pass token
    return CommandResult.Success();
}
```

**Solution:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    await LongOperationAsync(cancellationToken);  ✅
    return CommandResult.Success();
}
```

### Mistake 3: Throwing Exceptions for Expected Errors

**Problem:**
```csharp
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    if (!IsValid())
        throw new InvalidOperationException("Invalid");  ❌
}
```

**Solution:**
```csharp
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    if (!IsValid())
        return ValueTask.FromResult(
            CommandResult.Failure("Invalid operation"));  ✅
}
```

### Mistake 4: Mutable Descriptor

**Problem:**
```csharp
public override CommandDescriptor Descriptor =>
    CommandDescriptor.Create(...);  ❌  // Created each time
```

**Solution:**
```csharp
private static readonly CommandDescriptor Info =
    CommandDescriptor.Create(...);  ✅  // Created once

public override CommandDescriptor Descriptor => Info;
```

### Mistake 5: Stateful Commands

**Problem:**
```csharp
public sealed class StatefulCommand : VisoraCommand
{
    private int _executionCount;  ❌  // State between executions

    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        _executionCount++;  // Bad: state accumulation
        return ValueTask.FromResult(CommandResult.Success());
    }
}
```

**Solution:**
```csharp
public sealed class StatelessCommand : VisoraCommand
{
    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        // All state comes from context, no instance state
        var count = GetCountFromContext(context);
        return ValueTask.FromResult(CommandResult.Success());
    }
}
```

---

## Advanced Topics

### Topic 1: Long-Running Commands

For long operations, report progress:

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    var progress = context.Capabilities.GetOptional<IProgress<double>>();

    for (int i = 0; i < 100; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ProcessItemAsync(i, cancellationToken);
        progress?.Report((i + 1) / 100.0);
    }

    return CommandResult.Success("Complete");
}
```

### Topic 2: Interactive Commands

Commands that need user input:

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    var console = context.Capabilities.GetRequired<IConsole>();

    await console.WriteLineAsync("Enter your name:");
    var name = await console.ReadLineAsync(cancellationToken);

    await console.WriteLineAsync("Confirm? (y/n):");
    var confirm = await console.ReadLineAsync(cancellationToken);

    if (confirm?.ToLower() == "y")
    {
        return CommandResult.Success($"Confirmed for {name}");
    }

    return CommandResult.Cancelled("User cancelled");
}
```

### Topic 3: Commands with Side Effects

Track side effects in result payload:

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    var fileSystem = context.Capabilities.GetRequired<IFileSystem>();

    // Perform side effects
    var createdFiles = new List<string>();
    foreach (var file in filesToCreate)
    {
        await fileSystem.WriteAsync(file, content, cancellationToken);
        createdFiles.Add(file);
    }

    // Return side effects in payload
    var payload = new
    {
        CreatedFiles = createdFiles,
        Timestamp = DateTimeOffset.UtcNow
    };

    return CommandResult.Success($"Created {createdFiles.Count} files", payload);
}
```

### Topic 4: Chaining Commands

Commands can invoke other commands:

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    var commandRegistry = context.Capabilities.GetRequired<ICommandRegistry>();

    // Execute prerequisite command
    var setupResult = await commandRegistry.ExecuteAsync(
        "myfeature.setup",
        parameters: new Dictionary<string, object?>(),
        cancellationToken);

    if (!setupResult.IsSuccess)
    {
        return CommandResult.Failure("Setup failed");
    }

    // Continue with main operation
    return CommandResult.Success("Complete");
}
```

---

## Best Practices

### 1. Keep Commands Stateless

```csharp
// Good: Stateless
public sealed class GoodCommand : VisoraCommand
{
    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        var data = context.Parameters["data"];
        // Process data
        return ValueTask.FromResult(CommandResult.Success());
    }
}

// Bad: Stateful
public sealed class BadCommand : VisoraCommand
{
    private object? _lastData;  ❌

    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        _lastData = context.Parameters["data"];
        return ValueTask.FromResult(CommandResult.Success());
    }
}
```

### 2. Validate Input Early

```csharp
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    // Validate all inputs at the start
    if (!ValidateParameters(context.Parameters, out var error))
    {
        return ValueTask.FromResult(CommandResult.Failure(error));
    }

    // Continue with validated inputs
    return PerformOperationAsync(context, cancellationToken);
}
```

### 3. Use Descriptive Error Messages

```csharp
// Good
return CommandResult.Failure(
    "Invalid URL format. Expected 'https://...' but got 'htp://...'");

// Bad
return CommandResult.Failure("Error");  ❌
```

### 4. Handle Cancellation Gracefully

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    try
    {
        await OperationAsync(cancellationToken);
        return CommandResult.Success();
    }
    catch (OperationCanceledException)
    {
        // Clean up partial work
        await CleanupAsync();
        return CommandResult.Cancelled("Operation cancelled");
    }
}
```

### 5. Leverage Capabilities

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    // Use optional capabilities defensively
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.LogInformation("Starting command");

    // Use required capabilities with clear errors
    var fileSystem = context.Capabilities.GetRequired<IFileSystem>();

    return CommandResult.Success();
}
```

---

## Reference Examples

### Example 1: Simple Ping Command

**File**: `src/Visora.Shell.Commands.Core/Commands/PingCommand.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Commands;

namespace Visora.Shell.Commands.Core.Commands;

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

### Example 2: Environment Info Command

**File**: `src/Visora.Shell.Commands.Core/Commands/EnvironmentInfoCommand.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Commands;

namespace Visora.Shell.Commands.Core.Commands;

public sealed class EnvironmentInfoCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.env.info",
        title: "Environment Info",
        description: "Returns process and environment details useful for diagnostics.",
        kind: CommandKind.Tool,
        keywords: new[] { "environment", "diagnostics" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["ProcessId"] = Environment.ProcessId,
            ["MachineName"] = Environment.MachineName,
            ["CurrentDirectory"] = Environment.CurrentDirectory,
            ["BaseDirectory"] = AppContext.BaseDirectory,
            ["OSVersion"] = Environment.OSVersion.ToString(),
            ["CommandSurface"] = context.Surface.ToString()
        };

        var message = $"Process {Environment.ProcessId} on {Environment.MachineName}, " +
                      $"cwd '{Environment.CurrentDirectory}'.";

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

---

## Cross-References

### Related Documentation

- **[Creating Components](./creating-components.md)**: Command registration
- **[Creating Modules](./creating-modules.md)**: Module fundamentals
- **[Testing Strategies](./testing-strategies.md)**: Testing guide
- **[Command Execution Pattern](../patterns/command-execution/visora-analysis.md)**: Deep dive
- **[Result Objects Pattern](../patterns/result-objects/visora-analysis.md)**: CommandResult design
- **[Context Objects Pattern](../patterns/context-objects/visora-analysis.md)**: CommandContext
- **[Naming Conventions](../conventions/naming-conventions.md)**: Command naming

### Related Decisions

- **[ADR-003: Async Everywhere](../decisions/async-everywhere.md)**: Why ExecuteAsync is async
- **[ADR-005: Result Objects Not Exceptions](../decisions/result-objects-not-exceptions.md)**: CommandResult pattern

---

**End of Document**
