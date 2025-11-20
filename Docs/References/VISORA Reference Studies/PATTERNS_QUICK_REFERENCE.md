# VISORA ARCHITECTURE - QUICK REFERENCE GUIDE

## Core Patterns at a Glance

### 1. Module-Component-Command Hierarchy
```
Module (VisoraModule)
  ├─ DiscoverComponents() → Component[]
  └─ Component (VisoraComponent)
       └─ CreateCommands() → Command[]
            └─ Command (VisoraCommand)
                 └─ ExecuteAsync() → CommandResult
```

### 2. Context Objects (Dependency Passing)
| Context | Contains | Used By |
|---------|----------|---------|
| `ModuleContext` | Descriptor, Services, Capabilities | Module.InitializeAsync() |
| `ComponentContext` | Module, Capabilities | Component.InitializeAsync() |
| `CommandContext` | Module, Component?, Surface, Capabilities, Parameters | Command.ExecuteAsync() |

### 3. Descriptors (Metadata Records)
- **ModuleDescriptor** - Id, Name, Version, Tags, RuntimeHints
- **ComponentDescriptor** - Id, Name, Kind (Generic/Service/Ui/Console/ShellExtension)
- **CommandDescriptor** - Id, Title, Kind (General/Navigation/Tool/Shell/Automation), UI Hints
- **CommandResult** - Outcome (Success/Cancelled/Failed), Message, Payload

### 4. Capability Provider (Type-Safe DI)
```csharp
// Get optional capability
var service = context.Capabilities.GetOptional<IMyService>();

// Get required capability (throws if missing)
var required = context.Capabilities.GetRequired<IMyService>();

// Build capabilities
var caps = CapabilityProviders.CreateBuilder()
    .Add<IService1>(service1)
    .Add<IService2>(service2)
    .Build();
```

### 5. Module Lifecycle
```
1. LoadAsync(path, options)      ← Assembly loaded, module instantiated
2. EnsureInitializedAsync()      ← Module.InitializeAsync(context)
3. InspectAsync()                 ← Deep component discovery, command enumeration
4. ShutdownAsync()                ← Module.ShutdownAsync(context)
5. DisposeAsync()                 ← Assembly unloaded
```

### 6. Module Catalog (Registry)
```csharp
var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options, ct);        // Scan, load, and register
var module = catalog.GetById("module.id");       // Lookup
await catalog.DisposeAsync();                     // Cleanup all
```

### 7. Command Execution
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context, 
    CancellationToken cancellationToken = default)
{
    try
    {
        var result = await DoWorkAsync(context.Parameters);
        return CommandResult.Success("Done", result);
    }
    catch (OperationCanceledException)
    {
        return CommandResult.Cancelled("User cancelled");
    }
    catch (Exception ex)
    {
        return CommandResult.Failed($"Error: {ex.Message}");
    }
}
```

## File Reference Shortcuts

| Pattern | File | Lines |
|---------|------|-------|
| Module abstraction | `/src/Visora.Contracts/Modules/VisoraModule.cs` | 13-40 |
| Component abstraction | `/src/Visora.Contracts/Components/VisoraComponent.cs` | 12-44 |
| Command abstraction | `/src/Visora.Contracts/Commands/VisoraCommand.cs` | 9-15 |
| Command result | `/src/Visora.Contracts/Commands/CommandResult.cs` | 8-25 |
| Module catalog | `/src/Visora.Core/Modules/ModuleCatalog.cs` | 12-45 |
| Module handle | `/src/Visora.Core/Modules/ModuleHandle.cs` | 20-158 |
| Capability provider | `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 10-70 |
| CLI host | `/src/Visora.CLI/Program.cs` | 25-42 |
| Example module | `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | 8-24 |
| Example component | `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs` | 10-27 |
| Example command | `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` | 8-33 |

## Key Design Principles

1. **Reflection-First Discovery** - No manifests, assembly scanning only
2. **Capability Negotiation** - Modules express requirements, hosts fulfill them
3. **Async/Await Everywhere** - Future-proof for remote execution
4. **Command Results, Not Exceptions** - User-facing operations use result objects
5. **Immutable Descriptors** - Records for metadata, no behavior
6. **Unloadable Plugins** - Hot-swapping and resource cleanup
7. **Type-Safe APIs** - No string-based lookups or DSLs

## Common Tasks

### Create a New Module
```csharp
public sealed class MyModule : Module
{
    private static readonly ModuleDescriptor Info = 
        ModuleDescriptor.Create(
            id: "my.module",
            name: "My Module",
            version: new Version(1, 0, 0),
            description: "Does something useful");

