# VISORA Naming Conventions

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Applies To:** All VISORA platform code, modules, and components

---

## Table of Contents

1. [Overview](#overview)
2. [Module Identification](#module-identification)
3. [Component Identification](#component-identification)
4. [Command Identification](#command-identification)
5. [Class Naming](#class-naming)
6. [Method Naming](#method-naming)
7. [Property Naming](#property-naming)
8. [File Naming](#file-naming)
9. [Namespace Conventions](#namespace-conventions)
10. [Identifier Quick Reference](#identifier-quick-reference)
11. [Anti-Patterns](#anti-patterns)
12. [Cross-References](#cross-references)

---

## Overview

VISORA uses a hierarchical, dot-separated naming system for modules, components, and commands. This convention ensures:

- **Discoverability**: Clear naming makes features easy to find
- **Consistency**: Predictable patterns across the codebase
- **Avoiding Collisions**: Unique identifiers prevent conflicts
- **Semantic Clarity**: Names communicate purpose and scope

### Core Principles

1. **Hierarchical**: Use dot-notation to express containment relationships
2. **Descriptive**: Names should reveal intent without requiring documentation
3. **Consistent**: Follow established patterns from existing VISORA code
4. **Concise**: Favor clarity over brevity, but avoid redundancy

---

## Module Identification

### Format

```
visora.<subsystem>.<feature>[.<specialization>]
```

### Rules

| Rule | Description | Example |
|------|-------------|---------|
| **Prefix** | Always start with `visora.` | `visora.shell.commands.core` |
| **Subsystem** | Major functional area (shell, terminal, cli, etc.) | `visora.shell.*` |
| **Feature** | Specific capability or feature set | `visora.shell.commands.*` |
| **Specialization** | Optional refinement for focused modules | `visora.shell.commands.core` |

### Examples from VISORA Codebase

```csharp
// File: ShellCommandsModule.cs
private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Visora Shell Commands",
    version: new Version(0, 1, 0),
    description: "Baseline commands for diagnostics and exploration.");
```

**Good Examples:**

```
visora.shell.commands.core           // Core shell commands module
visora.shell.commands.git            // Git integration commands
visora.terminal.emulation            // Terminal emulation module
visora.cli.interactive               // Interactive CLI features
visora.diagnostics.performance       // Performance diagnostics
visora.extensions.scripting          // Scripting extension support
```

**Bad Examples:**

```
❌ shellCommands                     // Missing visora prefix
❌ Visora.Shell.Commands.Core        // Wrong casing (use lowercase)
❌ visora_shell_commands_core        // Wrong separator (use dots)
❌ visora.commands                   // Too vague (missing subsystem)
❌ visora.shell.commands.core.utils  // Too deep (4+ levels)
```

### Module ID Guidelines

- **Use lowercase** for all segments
- **Maximum 4 segments** (visora + 3 levels)
- **No abbreviations** unless widely understood (e.g., `cli`, `git`, `ui`)
- **Plural for collections** (commands, utilities, extensions)
- **Singular for concepts** (terminal, shell, module)

---

## Component Identification

### Format

```
<module-id>.<aspect>
```

Components extend their parent module's ID with an additional aspect descriptor.

### Rules

| Rule | Description | Example |
|------|-------------|---------|
| **Module Prefix** | Start with the full module ID | `visora.shell.commands.core.*` |
| **Aspect** | The specific component role or area | `visora.shell.commands.core.utilities` |
| **Uniqueness** | Must be unique within the module | Each component has distinct aspect |

### Examples from VISORA Codebase

```csharp
// File: CoreUtilitiesComponent.cs
private static readonly ComponentDescriptor Info = ComponentDescriptor.Create(
    id: "visora.shell.commands.core.utilities",
    name: "Core Utilities",
    description: "Diagnostics and helper commands for Visora shell experiments.",
    tags: new[] { "core", "shell", "diagnostics" },
    kind: ComponentKind.Console);
```

**Good Examples:**

```
visora.shell.commands.core.utilities        // Utility commands component
visora.shell.commands.core.automation       // Automation commands
visora.terminal.emulation.renderer          // Terminal renderer
visora.terminal.emulation.input             // Input handling
visora.cli.interactive.prompt               // Interactive prompt
visora.diagnostics.performance.monitoring   // Performance monitoring
```

**Bad Examples:**

```
❌ core.utilities                           // Missing module prefix
❌ visora.shell.utilities                   // Skipped intermediate levels
❌ visora.shell.commands.core.util          // Abbreviated (use full word)
❌ visora.shell.commands.core.component1    // Non-descriptive name
```

### Component ID Guidelines

- **Always include full module ID** as prefix
- **One additional segment** for the aspect
- **Descriptive aspects**: renderer, parser, handler, manager, provider
- **Role-based naming**: What the component does, not how it does it

---

## Command Identification

### Format

```
<subsystem>.<verb>[.<object>][.<qualifier>]
```

Commands use a simpler, action-oriented naming scheme.

### Rules

| Rule | Description | Example |
|------|-------------|---------|
| **Subsystem** | The functional area (shell, git, file, etc.) | `shell.*` |
| **Verb** | Action to perform | `shell.ping`, `file.open` |
| **Object** | Optional target of the action | `shell.env.info` |
| **Qualifier** | Optional refinement | `git.branch.create` |

### Examples from VISORA Codebase

```csharp
// File: PingCommand.cs
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping",
    description: "Checks connectivity with the Visora host.",
    kind: CommandKind.Automation);

// File: EnvironmentInfoCommand.cs
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "shell.env.info",
    title: "Environment Info",
    description: "Returns process and environment details useful for diagnostics.",
    kind: CommandKind.Tool);
```

**Good Examples:**

```
shell.ping                    // Simple diagnostic command
shell.env.info               // Environment information
shell.module.list            // List available modules
git.status                   // Git status check
git.branch.create            // Create a git branch
git.commit.amend             // Amend last commit
file.open                    // Open a file
file.save.as                 // Save file with new name
terminal.clear               // Clear terminal display
```

**Bad Examples:**

```
❌ visora.shell.ping                    // Don't include 'visora' prefix
❌ shell.Ping                          // Wrong casing (use lowercase)
❌ shell.ping_command                  // No underscores
❌ shell.check.connection              // Use standard verb (ping)
❌ pingShell                           // Reversed structure
❌ shell.environment.information       // Too verbose (use env.info)
```

### Command ID Guidelines

- **No `visora.` prefix** (commands are scoped to their module context)
- **Use lowercase** for all segments
- **Start with verb** for action commands (get, set, list, create, delete)
- **Use standard verbs** when possible: get, set, list, create, delete, update, show, find
- **Maximum 4 segments** (subsystem.verb.object.qualifier)
- **Aliases** for commonly-used commands should be short (2-4 chars)

### Standard Command Verbs

| Verb | Meaning | Example |
|------|---------|---------|
| `get` | Retrieve data | `module.get` |
| `set` | Modify data | `config.set` |
| `list` | Show collection | `module.list` |
| `create` | Make new entity | `branch.create` |
| `delete` | Remove entity | `module.delete` |
| `update` | Modify entity | `config.update` |
| `show` | Display details | `status.show` |
| `find` | Search | `command.find` |
| `info` | Get information | `env.info` |
| `ping` | Test connectivity | `shell.ping` |
| `probe` | Diagnostic check | `module.probe` |

---

## Class Naming

### Base VISORA Types

VISORA defines several base types that form the foundation of the module system.

| Type | Purpose | Convention |
|------|---------|------------|
| `VisoraModule` | Abstract base for all modules | Contract type in Visora.Contracts |
| `VisoraCommand` | Abstract base for all commands | Contract type in Visora.Contracts |
| `VisoraComponent` | Abstract base for all components | Contract type in Visora.Contracts |
| `Module` | Convenience base in Core | Implementation type in Visora.Core |
| `Component` | Convenience base in Core | Implementation type in Visora.Core |

### Class Naming Rules

| Pattern | When to Use | Example |
|---------|-------------|---------|
| `*Module` | Classes inheriting from VisoraModule | `ShellCommandsModule` |
| `*Command` | Classes inheriting from VisoraCommand | `PingCommand` |
| `*Component` | Classes inheriting from VisoraComponent | `CoreUtilitiesComponent` |
| `*Descriptor` | Metadata record types | `ModuleDescriptor`, `CommandDescriptor` |
| `*Context` | Context/state holders | `ModuleContext`, `CommandContext` |
| `*Options` | Configuration classes | `ModuleCatalogOptions` |
| `*Handle` | Resource wrapper/proxy | `ModuleHandle` |
| `*Catalog` | Registry/collection managers | `ModuleCatalog` |
| `*Locator` | Discovery/finding utilities | `ModuleLocator` |
| `*Provider` | Service/capability providers | `CapabilityProvider` |
| `*Manager` | Lifecycle/state managers | `SessionManager` |
| `*Builder` | Fluent construction APIs | `ModuleBuilder` |
| `*Factory` | Object creation helpers | `CommandFactory` |

### Examples from VISORA Codebase

```csharp
// Module class - inherits from Module base (which inherits from VisoraModule)
public sealed class ShellCommandsModule : Module
{
    // ...
}

// Command class - inherits from VisoraCommand
public sealed class PingCommand : VisoraCommand
{
    // ...
}

// Component class - inherits from Component base (which inherits from VisoraComponent)
public sealed class CoreUtilitiesComponent : Component
{
    // ...
}

// Descriptor - metadata record
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null);

// Context - state holder
public sealed class CommandContext
{
    // ...
}

// Options - configuration
public sealed class ModuleCatalogOptions
{
    // ...
}
```

### Choosing Between `Component` and `VisoraComponent`

**Use `VisoraComponent`** when:
- Defining contracts in `Visora.Contracts` project
- Creating base abstractions
- No dependency on `Visora.Core` is allowed

**Use `Component`** when:
- Implementing concrete components in modules
- You have access to `Visora.Core` reference
- Want convenient base implementation

```csharp
// ✅ Good: Contract definition
namespace Visora.Contracts.Components;
public abstract class VisoraComponent : IAsyncDisposable { }

// ✅ Good: Core convenience wrapper
namespace Visora.Core;
public abstract class Component : VisoraComponent { }

// ✅ Good: Module implementation
namespace Visora.Shell.Commands.Core.Components;
public sealed class CoreUtilitiesComponent : Component { }
```

### Class Naming Anti-Patterns

```csharp
// ❌ Bad: Generic name, unclear purpose
public class Helper { }

// ❌ Bad: Redundant 'Visora' prefix in module implementations
public class VisoraShellCommandsModule : Module { }

// ❌ Bad: Missing suffix for clarity
public class Ping : VisoraCommand { }  // Should be PingCommand

// ❌ Bad: Abbreviations
public class ShellCmdsMod : Module { }

// ❌ Bad: Wrong suffix
public class CommandInfo { }  // Should be CommandDescriptor

// ✅ Good: Clear, consistent naming
public sealed class ShellCommandsModule : Module { }
public sealed class PingCommand : VisoraCommand { }
public sealed class CoreUtilitiesComponent : Component { }
public sealed record CommandDescriptor(...);
```

---

## Method Naming

### Async Methods

All async methods must use the `Async` suffix.

```csharp
// ✅ Good: Async suffix
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    await DoWorkAsync(cancellationToken).ConfigureAwait(false);
}

public ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
{
    // ...
}

// ❌ Bad: Missing Async suffix
public async Task Initialize() { }
public Task Execute() { }
```

### Factory Methods

Use `Create` prefix for factory methods, especially for descriptor types.

```csharp
// ✅ Good: Static factory pattern
public sealed record ModuleDescriptor(...)
{
    public static ModuleDescriptor Create(
        string id,
        string name,
        Version version,
        string? description = null)
        => new(id, name, version, description);
}

// Usage
var descriptor = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Visora Shell Commands",
    version: new Version(0, 1, 0));
```

### Get Methods with Optional/Required Variants

When retrieving values that may or may not exist, use clear naming:

```csharp
// ✅ Good: Clear optional vs required semantics
public ModuleHandle? GetModuleOptional(string id)
    => _modules.FirstOrDefault(m => m.Id == id);

public ModuleHandle GetModuleRequired(string id)
    => GetModuleOptional(id) ?? throw new ModuleNotFoundException(id);

// Also acceptable for simple cases
public ModuleHandle? GetModule(string id)  // Nullable indicates optional
    => _modules.FirstOrDefault(m => m.Id == id);

// ❌ Bad: Unclear whether null is possible
public ModuleHandle GetModule(string id)  // Does this throw or return null?
    => _modules.FirstOrDefault(m => m.Id == id);
```

### Discovery and Enumeration Methods

```csharp
// ✅ Good: Clear discovery/enumeration semantics
public IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
    => context.EnumerateComponentCandidates()
        .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

public static IEnumerable<string> EnumerateCandidateFiles(ModuleCatalogOptions options)
{
    // ...
}

// ✅ Good: Create prefix for instance factories
public IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new PingCommand();
    yield return new EnvironmentInfoCommand();
}
```

### Lifecycle Methods

Standard lifecycle methods from VISORA base types:

```csharp
// Module lifecycle
public virtual ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
public virtual ValueTask ShutdownAsync(ModuleContext context, CancellationToken cancellationToken = default)
public virtual ValueTask DisposeAsync()

// Component lifecycle
public virtual ValueTask InitializeAsync(ComponentContext context, CancellationToken cancellationToken = default)
public virtual ValueTask ActivateAsync(ComponentContext context, CancellationToken cancellationToken = default)
public virtual ValueTask DeactivateAsync(ComponentContext context, CancellationToken cancellationToken = default)
public virtual ValueTask DisposeAsync()

// Command execution
public virtual ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
```

### Method Naming Patterns

| Pattern | Usage | Example |
|---------|-------|---------|
| `*Async` | Any async method | `LoadAsync`, `SaveAsync` |
| `Get*` | Retrieve single item | `GetModule`, `GetById` |
| `Find*` | Search for items | `FindCommand`, `FindByTag` |
| `List*` | Get collection | `ListModules`, `ListCommands` |
| `Enumerate*` | Lazy iteration | `EnumerateCandidates` |
| `Discover*` | Find/probe capabilities | `DiscoverComponents` |
| `Create*` | Factory/builder | `CreateCommands`, `CreateInstance` |
| `Initialize*` | Setup/prepare | `InitializeAsync` |
| `Shutdown*` | Teardown/cleanup | `ShutdownAsync` |
| `Activate*` | Enable/start | `ActivateAsync` |
| `Deactivate*` | Disable/stop | `DeactivateAsync` |
| `Execute*` | Run/invoke | `ExecuteAsync` |
| `Dispose*` | Resource cleanup | `DisposeAsync` |

---

## Property Naming

### Descriptor Properties

Descriptors should use `PascalCase` and be clear and descriptive.

```csharp
// ✅ Good: Clear descriptor properties
public sealed record ModuleDescriptor(
    string Id,                                        // Simple, clear
    string Name,                                      // Human-readable name
    Version Version,                                  // Semantic version
    string? Description = null,                       // Optional description
    IReadOnlyDictionary<string, string>? Tags = null, // Optional metadata
    ModuleRuntimeHints? RuntimeHints = null);         // Optional runtime config

public sealed record CommandDescriptor(
    string Id,
    string Title,                                     // Display name
    string? Description = null,
    CommandKind Kind = CommandKind.General,
    IReadOnlyCollection<string>? Aliases = null,
    IReadOnlyCollection<string>? Keywords = null,
    bool IsVisible = true,
    bool IsInstanceScoped = false,
    CommandUiHint? Ui = null);
```

### Descriptor Property Pattern

When exposing descriptors from classes, use the suffix `Descriptor`:

```csharp
// ✅ Good: Consistent descriptor exposure
public abstract class VisoraModule : IAsyncDisposable
{
    public abstract ModuleDescriptor Descriptor { get; }
}

public abstract class VisoraCommand
{
    public abstract CommandDescriptor Descriptor { get; }
}

public abstract class VisoraComponent : IAsyncDisposable
{
    public abstract ComponentDescriptor Descriptor { get; }
}

// Implementation example
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.");

    public override CommandDescriptor Descriptor => Info;
}
```

### Boolean Properties

Use `Is*`, `Has*`, `Can*`, or `Should*` prefixes:

```csharp
// ✅ Good: Clear boolean intent
bool IsVisible
bool IsInstanceScoped
bool HasCommands
bool CanExecute
bool ShouldRecurse

// ❌ Bad: Unclear boolean properties
bool Visible        // Use IsVisible
bool Scoped         // Use IsScoped
bool Commands       // Use HasCommands
bool Execute        // Use CanExecute
```

### Collection Properties

Use plural names and prefer `IReadOnly*` for immutability:

```csharp
// ✅ Good: Clear collection properties
public IReadOnlyList<ModuleHandle> Modules { get; }
public IReadOnlyCollection<string> Aliases { get; }
public IReadOnlyDictionary<string, string> Tags { get; }

// ❌ Bad: Mutable collections exposed
public List<ModuleHandle> Modules { get; }  // Allows external mutation
public string[] Aliases { get; }            // Prefer IReadOnlyCollection
```

---

## File Naming

### Primary Rule: Match Class Name

Each file should contain one primary class and be named to match.

```
✅ Good file naming:
ShellCommandsModule.cs          → class ShellCommandsModule
PingCommand.cs                  → class PingCommand
CoreUtilitiesComponent.cs       → class CoreUtilitiesComponent
ModuleDescriptor.cs             → record ModuleDescriptor
CommandContext.cs               → class CommandContext
```

### Multiple Types in One File

When a file contains multiple related types (e.g., record + enum), name the file after the primary type:

```csharp
// File: CommandDescriptor.cs
namespace Visora.Contracts.Commands;

// Primary type - file named after this
public sealed record CommandDescriptor(...)
{
    // ...
}

// Supporting type in same file
public sealed record CommandUiHint(
    string? MenuPath = null,
    string? Icon = null,
    string? DefaultGesture = null);

// Supporting enum in same file
public enum CommandKind
{
    General,
    Navigation,
    Tool,
    Shell,
    Automation
}
```

### File Organization Patterns

```
✅ Good: Clear organization
/Modules/
  ModuleDescriptor.cs
  ModuleContext.cs
  ModuleDiscoveryContext.cs
  ModuleRuntimeHints.cs
  VisoraModule.cs

/Commands/
  CommandDescriptor.cs
  CommandContext.cs
  CommandResult.cs
  VisoraCommand.cs

/Components/
  ComponentDescriptor.cs
  ComponentContext.cs
  VisoraComponent.cs
```

### File Naming Rules

1. **Use PascalCase** for all file names
2. **Match the primary class name** exactly
3. **One primary class per file** (supporting types allowed)
4. **No underscores or hyphens** in file names
5. **Use `.cs` extension** for all C# files

---

## Namespace Conventions

### Primary Rule: Match Folder Structure

Namespaces must match the physical folder structure of the project.

```
Project: Visora.Contracts
RootNamespace: Visora.Contracts

Folder Structure:
/Visora.Contracts/
  /Modules/
    VisoraModule.cs           → namespace Visora.Contracts.Modules
    ModuleDescriptor.cs       → namespace Visora.Contracts.Modules
  /Commands/
    VisoraCommand.cs          → namespace Visora.Contracts.Commands
    CommandDescriptor.cs      → namespace Visora.Contracts.Commands
  /Components/
    VisoraComponent.cs        → namespace Visora.Contracts.Components
    ComponentDescriptor.cs    → namespace Visora.Contracts.Components
```

### Examples from VISORA Codebase

```csharp
// File: /Visora.Contracts/Modules/VisoraModule.cs
namespace Visora.Contracts.Modules;

public abstract class VisoraModule : IAsyncDisposable
{
    // ...
}

// File: /Visora.Shell.Commands.Core/Commands/PingCommand.cs
namespace Visora.Shell.Commands.Core.Commands;

public sealed class PingCommand : VisoraCommand
{
    // ...
}

// File: /Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs
namespace Visora.Shell.Commands.Core.Components;

public sealed class CoreUtilitiesComponent : Component
{
    // ...
}
```

### Namespace Hierarchy

The namespace hierarchy for VISORA follows this pattern:

```
Visora                              (Root - not used directly)
├── Visora.Contracts                (Abstract contracts, no dependencies)
│   ├── Visora.Contracts.Modules
│   ├── Visora.Contracts.Commands
│   ├── Visora.Contracts.Components
│   └── Visora.Contracts.Common
├── Visora.Core                     (Core implementations)
│   ├── Visora.Core.Modules
│   ├── Visora.Core.Commands
│   ├── Visora.Core.Components
│   └── Visora.Core.Capabilities
├── Visora.Shared                   (Shared utilities)
├── Visora.Shell                    (Shell host)
├── Visora.Terminal                 (Terminal host)
├── Visora.CLI                      (CLI host)
└── Visora.Shell.Commands.Core      (Example module)
    ├── Visora.Shell.Commands.Core.Commands
    └── Visora.Shell.Commands.Core.Components
```

### Namespace Rules

1. **Match physical folder structure** exactly
2. **Use PascalCase** for all namespace segments
3. **No abbreviations** unless universally understood
4. **Logical grouping** by feature area (Modules, Commands, Components)
5. **Root namespace matches** assembly/project name

---

## Identifier Quick Reference

### Module IDs

```
Format:     visora.<subsystem>.<feature>[.<specialization>]
Casing:     lowercase
Separator:  dot (.)
Prefix:     visora.
Depth:      2-4 segments (including visora)

Examples:
  visora.shell.commands.core
  visora.terminal.emulation
  visora.diagnostics.performance
```

### Component IDs

```
Format:     <module-id>.<aspect>
Casing:     lowercase
Separator:  dot (.)
Prefix:     Full module ID
Depth:      module depth + 1

Examples:
  visora.shell.commands.core.utilities
  visora.terminal.emulation.renderer
  visora.diagnostics.performance.monitoring
```

### Command IDs

```
Format:     <subsystem>.<verb>[.<object>][.<qualifier>]
Casing:     lowercase
Separator:  dot (.)
Prefix:     None (no 'visora.')
Depth:      2-4 segments

Examples:
  shell.ping
  shell.env.info
  git.branch.create
  file.save.as
```

### Class Names

```
Format:     PascalCase[Suffix]
Casing:     PascalCase
Suffixes:   Module, Command, Component, Descriptor, Context, Options, etc.

Examples:
  ShellCommandsModule
  PingCommand
  CoreUtilitiesComponent
  ModuleDescriptor
  CommandContext
```

### Method Names

```
Format:     PascalCase[Async]
Casing:     PascalCase
Suffix:     Async (for async methods)
Prefixes:   Get, Find, List, Create, Discover, Initialize, etc.

Examples:
  InitializeAsync
  ExecuteAsync
  GetModule
  DiscoverComponents
  CreateCommands
```

### Property Names

```
Format:     PascalCase
Casing:     PascalCase
Booleans:   Is*, Has*, Can*, Should*
Descriptor: *Descriptor

Examples:
  Descriptor
  IsVisible
  HasCommands
  Modules
  RuntimeHints
```

### File Names

```
Format:     PascalCase.cs
Casing:     PascalCase
Rule:       Match primary class name
Extension:  .cs

Examples:
  ShellCommandsModule.cs
  PingCommand.cs
  ModuleDescriptor.cs
```

### Namespaces

```
Format:     Project.Folder.Subfolder
Casing:     PascalCase
Rule:       Match folder structure
Separator:  dot (.)

Examples:
  Visora.Contracts.Modules
  Visora.Shell.Commands.Core.Commands
  Visora.Core.Capabilities
```

---

## Anti-Patterns

### Common Mistakes to Avoid

#### 1. Wrong Casing

```csharp
// ❌ Bad: Module ID with wrong casing
id: "Visora.Shell.Commands.Core"  // Should be lowercase

// ❌ Bad: Command ID with wrong casing
id: "Shell.Ping"                  // Should be lowercase

// ✅ Good: Correct casing
id: "visora.shell.commands.core"
id: "shell.ping"
```

#### 2. Missing Prefixes

```csharp
// ❌ Bad: Module without 'visora.' prefix
id: "shell.commands.core"

// ✅ Good: Proper module prefix
id: "visora.shell.commands.core"
```

#### 3. Command IDs with Module Prefix

```csharp
// ❌ Bad: Command with 'visora.' prefix
id: "visora.shell.ping"

// ✅ Good: Command without platform prefix
id: "shell.ping"
```

#### 4. Inconsistent Separators

```csharp
// ❌ Bad: Using underscores
id: "visora.shell_commands_core"

// ❌ Bad: Using hyphens
id: "visora.shell-commands-core"

// ✅ Good: Using dots
id: "visora.shell.commands.core"
```

#### 5. Redundant Class Prefixes

```csharp
// ❌ Bad: Redundant 'Visora' prefix in implementation
public class VisoraPingCommand : VisoraCommand { }

// ✅ Good: Simple, clear name
public class PingCommand : VisoraCommand { }
```

#### 6. Abbreviated Names

```csharp
// ❌ Bad: Abbreviations
public class ShellCmdsMod : Module { }
public class PingCmd : VisoraCommand { }

// ✅ Good: Full descriptive names
public class ShellCommandsModule : Module { }
public class PingCommand : VisoraCommand { }
```

#### 7. Missing Async Suffix

```csharp
// ❌ Bad: Async method without suffix
public async Task Initialize() { }
public Task<CommandResult> Execute() { }

// ✅ Good: Clear async suffix
public async Task InitializeAsync() { }
public Task<CommandResult> ExecuteAsync() { }
```

#### 8. Non-descriptive Generic Names

```csharp
// ❌ Bad: Too generic
public class Helper { }
public class Utility { }
public class Manager { }

// ✅ Good: Specific purpose
public class ModuleLocator { }
public class CommandFactory { }
public class SessionManager { }
```

#### 9. Namespace Not Matching Folder Structure

```csharp
// File location: /Visora.Shell/Commands/PingCommand.cs

// ❌ Bad: Namespace doesn't match folder
namespace Visora.Shell;

// ✅ Good: Namespace matches folder structure
namespace Visora.Shell.Commands;
```

#### 10. Too Deep Hierarchies

```csharp
// ❌ Bad: Too many levels (5 segments)
id: "visora.shell.commands.core.diagnostics.network"

// ✅ Good: Reasonable depth (4 segments)
id: "visora.shell.diagnostics.network"
```

---

## Cross-References

### Related Documentation

- **[Project Structure](./project-structure.md)**: Solution and project organization patterns
- **[Code Style](./code-style.md)**: C# coding standards and patterns
- **[Assembly Conventions](./assembly-conventions.md)**: Assembly naming and deployment
- **[Module Pattern](../patterns/module-pattern.md)**: Module architecture and lifecycle
- **[Component Pattern](../patterns/component-pattern.md)**: Component design and registration
- **[Command Pattern](../patterns/command-pattern.md)**: Command design and execution

### Key Pattern Documents

- **Module Discovery**: See `patterns/module-discovery-pattern.md`
- **Component Lifecycle**: See `patterns/component-lifecycle-pattern.md`
- **Descriptor Pattern**: See `patterns/descriptor-pattern.md`
- **Async Patterns**: See `code-style.md#async-await-patterns`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-10 | Initial naming conventions documentation |

---

**See Also:**
- [VISORA Architecture Overview](../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md)
- [Pattern Quick Reference](../PATTERNS_QUICK_REFERENCE.md)
- [Quick Start Guide](../00-quick-start/README.md)
