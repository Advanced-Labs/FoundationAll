# VISORA Project Structure Conventions

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Applies To:** All VISORA projects and modules

---

## Table of Contents

1. [Overview](#overview)
2. [Repository Layout](#repository-layout)
3. [Solution Organization](#solution-organization)
4. [Project Categories](#project-categories)
5. [Dependency Direction](#dependency-direction)
6. [Namespace Hierarchy](#namespace-hierarchy)
7. [Layer Separation Rules](#layer-separation-rules)
8. [Reference Management](#reference-management)
9. [Build Output Organization](#build-output-organization)
10. [Module Deployment Structure](#module-deployment-structure)
11. [Project Templates](#project-templates)
12. [Anti-Patterns](#anti-patterns)
13. [Cross-References](#cross-references)

---

## Overview

VISORA follows a layered architecture with clear separation of concerns. The structure ensures:

- **Clear Dependencies**: One-way dependency flow prevents circular references
- **Modularity**: Clean boundaries enable independent development
- **Discoverability**: Consistent structure makes navigation intuitive
- **Extensibility**: New modules integrate without core changes

### Design Principles

1. **Contracts First**: Define interfaces before implementations
2. **Dependency Inversion**: Core depends on contracts, not implementations
3. **Single Responsibility**: Each project has one focused purpose
4. **Explicit References**: No implicit or transitive dependencies

---

## Repository Layout

### Top-Level Structure

```
/Visora/
├── src/                          # Source code
│   ├── Visora.Contracts/         # Contract definitions
│   ├── Visora.Core/              # Core implementations
│   ├── Visora.Shared/            # Shared utilities
│   ├── Visora.Shell/             # Shell host
│   ├── Visora.Terminal/          # Terminal host
│   ├── Visora.CLI/               # CLI host
│   ├── Visora.Shell.Commands.Core/  # Example module
│   └── Visora.sln                # Solution file
├── docs/                         # User documentation
│   ├── guides/
│   ├── api/
│   └── tutorials/
├── APMS/                         # Advanced Platform Module System docs
│   ├── specifications/
│   └── examples/
├── References/                   # Developer documentation
│   ├── patterns/                 # Architecture patterns
│   ├── conventions/              # Coding standards (this doc)
│   ├── blueprints/               # Design blueprints
│   ├── decisions/                # Architecture decisions
│   └── comparisons/              # Technology comparisons
├── .gitignore                    # Git ignore patterns
└── README.md                     # Repository overview
```

### Purpose of Each Top-Level Directory

| Directory | Purpose | Audience |
|-----------|---------|----------|
| `src/` | All source code and projects | Developers |
| `docs/` | User-facing documentation | End users, integrators |
| `APMS/` | Platform specifications | Module authors, architects |
| `References/` | Developer documentation | Core contributors |

---

## Solution Organization

### Solution Structure (Visora.sln)

The VISORA solution organizes projects into logical solution folders:

```
Visora.sln
├── Core/                         # Foundation projects
│   ├── Visora.Contracts          # Contract definitions (no dependencies)
│   ├── Visora.Shared             # Shared utilities
│   └── Windows/                  # Platform-specific core
│       ├── Visora.Core.Windows
│       └── Visora.Shared.Windows
├── Shell/                        # Shell host applications
│   ├── Visora.Shell              # Shell library
│   ├── Visora.CLI                # CLI executable
│   └── VISORA Windows            # Windows GUI host
└── Modules/                      # Extension modules
    ├── Windows/
    │   └── Visora.Terminal       # Terminal module
    └── Shell/
        ├── Visora.Shell.Commands.Core  # Core commands module
        └── Visora.CLI.Module     # CLI module
```

### Solution Folder Guidelines

| Folder | Contains | Purpose |
|--------|----------|---------|
| `Core/` | Contracts, Core, Shared | Foundation libraries |
| `Core/Windows/` | Platform-specific core | Windows-specific implementations |
| `Shell/` | Host applications | Executable hosts and shells |
| `Modules/` | Extension modules | Pluggable functionality |
| `Modules/Shell/` | Shell-specific modules | Shell enhancement modules |
| `Modules/Windows/` | Windows-specific modules | Windows-only modules |

### Actual Solution File Structure

```xml
Microsoft Visual Studio Solution File, Format Version 12.00

Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Visora.Contracts", "Visora.Contracts\Visora.Contracts.csproj"
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Visora.Core", "Visora.Core\Visora.Core.csproj"
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Visora.Shared", "Visora.Shared\Visora.Shared.csproj"

Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Core", "Core"
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Modules", "Modules"
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Shell", "Shell"

Global
    GlobalSection(NestedProjects) = preSolution
        {6AA0176C-ECB7-7005-D547-EBAF86BF6CB2} = {02EA681E-C7D8-13C7-8484-4AC65E1B71E8}  // Contracts in Core
        {2710BDC3-DABD-4AB7-B2DB-50CD593D9CDB} = {02EA681E-C7D8-13C7-8484-4AC65E1B71E8}  // Shared in Core
    EndGlobalSection
EndGlobal
```

---

## Project Categories

### 1. Contract Projects (Visora.Contracts)

**Purpose**: Define abstractions with zero implementation dependencies

**Characteristics**:
- No external dependencies (except .NET BCL)
- Abstract base classes and interfaces only
- Descriptor records and enums
- Minimal logic (validation only)

**Structure**:
```
/Visora.Contracts/
├── Modules/
│   ├── VisoraModule.cs              # Abstract base
│   ├── ModuleDescriptor.cs          # Metadata record
│   ├── ModuleContext.cs             # Context holder
│   ├── ModuleDiscoveryContext.cs    # Discovery context
│   └── ModuleRuntimeHints.cs        # Runtime metadata
├── Commands/
│   ├── VisoraCommand.cs             # Abstract base
│   ├── CommandDescriptor.cs         # Metadata record
│   ├── CommandContext.cs            # Context holder
│   └── CommandResult.cs             # Result type
├── Components/
│   ├── VisoraComponent.cs           # Abstract base
│   ├── ComponentDescriptor.cs       # Metadata record
│   └── ComponentContext.cs          # Context holder
├── Common/
│   └── ICapabilityProvider.cs       # Core interface
└── Visora.Contracts.csproj
```

**Project File**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Contracts</RootNamespace>
  </PropertyGroup>
  <!-- NO DEPENDENCIES -->
</Project>
```

### 2. Core Projects (Visora.Core)

**Purpose**: Provide base implementations and infrastructure

**Characteristics**:
- References Visora.Contracts
- References Visora.Shared
- Implements module discovery and loading
- Provides base Component/Module classes

**Structure**:
```
/Visora.Core/
├── Modules/
│   ├── ModuleCatalog.cs             # Module registry
│   ├── ModuleCatalogOptions.cs      # Configuration
│   ├── ModuleHandle.cs              # Module wrapper
│   └── ModuleLocator.cs             # Discovery logic
├── Components/
│   └── (component infrastructure)
├── Capabilities/
│   └── CapabilityProviders.cs       # Capability system
├── Module.cs                         # Convenience base
├── Component.cs                      # Convenience base
└── Visora.Core.csproj
```

**Project File**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Core</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="McMaster.NETCore.Plugins" Version="2.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Shared\Visora.Shared.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### 3. Shared Libraries (Visora.Shared)

**Purpose**: Cross-cutting utilities and helpers

**Characteristics**:
- Minimal dependencies
- Pure utility functions
- No business logic
- Platform-agnostic

**Typical Contents**:
```
/Visora.Shared/
├── Extensions/
│   ├── StringExtensions.cs
│   └── CollectionExtensions.cs
├── Utilities/
│   ├── PathHelper.cs
│   └── FileSystemHelper.cs
└── Visora.Shared.csproj
```

### 4. Host Projects (Visora.Shell, Visora.CLI, etc.)

**Purpose**: Application entry points and hosting logic

**Characteristics**:
- References Core and Contracts
- Executable or library that hosts modules
- UI or CLI interaction logic
- Module orchestration

**Structure**:
```
/Visora.Shell/
├── Services/
│   └── ShellServices.cs
├── Hosting/
│   └── ShellHost.cs
├── (shell-specific code)
└── Visora.Shell.csproj

/Visora.CLI/
├── Program.cs                        # Entry point
├── Commands/
│   └── (CLI-specific commands)
└── Visora.CLI.csproj
```

**Example Host Project File (Visora.CLI)**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.CLI</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
    <ProjectReference Include="..\Visora.Shell\Visora.Shell.csproj" />
  </ItemGroup>
</Project>
```

### 5. Module Projects (Visora.Shell.Commands.Core, etc.)

**Purpose**: Pluggable extensions to the platform

**Characteristics**:
- References Core and Contracts
- Builds as `.vixm.dll` (VISORA extension module)
- Contains Module, Component, and Command implementations
- Self-contained functionality

**Structure**:
```
/Visora.Shell.Commands.Core/
├── Commands/
│   ├── PingCommand.cs
│   ├── EnvironmentInfoCommand.cs
│   └── ModuleProbeCommand.cs
├── Components/
│   └── CoreUtilitiesComponent.cs
├── ShellCommandsModule.cs            # Module entry point
└── Visora.Shell.Commands.Core.csproj
```

**Example Module Project File**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Shell.Commands.Core</RootNamespace>
    <AssemblyName>VSCC.vixm</AssemblyName>        <!-- .vixm.dll naming -->
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

---

## Dependency Direction

### Dependency Flow Diagram

```
┌─────────────────────────────────────────────────────────┐
│                     Application Layer                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │ Visora.CLI  │  │Visora.Shell │  │Visora.XXXXX │     │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘     │
│         │                │                │             │
└─────────┼────────────────┼────────────────┼─────────────┘
          │                │                │
          └────────┬───────┴────────┬───────┘
                   ▼                ▼
┌─────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                    │
│         ┌──────────────────┐  ┌──────────────────┐      │
│         │  Visora.Core     │  │ Visora.Shared    │      │
│         └────────┬─────────┘  └──────────────────┘      │
│                  │                                       │
└──────────────────┼───────────────────────────────────────┘
                   ▼
┌─────────────────────────────────────────────────────────┐
│                    Contract Layer                        │
│              ┌──────────────────┐                        │
│              │ Visora.Contracts │                        │
│              └──────────────────┘                        │
└─────────────────────────────────────────────────────────┘
                   ▲
                   │
┌──────────────────┴───────────────────────────────────────┐
│                    Module Layer                          │
│  ┌──────────────────────────┐  ┌──────────────────┐     │
│  │Visora.Shell.Commands.Core│  │Visora.Terminal   │     │
│  └──────────────────────────┘  └──────────────────┘     │
│  (Modules reference Core and Contracts)                  │
└─────────────────────────────────────────────────────────┘
```

### Dependency Rules

| Project Type | Can Reference | Cannot Reference |
|--------------|---------------|------------------|
| **Visora.Contracts** | .NET BCL only | Any VISORA project |
| **Visora.Shared** | .NET BCL, Contracts (optional) | Core, Hosts, Modules |
| **Visora.Core** | Contracts, Shared, NuGet packages | Hosts, Modules |
| **Hosts** | Core, Contracts, Shared | Modules (except by loading) |
| **Modules** | Core, Contracts, Shared | Hosts, other Modules |

### Example Dependency Chain

```
Visora.CLI (Host)
  └─> Visora.Core
       ├─> Visora.Contracts
       └─> Visora.Shared

Visora.Shell.Commands.Core (Module)
  └─> Visora.Core
       ├─> Visora.Contracts
       └─> Visora.Shared
```

### Why This Dependency Direction?

1. **Contracts are Stable**: Changes to contracts affect everything, so keep them minimal
2. **Core is Reusable**: Multiple hosts can use the same core
3. **Modules are Isolated**: Modules don't know about each other or hosts
4. **Testability**: Each layer can be tested independently

---

## Namespace Hierarchy

### Root Namespaces by Project

| Project | Root Namespace | Matches |
|---------|----------------|---------|
| Visora.Contracts | `Visora.Contracts` | Project name |
| Visora.Core | `Visora.Core` | Project name |
| Visora.Shared | `Visora.Shared` | Project name |
| Visora.Shell | `Visora.Shell` | Project name |
| Visora.CLI | `Visora.CLI` | Project name |
| Visora.Terminal | `Visora.Terminal` | Project name |
| Visora.Shell.Commands.Core | `Visora.Shell.Commands.Core` | Project name |

### Namespace Structure Example

```
Visora.Contracts/
  namespace Visora.Contracts                     # Root (rarely used)
  namespace Visora.Contracts.Modules             # /Modules/ folder
  namespace Visora.Contracts.Commands            # /Commands/ folder
  namespace Visora.Contracts.Components          # /Components/ folder
  namespace Visora.Contracts.Common              # /Common/ folder

Visora.Core/
  namespace Visora.Core                          # Root (Module.cs, Component.cs)
  namespace Visora.Core.Modules                  # /Modules/ folder
  namespace Visora.Core.Components               # /Components/ folder
  namespace Visora.Core.Capabilities             # /Capabilities/ folder

Visora.Shell.Commands.Core/
  namespace Visora.Shell.Commands.Core           # Root (ShellCommandsModule.cs)
  namespace Visora.Shell.Commands.Core.Commands  # /Commands/ folder
  namespace Visora.Shell.Commands.Core.Components # /Components/ folder
```

### Namespace Guidelines

1. **Root namespace = Project name** (configured in .csproj)
2. **Sub-namespaces = Folder structure** (exactly)
3. **Use PascalCase** for all namespace segments
4. **Avoid deep nesting** (prefer 3-4 levels max)
5. **Logical grouping** over technical grouping

---

## Layer Separation Rules

### Contract Layer Rules

**Allowed**:
- Abstract base classes
- Interfaces
- Record types for descriptors
- Enums
- Simple validation logic

**Prohibited**:
- Concrete implementations
- External dependencies (except .NET BCL)
- Business logic
- Static utility methods (put in Shared instead)

**Example Contract**:
```csharp
namespace Visora.Contracts.Modules;

/// <summary>
/// Base type for Visora modules.
/// </summary>
public abstract class VisoraModule : IAsyncDisposable
{
    public abstract ModuleDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask ShutdownAsync(ModuleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Core Layer Rules

**Allowed**:
- Implementations of contract abstractions
- Infrastructure services (module loading, discovery)
- Convenience base classes
- Framework integration (dependency injection, logging)

**Prohibited**:
- UI logic (belongs in hosts)
- Module-specific logic (belongs in modules)
- Direct file I/O without abstraction

**Example Core Implementation**:
```csharp
namespace Visora.Core;

// Convenience base that adds no behavior, just simplifies inheritance
public abstract class Module : VisoraModule
{
    // No additional members, just a simpler base
}
```

### Module Layer Rules

**Allowed**:
- Concrete Module implementation
- Concrete Component implementations
- Concrete Command implementations
- Module-specific logic

**Prohibited**:
- Accessing host internals
- Referencing other modules
- Platform-specific code (without abstraction)

**Example Module**:
```csharp
namespace Visora.Shell.Commands.Core;

public sealed class ShellCommandsModule : Module
{
    private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands",
        version: new Version(0, 1, 0),
        description: "Baseline commands for diagnostics and exploration.");

    public override ModuleDescriptor Descriptor => ModuleInfo;

    public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => base.DiscoverComponents(context);
}
```

---

## Reference Management

### Project Reference Patterns

#### Contracts Project (No References)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Contracts</RootNamespace>
  </PropertyGroup>
  <!-- NO ItemGroup with references -->
</Project>
```

#### Core Project (References Contracts + Shared)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Core</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="McMaster.NETCore.Plugins" Version="2.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Shared\Visora.Shared.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

#### Module Project (References Core + Contracts)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Shell.Commands.Core</RootNamespace>
    <AssemblyName>VSCC.vixm</AssemblyName>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### Reference Guidelines

1. **Use relative paths** for project references
2. **Reference only what you need** (don't rely on transitive references)
3. **Order references** (Contracts first, then Core, then specific libs)
4. **NuGet packages** go in separate ItemGroup
5. **Version pinning** for external packages

---

## Build Output Organization

### Default Build Output

```
/Visora/src/
├── Visora.Contracts/
│   ├── bin/
│   │   ├── Debug/net9.0/
│   │   │   ├── Visora.Contracts.dll
│   │   │   └── Visora.Contracts.pdb
│   │   └── Release/net9.0/
│   └── obj/                          # Intermediate files (ignored)
├── Visora.Core/
│   ├── bin/
│   │   ├── Debug/net9.0/x64/
│   │   │   ├── Visora.Core.dll
│   │   │   ├── Visora.Contracts.dll   # Copied dependency
│   │   │   └── Visora.Shared.dll      # Copied dependency
│   │   └── Release/net9.0/x64/
│   └── obj/
└── Visora.Shell.Commands.Core/
    ├── bin/
    │   ├── Debug/net9.0/
    │   │   ├── VSCC.vixm.dll          # Module assembly
    │   │   ├── Visora.Core.dll         # Dependency
    │   │   └── Visora.Contracts.dll    # Dependency
    │   └── Release/net9.0/
    └── obj/
```

### Module Build Output Structure

For modules, the assembly name follows the pattern `<ShortName>.vixm.dll`:

```xml
<PropertyGroup>
  <AssemblyName>VSCC.vixm</AssemblyName>  <!-- Results in VSCC.vixm.dll -->
</PropertyGroup>
```

**Examples**:
- `Visora.Shell.Commands.Core` → `VSCC.vixm.dll`
- `Visora.Terminal` → `VT.vixm.dll`
- `Visora.GitIntegration` → `VGI.vixm.dll`

---

## Module Deployment Structure

### Deployment Layout

Modules are deployed in a versioned folder structure:

```
/Modules/
├── <module-id>/
│   └── <version>/
│       ├── <module>.vixm.dll
│       ├── <dependencies>.dll
│       ├── <module>.cmd           # Optional shim
│       └── <module>.ps1           # Optional shim
└── ...

Example:
/Modules/
├── visora.shell.commands.core/
│   └── 0.1.0/
│       ├── VSCC.vixm.dll
│       ├── Visora.Core.dll
│       ├── Visora.Contracts.dll
│       └── vscc.cmd
└── visora.terminal/
    └── 1.0.0/
        ├── VT.vixm.dll
        └── VT.cmd
```

### Version Folder Format

```
<major>.<minor>.<build>[-<tag>]

Examples:
  0.1.0                  # Standard version
  1.2.3                  # Release version
  2.0.0-alpha            # Pre-release
  1.5.0-beta.2           # Numbered pre-release
```

### Local vs Global Deployment

| Location | Purpose | Search Order |
|----------|---------|--------------|
| `./Modules/` | Local development modules | 1st (highest priority) |
| `%VISORA_PATH%/Modules/` | User-installed modules | 2nd |
| `%ProgramFiles%/Visora/Modules/` | System-wide modules | 3rd |

### Module Discovery Process

1. **Enumerate probing paths** (local, user, system)
2. **Find all `.vixm.dll` files** recursively
3. **Skip `/obj/` directories** (intermediate build output)
4. **Load assembly** and inspect for VisoraModule type
5. **Extract ModuleDescriptor** and register

**Example from ModuleLocator.cs**:
```csharp
private static bool ShouldSkipPath(string path)
{
    var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar);
    return normalized.Contains(
        Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);
}
```

---

## Project Templates

### Minimal Module Template

**Folder Structure**:
```
/Visora.MyModule/
├── Commands/
│   └── MyCommand.cs
├── Components/
│   └── MyComponent.cs
├── MyModule.cs
└── Visora.MyModule.csproj
```

**MyModule.cs**:
```csharp
using Visora.Core;
using Visora.Contracts.Modules;

namespace Visora.MyModule;

public sealed class MyModule : Module
{
    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "visora.mymodule",
        name: "My Module",
        version: new Version(1, 0, 0),
        description: "Description of my module.");

    public override ModuleDescriptor Descriptor => Info;
}
```

**MyComponent.cs**:
```csharp
using Visora.Core;
using Visora.Contracts.Components;
using Visora.Contracts.Commands;

namespace Visora.MyModule.Components;

public sealed class MyComponent : Component
{
    private static readonly ComponentDescriptor Info = ComponentDescriptor.Create(
        id: "visora.mymodule.main",
        name: "My Component",
        description: "Main component of my module.",
        kind: ComponentKind.Generic);

    public override ComponentDescriptor Descriptor => Info;

    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new MyCommand();
    }
}
```

**MyCommand.cs**:
```csharp
using Visora.Contracts.Commands;

namespace Visora.MyModule.Commands;

public sealed class MyCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "mymodule.mycommand",
        title: "My Command",
        description: "Does something useful.");

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Implementation
        return ValueTask.FromResult(CommandResult.Success("Done!"));
    }
}
```

**Visora.MyModule.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.MyModule</RootNamespace>
    <AssemblyName>VMM.vixm</AssemblyName>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

---

## Anti-Patterns

### 1. Circular Dependencies

```
❌ Bad: Contracts references Core
Visora.Contracts → Visora.Core  (NEVER!)

✅ Good: Core references Contracts
Visora.Core → Visora.Contracts
```

### 2. Modules Referencing Each Other

```
❌ Bad: Direct module-to-module reference
Visora.Module.A → Visora.Module.B

✅ Good: Both reference shared contracts
Visora.Module.A → Visora.Contracts
Visora.Module.B → Visora.Contracts
```

### 3. Deep Folder Nesting

```
❌ Bad: Too many levels
/Commands/Shell/Utilities/Diagnostics/Network/PingCommand.cs

✅ Good: Flat structure with clear names
/Commands/NetworkPingCommand.cs
```

### 4. Mixed Responsibilities

```
❌ Bad: Module and host in same project
/Visora.Shell/
  ShellHost.cs              # Host logic
  ShellModule.cs            # Module logic (wrong!)

✅ Good: Separate concerns
/Visora.Shell/              # Host only
  ShellHost.cs
/Visora.Shell.Extensions/   # Module
  ShellExtensionsModule.cs
```

### 5. Non-Standard Assembly Naming

```
❌ Bad: Module without .vixm suffix
<AssemblyName>VisoraModule</AssemblyName>

✅ Good: Module with .vixm suffix
<AssemblyName>VM.vixm</AssemblyName>
```

### 6. Namespace Mismatch

```
❌ Bad: Namespace doesn't match folder
// File: /Commands/PingCommand.cs
namespace Visora.Shell.Commands.Core;  // Wrong!

✅ Good: Namespace matches folder
// File: /Commands/PingCommand.cs
namespace Visora.Shell.Commands.Core.Commands;
```

### 7. Missing Root Namespace

```
❌ Bad: No RootNamespace specified
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <!-- Missing RootNamespace -->
</PropertyGroup>

✅ Good: Explicit RootNamespace
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <RootNamespace>Visora.MyProject</RootNamespace>
</PropertyGroup>
```

---

## Cross-References

### Related Documentation

- **[Naming Conventions](./naming-conventions.md)**: Identifier naming rules
- **[Code Style](./code-style.md)**: C# coding standards
- **[Assembly Conventions](./assembly-conventions.md)**: Build and deployment
- **[Module Pattern](../patterns/module-pattern.md)**: Module architecture
- **[Component Pattern](../patterns/component-pattern.md)**: Component design
- **[Dependency Injection](../patterns/dependency-injection-pattern.md)**: DI patterns

### Pattern Documents

- **Architecture Overview**: `../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md`
- **Quick Reference**: `../PATTERNS_QUICK_REFERENCE.md`
- **Module Discovery**: `../patterns/module-discovery-pattern.md`
- **Layered Architecture**: `../patterns/layered-architecture-pattern.md`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-10 | Initial project structure documentation |

---

**See Also:**
- [VISORA Architecture Overview](../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md)
- [Pattern Quick Reference](../PATTERNS_QUICK_REFERENCE.md)
- [Quick Start Guide](../00-quick-start/README.md)
