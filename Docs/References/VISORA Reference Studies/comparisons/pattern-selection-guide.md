# VISORA Pattern Selection Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Audience:** Architects and developers choosing patterns

---

## Table of Contents

1. [Overview](#overview)
2. [Decision Trees](#decision-trees)
3. [Scenario-Based Recommendations](#scenario-based-recommendations)
4. [Pattern Tradeoff Matrices](#pattern-tradeoff-matrices)
5. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
6. [Common Scenarios](#common-scenarios)
7. [Performance Considerations](#performance-considerations)
8. [Complexity vs Benefit](#complexity-vs-benefit)
9. [Meta-Platform Adaptation Guide](#meta-platform-adaptation-guide)
10. [Quick Reference](#quick-reference)
11. [Cross-References](#cross-references)

---

## Overview

### Purpose of This Guide

This guide helps you choose the right VISORA patterns for your specific needs. It provides:

- **Decision trees**: Step-by-step pattern selection
- **Scenario-based recommendations**: Common use cases
- **Tradeoff matrices**: Compare patterns across dimensions
- **Anti-patterns**: What to avoid
- **Performance considerations**: Speed vs complexity

### Pattern Categories

VISORA patterns fall into these categories:

| Category | Patterns | Purpose |
|----------|----------|---------|
| **Foundation** | Plugin Architecture, Capability Negotiation, Reflection Discovery, Module Lifecycle, Context Objects | Core platform patterns |
| **Communication** | Command Execution, Async Patterns, Result Objects, Multi-Surface Execution | Module ↔ Host communication |
| **Data** | Immutable Metadata, Factory Patterns, Builder Pattern | Data modeling |
| **Structural** | Layered Architecture, Registry Pattern, Template Method | Code organization |
| **Quality** | Testable Design | Testing and quality |

---

## Decision Trees

### Tree 1: Choosing the Right Abstraction Level

```
START: What are you building?

├─ Plugin/Extension System
│  └─ Use: Module + Component + Command pattern
│     - Module: Top-level container
│     - Component: Logical grouping
│     - Command: Executable actions
│
├─ Host Application
│  └─ Use: ModuleCatalog + Capability Provider
│     - ModuleCatalog: Discover and load modules
│     - Capability Provider: Expose host APIs
│
├─ Reusable Library (not a plugin)
│  └─ Use: Standard .NET library patterns
│     - NOT a VISORA module
│     - Use traditional NuGet packages
│
└─ Standalone Application (no plugins)
   └─ Use: Standard application patterns
      - VISORA not needed
      - Use framework-specific patterns
```

### Tree 2: Module Organization

```
START: How should I organize my module?

├─ Single cohesive feature
│  └─ 1 Module + 1 Component + N Commands
│     Example: "Git Integration" module
│
├─ Multiple related features
│  └─ 1 Module + N Components + M Commands
│     Example: "Developer Tools" module
│        - Component: Code Analysis
│        - Component: Refactoring
│        - Component: Diagnostics
│
└─ Large subsystem
   └─ N Modules (separate .vixm.dll files)
      Example: "Database Tools" subsystem
         - Module: SQL Server Integration
         - Module: PostgreSQL Integration
         - Module: MongoDB Integration
```

### Tree 3: State Management

```
START: Where should state live?

├─ Configuration (loaded once)
│  └─ Store in: Module
│     Load in: InitializeAsync
│     Example: Connection strings, settings
│
├─ Session state (per activation)
│  └─ Store in: Component
│     Create in: ActivateAsync
│     Dispose in: DeactivateAsync
│     Example: UI state, active connections
│
├─ Per-execution state
│  └─ Store in: Local variables
│     Create in: ExecuteAsync
│     Dispose: End of method
│     Example: Command parameters, temp data
│
└─ Long-lived state
   └─ Store in: External system
      Access via: Capabilities
      Example: Database, file system, cache
```

### Tree 4: Capability vs DI Container

```
START: How should dependencies be managed?

├─ Crossing host ↔ module boundary?
│  └─ Use: Capability Provider
│     - Host exposes APIs to modules
│     - Modules request capabilities
│
├─ Within module internals?
│  └─ Use: DI Container (optional)
│     - Module manages internal dependencies
│     - Can use ASP.NET Core DI, Autofac, etc.
│
└─ Within host internals?
   └─ Use: DI Container (recommended)
      - Host manages internal dependencies
      - Exposes selected services as capabilities
```

---

## Scenario-Based Recommendations

### Scenario 1: Simple CLI Tool Module

**Use Case**: Add a "ping" command to check host connectivity

**Recommended Patterns**:
- ✅ Module with single component
- ✅ Simple command with no parameters
- ✅ Result objects for success/failure
- ✅ Optional capability (logger)

**Implementation**:
```csharp
// 1 Module
public sealed class ToolsModule : Module { }

// 1 Component
public sealed class DiagnosticsComponent : Component
{
    public override IEnumerable<VisoraCommand> CreateCommands(...)
    {
        yield return new PingCommand();
    }
}

// 1 Command
public sealed class PingCommand : VisoraCommand
{
    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        return ValueTask.FromResult(CommandResult.Success("Pong"));
    }
}
```

**Complexity**: Low (1/5)
**Development Time**: 15 minutes

---

### Scenario 2: Multi-Feature Development Tools

**Use Case**: Developer productivity tools (formatting, linting, refactoring)

**Recommended Patterns**:
- ✅ 1 Module, multiple components
- ✅ Component per feature area
- ✅ Shared configuration in module
- ✅ Factory pattern for tool instances

**Implementation**:
```csharp
// 1 Module
public sealed class DevToolsModule : Module
{
    private Configuration? _config;

    public override async ValueTask InitializeAsync(...)
    {
        _config = await LoadConfigurationAsync();
    }
}

// 3 Components (one per feature)
public sealed class FormattingComponent : Component { }
public sealed class LintingComponent : Component { }
public sealed class RefactoringComponent : Component { }

// Each component has multiple commands
public sealed class FormatDocumentCommand : VisoraCommand { }
public sealed class FormatSelectionCommand : VisoraCommand { }
```

**Complexity**: Medium (3/5)
**Development Time**: 2-4 hours

---

### Scenario 3: Integration with External Service

**Use Case**: GitHub integration (repos, issues, PRs)

**Recommended Patterns**:
- ✅ Module with background initialization
- ✅ HttpClient capability for API calls
- ✅ Async patterns throughout
- ✅ Result objects with detailed payloads
- ✅ Cancellation token support

**Implementation**:
```csharp
public sealed class GitHubModule : Module
{
    private HttpClient? _httpClient;
    private string? _apiToken;

    public override async ValueTask InitializeAsync(...)
    {
        _httpClient = context.Capabilities.GetRequired<HttpClient>();
        _apiToken = await LoadApiTokenAsync(cancellationToken);
    }
}

public sealed class GitHubCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(...)
    {
        var httpClient = context.Capabilities.GetRequired<HttpClient>();

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            var data = await response.Content.ReadAsStringAsync(cancellationToken);
            return CommandResult.Success("Retrieved", data);
        }
        catch (HttpRequestException ex)
        {
            return CommandResult.Failure($"API error: {ex.Message}");
        }
    }
}
```

**Complexity**: Medium-High (4/5)
**Development Time**: 4-8 hours

---

### Scenario 4: UI Component with State

**Use Case**: Settings panel with live updates

**Recommended Patterns**:
- ✅ Component with activate/deactivate lifecycle
- ✅ State stored in component
- ✅ Surface-specific behavior
- ✅ Observer pattern for live updates

**Implementation**:
```csharp
public sealed class SettingsPanelComponent : Component
{
    private Timer? _refreshTimer;
    private Settings? _currentSettings;

    public override async ValueTask ActivateAsync(...)
    {
        // Load settings
        _currentSettings = await LoadSettingsAsync(cancellationToken);

        // Start refresh timer
        _refreshTimer = new Timer(
            callback: _ => RefreshSettings(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(5));
    }

    public override ValueTask DeactivateAsync(...)
    {
        // Stop refresh
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        return ValueTask.CompletedTask;
    }
}
```

**Complexity**: High (4/5)
**Development Time**: 4-6 hours

---

### Scenario 5: Multi-Surface Command

**Use Case**: Command that runs in CLI, Terminal, and WPF

**Recommended Patterns**:
- ✅ Multi-surface execution pattern
- ✅ Surface-specific behavior
- ✅ Context objects for surface info
- ✅ Adaptive UI (if applicable)

**Implementation**:
```csharp
public sealed class SearchCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(...)
    {
        return context.Surface switch
        {
            Surface.Cli => await ExecuteForCliAsync(context, cancellationToken),
            Surface.Terminal => await ExecuteForTerminalAsync(context, cancellationToken),
            Surface.Wpf => await ExecuteForWpfAsync(context, cancellationToken),
            _ => CommandResult.Failure("Unsupported surface")
        };
    }

    private async ValueTask<CommandResult> ExecuteForCliAsync(...)
    {
        // CLI-specific: Simple text output
        var results = await SearchAsync(query, cancellationToken);
        return CommandResult.Success(string.Join("\n", results));
    }

    private async ValueTask<CommandResult> ExecuteForWpfAsync(...)
    {
        // WPF-specific: Show dialog
        var dialogService = context.Capabilities.GetRequired<IDialogService>();
        await dialogService.ShowSearchResultsAsync(results);
        return CommandResult.Success("Displayed in dialog");
    }
}
```

**Complexity**: High (5/5)
**Development Time**: 6-10 hours

---

## Pattern Tradeoff Matrices

### Matrix 1: Module Organization

| Pattern | Cohesion | Flexibility | Deployment | Complexity |
|---------|----------|-------------|------------|------------|
| **Monolithic Module** (1 big module) | Low | Low | Easy | Low |
| **Feature Modules** (1 module per feature) | High | High | Complex | Medium |
| **Hybrid** (related features grouped) | Medium | Medium | Medium | Medium |

**Recommendation**: Start with hybrid (1 module, multiple components), split into separate modules only when needed.

---

### Matrix 2: State Management

| Location | Lifetime | Sharing | Thread Safety | Complexity |
|----------|----------|---------|---------------|------------|
| **Module** | Module lifetime | All components | Must manage | Medium |
| **Component** | Activation cycle | Single component | Must manage | Low |
| **Command** | Execution only | Single execution | Not needed | Low |
| **External** | Application lifetime | All modules | Handled externally | High |

**Recommendation**: Prefer component or command-scoped state. Use module state for shared configuration only.

---

### Matrix 3: Capability vs Service Injection

| Aspect | Capability Provider | DI Container |
|--------|---------------------|--------------|
| **Boundary** | Host ↔ Module | Within host/module |
| **Coupling** | Low | Medium |
| **Testability** | High | High |
| **Complexity** | Low | Medium-High |
| **Optional deps** | Easy | Requires special handling |
| **Performance** | Fast | Slower |

**Recommendation**: Use capabilities for host ↔ module boundary, DI for internal dependencies.

---

### Matrix 4: Async vs Sync

| Operation | Async Preferred | Sync Acceptable | Rationale |
|-----------|----------------|-----------------|-----------|
| **Module Init** | ✅ Always | ❌ Never | May load files, connect to services |
| **Component Init** | ✅ Always | ❌ Never | May load resources |
| **Command Exec** | ✅ Usually | ⚠️ Rare | Most commands do I/O |
| **Descriptor** | ❌ Never | ✅ Always | Descriptors are static metadata |

**Recommendation**: Use async everywhere except descriptors.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: God Module

**Problem**: Single module with dozens of unrelated features

```csharp
// ❌ Bad: God Module
public sealed class EverythingModule : Module
{
    // 50+ components with unrelated features
}
```

**Solution**: Split into focused modules

```csharp
// ✅ Good: Focused Modules
public sealed class GitIntegrationModule : Module { }
public sealed class DatabaseToolsModule : Module { }
public sealed class EditorExtensionsModule : Module { }
```

---

### Anti-Pattern 2: Stateful Commands

**Problem**: Commands that maintain state between executions

```csharp
// ❌ Bad: Stateful Command
public sealed class CounterCommand : VisoraCommand
{
    private int _executionCount;  // ❌ State

    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        _executionCount++;  // ❌ Mutating state
        return ValueTask.FromResult(
            CommandResult.Success($"Count: {_executionCount}"));
    }
}
```

**Solution**: Store state externally or in component

```csharp
// ✅ Good: Stateless Command
public sealed class CounterCommand : VisoraCommand
{
    public override ValueTask<CommandResult> ExecuteAsync(...)
    {
        // Get count from external source
        var storage = context.Capabilities.GetRequired<IStateStorage>();
        var count = storage.GetCount();
        storage.IncrementCount();

        return ValueTask.FromResult(
            CommandResult.Success($"Count: {count}"));
    }
}
```

---

### Anti-Pattern 3: Service Locator in Commands

**Problem**: Storing context and requesting capabilities throughout execution

```csharp
// ❌ Bad: Service Locator
public sealed class BadCommand : VisoraCommand
{
    private CommandContext? _context;  // ❌ Storing context

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, ...)
    {
        _context = context;  // ❌ Storing for later use
        return DoWorkAsync();
    }

    private async ValueTask<CommandResult> DoWorkAsync()
    {
        // ❌ Retrieving capability in nested method
        var logger = _context!.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Working");
        return CommandResult.Success();
    }
}
```

**Solution**: Retrieve capabilities once at the start

```csharp
// ✅ Good: Retrieve Once
public sealed class GoodCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, ...)
    {
        // ✅ Retrieve capability once
        var logger = context.Capabilities.GetOptional<ILogger>();

        // ✅ Pass as parameter
        return await DoWorkAsync(logger, cancellationToken);
    }

    private async ValueTask<CommandResult> DoWorkAsync(
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("Working");
        return CommandResult.Success();
    }
}
```

---

### Anti-Pattern 4: Blocking Async Methods

**Problem**: Using blocking calls in async methods

```csharp
// ❌ Bad: Blocking
public override async ValueTask InitializeAsync(...)
{
    var data = File.ReadAllText("config.json");  // ❌ Blocking
    Thread.Sleep(1000);  // ❌ Blocking
}
```

**Solution**: Use async APIs

```csharp
// ✅ Good: Async
public override async ValueTask InitializeAsync(...)
{
    var data = await File.ReadAllTextAsync("config.json", cancellationToken);  // ✅
    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);  // ✅
}
```

---

### Anti-Pattern 5: Ignoring Cancellation

**Problem**: Not respecting cancellation tokens

```csharp
// ❌ Bad: Ignoring Cancellation
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    for (int i = 0; i < 1000; i++)
    {
        await ProcessItemAsync(i);  // ❌ No cancellation
    }
}
```

**Solution**: Pass and check cancellation token

```csharp
// ✅ Good: Respecting Cancellation
public override async ValueTask<CommandResult> ExecuteAsync(...)
{
    for (int i = 0; i < 1000; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();  // ✅
        await ProcessItemAsync(i, cancellationToken);  // ✅
    }
}
```

---

## Common Scenarios

### Scenario Matrix

| Scenario | Module Count | Component Count | Command Count | Complexity |
|----------|--------------|-----------------|---------------|------------|
| **Simple CLI tool** | 1 | 1 | 1-3 | Low |
| **Dev tools suite** | 1 | 3-5 | 10-20 | Medium |
| **External integration** | 1 | 2-3 | 5-10 | Medium |
| **UI subsystem** | 1-2 | 5-10 | 20-50 | High |
| **Full IDE platform** | 10+ | 50+ | 200+ | Very High |

---

## Performance Considerations

### Performance vs Complexity

| Pattern | Runtime Cost | Memory Cost | Complexity | Use When |
|---------|--------------|-------------|------------|----------|
| **Single Module** | Low | Low | Low | Small project |
| **Multiple Modules** | Medium | Medium | Medium | Organized features |
| **Lazy Loading** | Low (deferred) | Low (deferred) | Medium | Large modules |
| **Hot-Swapping** | Medium | Medium | High | Development/debugging |

### Optimization Tips

1. **Lazy initialization**: Don't load resources until needed
2. **Async all the way**: Avoid blocking calls
3. **Minimal descriptors**: Keep metadata small and simple
4. **Cache lookups**: Store capability references, don't re-query
5. **Profile first**: Measure before optimizing

---

## Meta-Platform Adaptation Guide

### Adapting VISORA Patterns to Other Languages

| VISORA Pattern | Python Equivalent | Node.js Equivalent |
|----------------|-------------------|-------------------|
| **Module** | Package with `__init__.py` | NPM package with index.js |
| **Component** | Class with decorator | ES6 class with metadata |
| **Command** | Function/class | Function/class |
| **Descriptor** | Dataclass/NamedTuple | Object literal |
| **Capability Provider** | Dict or Protocol | Map or duck typing |
| **AssemblyLoadContext** | importlib | require() / dynamic import |

### Python Adaptation Example

```python
# Python adaptation (illustrative)
from dataclasses import dataclass
from typing import Protocol, Optional

@dataclass(frozen=True)
class ModuleDescriptor:
    id: str
    name: str
    version: str

class ICapabilityProvider(Protocol):
    def try_get(self, capability_type: type) -> Optional[object]:
        ...

class VisoraModule:
    @property
    def descriptor(self) -> ModuleDescriptor:
        raise NotImplementedError

    async def initialize_async(self, context: ModuleContext):
        pass

class MyModule(VisoraModule):
    @property
    def descriptor(self) -> ModuleDescriptor:
        return ModuleDescriptor(
            id="mycompany.mymodule",
            name="My Module",
            version="1.0.0")

    async def initialize_async(self, context: ModuleContext):
        logger = context.capabilities.try_get(ILogger)
        if logger:
            logger.info("Initializing")
```

---

## Quick Reference

### Pattern Selection Cheatsheet

| If You Need... | Use This Pattern |
|----------------|------------------|
| Top-level container | **Module** |
| Logical grouping | **Component** |
| Executable action | **Command** |
| Host API exposure | **Capability Provider** |
| Module discovery | **Reflection Discovery** |
| Initialization/cleanup | **Module/Component Lifecycle** |
| Success/failure results | **Result Objects** |
| Async operations | **Async Patterns** |
| Multi-surface support | **Multi-Surface Execution** |
| Immutable config | **Immutable Metadata** |

### When in Doubt

1. **Start simple**: 1 Module, 1 Component, few Commands
2. **Add complexity only when needed**: Don't over-engineer
3. **Follow VISORA examples**: Look at `ShellCommandsModule`
4. **Test early**: Write tests as you build
5. **Refactor later**: It's okay to split modules/components later

---

## Cross-References

### Related Documentation

- **[Creating Modules](../blueprints/creating-modules.md)**: Module implementation
- **[Creating Components](../blueprints/creating-components.md)**: Component implementation
- **[Creating Commands](../blueprints/creating-commands.md)**: Command implementation
- **[VISORA vs Traditional Plugins](./visora-vs-traditional-plugins.md)**: Framework comparison
- **[VISORA vs DI Containers](./visora-vs-di-containers.md)**: Capability vs DI

### Related Patterns

- **[Plugin Architecture](../patterns/plugin-architecture/visora-analysis.md)**
- **[Capability Negotiation](../patterns/capability-negotiation/visora-analysis.md)**
- **[Module Lifecycle](../patterns/module-lifecycle/visora-analysis.md)**
- **[Command Execution](../patterns/command-execution/visora-analysis.md)**

---

**End of Document**
