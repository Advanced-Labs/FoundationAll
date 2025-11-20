# VISORA vs Traditional Plugin Systems

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Audience:** Architects evaluating plugin frameworks

---

## Table of Contents

1. [Overview](#overview)
2. [Traditional Plugin Systems](#traditional-plugin-systems)
3. [VISORA vs MEF](#visora-vs-mef)
4. [VISORA vs MAF](#visora-vs-maf)
5. [VISORA vs VSPackages](#visora-vs-vspackages)
6. [Comparison Tables](#comparison-tables)
7. [When to Use Each](#when-to-use-each)
8. [Decision Criteria](#decision-criteria)
9. [Migration Considerations](#migration-considerations)
10. [Cross-References](#cross-references)

---

## Overview

### What Are Plugin Systems?

Plugin systems enable applications to be extended without modifying the core application code. They provide:

- **Dynamic extensibility**: Load features at runtime
- **Isolation**: Prevent plugins from interfering with each other
- **Versioning**: Support multiple versions of plugins
- **Discovery**: Find and load plugins automatically

### Traditional .NET Plugin Systems

| System | Era | Platform | Use Case |
|--------|-----|----------|----------|
| **MEF** | 2009-2015 | .NET Framework | Composition, dependency injection |
| **MAF** | 2007-2012 | .NET Framework | Add-in isolation, versioning |
| **VSPackages** | 2005-present | Visual Studio | IDE extensions |
| **VISORA** | 2024-present | .NET 9.0 | Multi-surface IDE platforms |

---

## Traditional Plugin Systems

### MEF (Managed Extensibility Framework)

**Overview**: Composition framework for .NET applications

**Key Features**:
- Attribute-based discovery (`[Export]`, `[Import]`)
- Composition containers
- Parts and contracts
- Lazy initialization

**Example**:
```csharp
// MEF Plugin
[Export(typeof(IPlugin))]
public class MyPlugin : IPlugin
{
    [Import]
    public ILogger Logger { get; set; }

    public void Execute()
    {
        Logger.Log("Plugin executing");
    }
}

// MEF Host
var catalog = new AssemblyCatalog(typeof(Program).Assembly);
var container = new CompositionContainer(catalog);
var plugin = container.GetExportedValue<IPlugin>();
plugin.Execute();
```

### MAF (Managed Add-in Framework)

**Overview**: Add-in isolation framework with AppDomains

**Key Features**:
- Pipeline segments (contracts, adapters, views)
- Strong isolation via AppDomains
- Version independence
- Complex configuration

**Example**:
```csharp
// MAF Contract
public interface ICalculator : IContract
{
    int Add(int a, int b);
}

// MAF Host
var tokens = AddInStore.FindAddIns(typeof(ICalculator), pipelineRoot);
var addIn = tokens[0].Activate<ICalculator>(AppDomain.CurrentDomain);
var result = addIn.Add(2, 3);
```

### VSPackages

**Overview**: Visual Studio extension system

**Key Features**:
- COM-based interfaces
- Tight VS integration
- Complex lifecycle
- Registry-based discovery

**Example**:
```csharp
[PackageRegistration(UseManagedResourcesOnly = true)]
[Guid("12345678-1234-1234-1234-123456789ABC")]
public sealed class MyVSPackage : Package
{
    protected override void Initialize()
    {
        base.Initialize();
        // Initialize package
    }
}
```

---

## VISORA vs MEF

### Conceptual Comparison

| Aspect | VISORA | MEF |
|--------|--------|-----|
| **Purpose** | Multi-surface platform | Composition framework |
| **Discovery** | Reflection + naming convention | Attributes (`[Export]`) |
| **Isolation** | `AssemblyLoadContext` | None (shared AppDomain) |
| **Unloading** | Yes (modules can be unloaded) | No (cannot unload) |
| **Versioning** | Built-in version management | Limited (through catalogs) |
| **Async** | Async-first lifecycle | Synchronous |
| **Capabilities** | Explicit capability negotiation | Import/Export contracts |
| **Configuration** | Code-based (no external files) | Attributes + code |

### Side-by-Side Code Example

**MEF Plugin**:
```csharp
[Export(typeof(ICommand))]
public class PingCommand : ICommand
{
    [Import]
    public ILogger Logger { get; set; }

    public void Execute()
    {
        Logger.Log("Pong");
    }
}

// Host
var catalog = new DirectoryCatalog("plugins");
var container = new CompositionContainer(catalog);
container.ComposeParts(this); // Satisfy imports
```

**VISORA Module**:
```csharp
public sealed class ShellCommandsModule : Module
{
    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Shell Commands",
        version: new Version(0, 1, 0));

    public override ModuleDescriptor Descriptor => Info;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Module initializing");
    }
}

public sealed class PingCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Pong");
        return CommandResult.Success("Pong");
    }
}

// Host
await using var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options);
```

### Key Differences

#### 1. Discovery Mechanism

**MEF**: Attribute-based
```csharp
[Export(typeof(IPlugin))]
public class MyPlugin : IPlugin { }
```

**VISORA**: Reflection + naming convention
```csharp
// No attributes needed
// Assembly name: MyModule.vixm.dll
public sealed class MyModule : Module { }
```

#### 2. Dependency Resolution

**MEF**: Import/Export
```csharp
[Export]
public class Service { }

[Import]
public IService Service { get; set; }
```

**VISORA**: Capability negotiation
```csharp
public override async ValueTask InitializeAsync(ModuleContext context, ...)
{
    var service = context.Capabilities.GetOptional<IService>();
}
```

#### 3. Lifecycle

**MEF**: Creation only
```csharp
// MEF creates instances, no lifecycle hooks
var plugin = container.GetExportedValue<IPlugin>();
```

**VISORA**: Full async lifecycle
```csharp
await module.InitializeAsync(context);
// ... use module
await module.ShutdownAsync(context);
await module.DisposeAsync();
```

#### 4. Isolation

**MEF**: No isolation
- All types share the same AppDomain
- Cannot unload assemblies
- Memory leaks accumulate

**VISORA**: Strong isolation
- Each module in separate `AssemblyLoadContext`
- Modules can be unloaded
- Memory can be reclaimed

### When to Use MEF

**Use MEF if**:
- You need dependency injection for a .NET Framework application
- You want attribute-based composition
- You don't need module unloading
- You're building a simple plugin system

**Don't use MEF if**:
- You need to unload plugins dynamically
- You want async lifecycle support
- You need strong isolation between plugins
- You're building a modern .NET 9.0 application

---

## VISORA vs MAF

### Conceptual Comparison

| Aspect | VISORA | MAF |
|--------|--------|-----|
| **Isolation** | `AssemblyLoadContext` | AppDomain |
| **Pipeline** | Simple (Module → Component → Command) | Complex (7 segments) |
| **Configuration** | Code-based | XML manifests + code |
| **Versioning** | Semantic versioning | Contract versioning |
| **Serialization** | Not required | Required for cross-domain |
| **Complexity** | Low to medium | High |
| **Performance** | High (in-process) | Lower (marshaling overhead) |

### Pipeline Comparison

**MAF Pipeline** (7 segments):
```
Host ↔ HostAdapter ↔ HostView ↔ Contract ↔ AddInView ↔ AddInAdapter ↔ AddIn
```

**VISORA Pipeline** (3 levels):
```
Host ↔ Module ↔ Component ↔ Command
```

### Side-by-Side Code Example

**MAF Add-in**:
```csharp
// Contract (separate assembly)
public interface ICalculator : IContract
{
    int Add(int a, int b);
}

// Add-in View (separate assembly)
public abstract class CalculatorAddInView
{
    public abstract int Add(int a, int b);
}

// Add-in (plugin assembly)
[AddIn("Calculator", Version = "1.0.0.0")]
public class CalculatorAddIn : CalculatorAddInView
{
    public override int Add(int a, int b) => a + b;
}

// Host View (separate assembly)
public abstract class CalculatorHostView
{
    public abstract int Add(int a, int b);
}

// Host
AddInStore.Update(pipelineRoot);
var tokens = AddInStore.FindAddIns(typeof(ICalculator), pipelineRoot);
var addIn = tokens[0].Activate<CalculatorHostView>(
    AppDomain.CurrentDomain,
    AddInSecurityLevel.FullTrust);
var result = addIn.Add(2, 3);
```

**VISORA Module**:
```csharp
// Module (single assembly)
public sealed class CalculatorModule : Module
{
    public override ModuleDescriptor Descriptor => ModuleDescriptor.Create(
        id: "calculator",
        name: "Calculator",
        version: new Version(1, 0, 0));
}

public sealed class CalculatorComponent : Component
{
    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new AddCommand();
    }
}

public sealed class AddCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var a = (int)context.Parameters["a"];
        var b = (int)context.Parameters["b"];
        return CommandResult.Success($"Result: {a + b}", a + b);
    }
}

// Host
await using var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options);
```

### Key Differences

#### 1. Complexity

**MAF**: Very complex
- 7 pipeline segments
- Multiple assemblies
- Adapter layers
- XML configuration

**VISORA**: Simpler
- 1 assembly per module
- Code-based configuration
- No adapters needed

#### 2. Isolation Mechanism

**MAF**: AppDomain isolation
- Heavyweight
- Limited on .NET Core/.NET 5+
- Requires serialization

**VISORA**: AssemblyLoadContext isolation
- Lightweight
- Native .NET Core/.NET 5+ support
- No serialization required

#### 3. Versioning

**MAF**: Contract versioning
- Must maintain contract assemblies
- Breaking changes difficult

**VISORA**: Semantic versioning
- Version in descriptor
- Host can choose version strategy

### When to Use MAF

**Use MAF if**:
- You're on .NET Framework (not .NET Core/.NET 5+)
- You need maximum isolation (AppDomain boundaries)
- You're willing to accept complexity

**Don't use MAF if**:
- You're on .NET Core/.NET 5+/.NET 9.0
- You want a simple plugin system
- You need high performance

---

## VISORA vs VSPackages

### Conceptual Comparison

| Aspect | VISORA | VSPackages |
|--------|--------|------------|
| **Platform** | Cross-platform .NET 9.0 | Windows/Visual Studio |
| **Discovery** | Reflection + naming | Registry + attributes |
| **Interface** | .NET contracts | COM interfaces |
| **Isolation** | `AssemblyLoadContext` | Shared VS process |
| **Lifecycle** | Async | Synchronous |
| **Multi-surface** | CLI, Terminal, WPF | Visual Studio IDE only |

### Side-by-Side Code Example

**VSPackage**:
```csharp
[PackageRegistration(UseManagedResourcesOnly = true)]
[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuidString)]
public sealed class MyVSPackage : Package
{
    public const string PackageGuidString = "12345678-1234-1234-1234-123456789ABC";

    protected override void Initialize()
    {
        base.Initialize();

        OleMenuCommandService commandService =
            this.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

        if (commandService != null)
        {
            var menuCommandID = new CommandID(
                new Guid(CommandSet),
                CommandId);

            var menuItem = new MenuCommand(
                this.Execute,
                menuCommandID);

            commandService.AddCommand(menuItem);
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        // Command logic
    }
}
```

**VISORA Module**:
```csharp
public sealed class MyToolsModule : Module
{
    public override ModuleDescriptor Descriptor => ModuleDescriptor.Create(
        id: "mytools",
        name: "My Tools",
        version: new Version(1, 0, 0));
}

public sealed class ToolsComponent : Component
{
    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new MyToolCommand();
    }
}

public sealed class MyToolCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "mytool.execute",
        title: "Execute My Tool",
        ui: new CommandUiHint(
            menuPath: "Tools/My Tool",
            icon: "tool.png",
            defaultGesture: "Ctrl+Shift+T"));

    public override CommandDescriptor Descriptor => Info;

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Command logic
        return CommandResult.Success("Tool executed");
    }
}
```

### Key Differences

#### 1. Platform

**VSPackages**: Visual Studio only
- Tightly coupled to VS
- Windows-only
- COM-based

**VISORA**: Cross-platform
- Runs on Windows, Linux, macOS
- Pure .NET
- Multiple surfaces (CLI, Terminal, WPF)

#### 2. Registration

**VSPackages**: Registry + attributes
```csharp
[PackageRegistration(...)]
[ProvideMenuResource(...)]
// Requires VSIX manifest, registry keys
```

**VISORA**: Code-based
```csharp
// No attributes needed
// Just implement Module base class
```

#### 3. Multi-Surface Support

**VSPackages**: IDE only
- Must run inside Visual Studio
- No CLI support

**VISORA**: Multi-surface
- CLI: `visora mycommand`
- Terminal: Interactive REPL
- WPF: Full GUI
- Same command runs everywhere

### When to Use VSPackages

**Use VSPackages if**:
- You're building a Visual Studio extension
- You need deep VS IDE integration
- You want to leverage VS services (project system, editor, debugger)

**Don't use VSPackages if**:
- You want to run outside Visual Studio
- You need cross-platform support
- You want a modern async API

---

## Comparison Tables

### Discovery Mechanisms

| Framework | Mechanism | Configuration | Overhead |
|-----------|-----------|---------------|----------|
| **MEF** | Attributes (`[Export]`) | Low | Low |
| **MAF** | XML manifests | High | High |
| **VSPackages** | Registry + attributes | Very High | High |
| **VISORA** | Reflection + naming | Low | Low |

### Isolation and Unloading

| Framework | Isolation | Unloadable | Memory Reclaim |
|-----------|-----------|------------|----------------|
| **MEF** | None | ❌ No | ❌ No |
| **MAF** | AppDomain | ✅ Yes | ✅ Yes |
| **VSPackages** | None | ❌ No | ❌ No |
| **VISORA** | AssemblyLoadContext | ✅ Yes | ✅ Yes |

### Async Support

| Framework | Async Lifecycle | Cancellation | ValueTask |
|-----------|-----------------|--------------|-----------|
| **MEF** | ❌ No | ❌ No | ❌ No |
| **MAF** | ❌ No | ❌ No | ❌ No |
| **VSPackages** | ❌ No | ❌ No | ❌ No |
| **VISORA** | ✅ Yes | ✅ Yes | ✅ Yes |

### Complexity

| Framework | Learning Curve | LOC for Hello World | Pipeline Segments |
|-----------|----------------|---------------------|-------------------|
| **MEF** | Low | ~30 | 1 (container) |
| **MAF** | Very High | ~300+ | 7 (full pipeline) |
| **VSPackages** | High | ~100+ | N/A |
| **VISORA** | Medium | ~50 | 3 (module/component/command) |

---

## When to Use Each

### Use VISORA If

✅ You're building a modern .NET 9.0 application
✅ You need multi-surface support (CLI, Terminal, WPF)
✅ You want module isolation and unloading
✅ You prefer async-first APIs
✅ You need capability-based dependency resolution
✅ You want code-based (no manifest) configuration
✅ You're building an IDE-like platform

### Use MEF If

✅ You need a simple composition framework
✅ You're on .NET Framework
✅ You don't need module unloading
✅ You prefer attribute-based discovery
✅ You want dependency injection-like features

### Use MAF If

✅ You need maximum isolation (AppDomain boundaries)
✅ You're on .NET Framework
✅ You can accept high complexity
✅ You need strict contract versioning

### Use VSPackages If

✅ You're building a Visual Studio extension
✅ You need deep VS integration
✅ You want to leverage VS services

---

## Decision Criteria

### Choose VISORA Over MEF When

| Criterion | Favor VISORA |
|-----------|--------------|
| .NET Version | .NET 5+ / .NET 9.0 |
| Unloading | Required |
| Isolation | Strong isolation needed |
| Lifecycle | Async operations common |
| Discovery | Prefer reflection over attributes |

### Choose VISORA Over MAF When

| Criterion | Favor VISORA |
|-----------|--------------|
| Platform | .NET Core/.NET 5+/.NET 9.0 |
| Complexity | Simple preferred |
| Performance | High performance needed |
| Serialization | Avoid marshaling overhead |

### Choose VISORA Over VSPackages When

| Criterion | Favor VISORA |
|-----------|--------------|
| Platform | Cross-platform required |
| Surfaces | CLI, Terminal, or WPF |
| Isolation | Module unloading needed |
| Modern API | Async/await patterns |

---

## Migration Considerations

### Migrating from MEF to VISORA

**Steps**:
1. Convert `[Export]` classes to `Module`/`Component`/`Command`
2. Replace `[Import]` with capability negotiation
3. Add async lifecycle methods
4. Package as `.vixm.dll` assembly
5. Test with `ModuleCatalog`

**Example**:

**Before (MEF)**:
```csharp
[Export(typeof(ICommand))]
public class MyCommand : ICommand
{
    [Import]
    public ILogger Logger { get; set; }

    public void Execute()
    {
        Logger.Log("Executing");
    }
}
```

**After (VISORA)**:
```csharp
public sealed class MyModule : Module
{
    public override ModuleDescriptor Descriptor => ModuleDescriptor.Create(
        id: "mycompany.mycommand",
        name: "My Command",
        version: new Version(1, 0, 0));
}

public sealed class MyComponent : Component
{
    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new MyCommand();
    }
}

public sealed class MyCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Executing");
        return CommandResult.Success("Complete");
    }
}
```

### Migrating from MAF to VISORA

**Steps**:
1. Collapse 7-segment pipeline into Module → Component → Command
2. Remove XML manifests
3. Remove adapter layers
4. Replace AppDomain isolation with AssemblyLoadContext
5. Remove serialization requirements

### Migrating from VSPackages to VISORA

**Steps**:
1. Extract command logic from VSPackage
2. Create Module/Component/Command structure
3. Replace VS services with capabilities
4. Remove COM interfaces
5. Add multi-surface support

---

## Cross-References

### Related Documentation

- **[Plugin Architecture Pattern](../patterns/plugin-architecture/visora-analysis.md)**: VISORA plugin design
- **[Capability Negotiation Pattern](../patterns/capability-negotiation/visora-analysis.md)**: Capability vs Import/Export
- **[Module Lifecycle Pattern](../patterns/module-lifecycle/visora-analysis.md)**: Lifecycle comparison
- **[Creating Modules](../blueprints/creating-modules.md)**: VISORA module guide

### Related Decisions

- **[ADR-001: Reflection Over Manifests](../decisions/reflection-over-manifests.md)**: Why no XML configuration
- **[ADR-004: Unloadable Plugins](../decisions/unloadable-plugins.md)**: AssemblyLoadContext isolation

---

**End of Document**