    public override ModuleDescriptor Descriptor => Info;

    public override async ValueTask InitializeAsync(
        ModuleContext context, 
        CancellationToken cancellationToken = default)
    {
        // Setup code
    }
}
```

### Create a Component with Commands
```csharp
public sealed class MyComponent : Component
{
    private static readonly ComponentDescriptor Info =
        ComponentDescriptor.Create(
            id: "my.component",
            name: "My Component",
            kind: ComponentKind.Console);

    public override ComponentDescriptor Descriptor => Info;

    public override IEnumerable<VisoraCommand> CreateCommands(
        ComponentContext context)
    {
        yield return new MyCommand1();
        yield return new MyCommand2();
    }
}
```

### Create a Command
```csharp
public sealed class MyCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info =
        CommandDescriptor.Create(
            id: "my.command",
            title: "My Command",
            kind: CommandKind.Tool);

    public override CommandDescriptor Descriptor => Info;

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        // Implementation
        return CommandResult.Success("Done");
    }
}
```

### Access Capabilities
```csharp
// In module/component/command execute methods
var optional = context.Capabilities.GetOptional<IMyService>();
var required = context.Capabilities.GetRequired<IMyService>();

if (optional != null)
{
    await optional.DoSomethingAsync();
}
```

## Naming Conventions

### Module IDs
- Format: `visora.<subsystem>.<feature>`
- Example: `visora.shell.commands.core`

### Component IDs
- Format: `<module-id>.<aspect>`
- Example: `visora.shell.commands.core.utilities`

### Command IDs
- Format: `<subsystem>.<verb>[.<object>]`
- Examples: `shell.ping`, `shell.env.info`, `shell.module.probe`

## Module Discovery Configuration

```csharp
var options = new ModuleCatalogOptions
{
    // Where to look
    ProbingPaths = { @"C:\modules", @"./local" },
    ExplicitModuleFiles = { @"C:\specific\module.vixm.dll" },
    
    // How to search
    RecurseSubdirectories = true,
    SearchPattern = "*.vixm.dll",
    
    // What to provide
    Services = serviceProvider,  // Optional IServiceProvider
    Capabilities = CapabilityProviders.CreateBuilder()
        .Add<IService>(service)
        .Build(),
    
    // Module defaults
    SharedTypes = typeof(VisoraModule), // etc.
};

var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options, ct);
```

## Assembly Naming

- **Hosts:** `V.exe`, `Visora.Terminal.exe`
- **Modules:** `VSCC.vixm.dll`, `Visora.CLI.Module.vixm.dll`
- **Convention:** `*.vixm.dll` = "Visora IX Module"

## Error Handling

```csharp
// Use CommandResult for command outcomes
return CommandResult.Failed($"Invalid input: {error}");

// Use exceptions for infrastructure failures
throw new InvalidOperationException("Module descriptor is required");

// Always support cancellation
cancellationToken.ThrowIfCancellationRequested();
```

## Async Best Practices

```csharp
// Always use ValueTask for performance-sensitive paths
public override ValueTask<CommandResult> ExecuteAsync(...) { }

// Always use ConfigureAwait(false) in libraries
await someTask.ConfigureAwait(false);

// Always respect cancellation tokens
public override async ValueTask InitializeAsync(
    ModuleContext context, 
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}

// Always dispose properly
public override async ValueTask DisposeAsync()
{
    // Cleanup
}
```

## Testing Hints

The architecture supports:
- Mock `ICapabilityProvider` for dependency injection
- Mock `VisoraModule` for testing components
- Records for easy assertion (value equality)
- Async patterns throughout for realistic testing

Example test structure:
```csharp
// Setup
var module = new Mock<VisoraModule>();
var context = new CommandContext(
    module: module.Object,
    component: null,
    surface: CommandSurface.Programmatic,
    capabilities: CapabilityProviders.Empty);

// Execute
var result = await command.ExecuteAsync(context, ct);

// Assert
Assert.AreEqual(CommandOutcome.Success, result.Outcome);
```

---

For full details, see `COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md`
