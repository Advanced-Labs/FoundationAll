# VISORA Assembly Conventions

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Applies To:** All VISORA assemblies and modules

---

## Table of Contents

1. [Overview](#overview)
2. [Assembly Naming](#assembly-naming)
3. [Module Assembly Pattern](#module-assembly-pattern)
4. [Version Folder Structure](#version-folder-structure)
5. [Packaging Conventions](#packaging-conventions)
6. [Shim Generation](#shim-generation)
7. [Deployment Locations](#deployment-locations)
8. [Module Discovery](#module-discovery)
9. [Assembly Metadata](#assembly-metadata)
10. [Dependency Management](#dependency-management)
11. [Build Configuration](#build-configuration)
12. [Anti-Patterns](#anti-patterns)
13. [Cross-References](#cross-references)

---

## Overview

VISORA uses a consistent assembly naming and deployment strategy to support dynamic module loading and versioning.

### Key Principles

1. **Distinctive Module Naming**: `.vixm.dll` extension identifies modules
2. **Version Isolation**: Each module version in separate folder
3. **Global Discovery**: Modules loadable from multiple locations
4. **Metadata-Driven**: Assembly attributes provide discovery hints
5. **Shim Support**: Optional executable shims for direct invocation

---

## Assembly Naming

### Standard Library Assemblies

Libraries follow standard .NET naming conventions.

| Project | Assembly Name | Description |
|---------|---------------|-------------|
| Visora.Contracts | Visora.Contracts.dll | Contract definitions |
| Visora.Core | Visora.Core.dll | Core implementation |
| Visora.Shared | Visora.Shared.dll | Shared utilities |
| Visora.Shell | Visora.Shell.dll | Shell library |

**Pattern**: `<ProjectName>.dll`

```xml
<!-- Default: Assembly name matches project name -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>Visora.Contracts</RootNamespace>
    <!-- AssemblyName not specified, defaults to project name -->
  </PropertyGroup>
</Project>
```

### Executable Assemblies

Executables use `.exe` extension (Windows) or no extension (cross-platform).

| Project | Assembly Name | Description |
|---------|---------------|-------------|
| Visora.CLI | Visora.CLI.exe | CLI executable |
| VISORA Windows | VISORA Windows.exe | Windows GUI host |

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
```

---

## Module Assembly Pattern

### The `.vixm.dll` Extension

VISORA modules use the **`.vixm`** (VISORA eXtension Module) suffix before `.dll`.

**Pattern**: `<ShortName>.vixm.dll`

| Project | Assembly Name | Short Name |
|---------|---------------|------------|
| Visora.Shell.Commands.Core | VSCC.vixm.dll | VSCC |
| Visora.Terminal | VT.vixm.dll | VT |
| Visora.CLI.Module | VCLIM.vixm.dll | VCLIM |
| Visora.GitIntegration | VGI.vixm.dll | VGI |

### Configuring Module Assembly Name

```xml
<!-- File: Visora.Shell.Commands.Core/Visora.Shell.Commands.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Shell.Commands.Core</RootNamespace>

    <!-- Module assembly naming -->
    <AssemblyName>VSCC.vixm</AssemblyName>

    <!-- Disable reference assembly generation -->
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### Why `.vixm.dll`?

1. **Discoverable**: Easy to find module assemblies
2. **Distinctive**: Clear separation from regular libraries
3. **Filterable**: File system searches can target `*.vixm.dll`
4. **Recognizable**: Developers immediately know this is a module

### Short Name Guidelines

| Guideline | Example |
|-----------|---------|
| **Acronym from project name** | Visora.Shell.Commands.Core → VSCC |
| **Keep under 8 characters** | Visora.Terminal → VT |
| **All uppercase** | VSCC, VT, VCLIM |
| **Use initials of each segment** | Visora.Git.Integration → VGI |
| **Avoid ambiguity** | VC could be VisoraCore or VisoraCommands (use VCR, VCM) |

---

## Version Folder Structure

### Deployment Layout

Modules are deployed in a hierarchical folder structure:

```
/Modules/
  <module-id>/
    <version>/
      <assembly-name>.vixm.dll
      <dependencies>.dll
      <shims>
```

### Concrete Example

```
/Modules/
├── visora.shell.commands.core/
│   ├── 0.1.0/
│   │   ├── VSCC.vixm.dll
│   │   ├── Visora.Core.dll
│   │   ├── Visora.Contracts.dll
│   │   ├── Visora.Shared.dll
│   │   ├── vscc.cmd
│   │   └── vscc.ps1
│   └── 0.2.0/                    # Newer version
│       ├── VSCC.vixm.dll
│       └── ...
├── visora.terminal/
│   └── 1.0.0/
│       ├── VT.vixm.dll
│       ├── Visora.Core.dll
│       ├── Visora.Contracts.dll
│       └── vt.cmd
└── visora.diagnostics/
    └── 2.1.0-beta/
        ├── VD.vixm.dll
        └── ...
```

### Version Folder Naming

**Format**: `<major>.<minor>.<patch>[-<prerelease>]`

Based on Semantic Versioning 2.0.0:

| Version String | Meaning |
|----------------|---------|
| `0.1.0` | Initial development version |
| `1.0.0` | First stable release |
| `1.2.3` | Patch release |
| `2.0.0-alpha` | Pre-release alpha |
| `2.0.0-beta.1` | Pre-release beta with revision |
| `2.0.0-rc.1` | Release candidate |

**Examples**:
```csharp
// In module descriptor
version: new Version(0, 1, 0)        → Folder: 0.1.0/
version: new Version(1, 2, 3)        → Folder: 1.2.3/
version: new Version(2, 0, 0)        → Folder: 2.0.0/

// With prerelease tag (requires custom metadata)
"2.0.0-alpha"                        → Folder: 2.0.0-alpha/
"1.5.0-beta.2"                       → Folder: 1.5.0-beta.2/
```

### Side-by-Side Versions

Multiple versions can coexist:

```
/Modules/visora.shell.commands.core/
  ├── 0.1.0/          # Older version
  │   └── VSCC.vixm.dll
  ├── 0.2.0/          # Current stable
  │   └── VSCC.vixm.dll
  └── 0.3.0-beta/     # Next version in testing
      └── VSCC.vixm.dll
```

Hosts can:
- Load specific version by path
- Load latest stable version
- Support version constraints

---

## Packaging Conventions

### Single Module Per Assembly

Each module assembly contains exactly one `VisoraModule` implementation.

```csharp
// ✅ Good: One module per assembly
// File: VSCC.vixm.dll
namespace Visora.Shell.Commands.Core;

public sealed class ShellCommandsModule : Module
{
    public override ModuleDescriptor Descriptor => _descriptor;
}

// ❌ Bad: Multiple modules in one assembly
public sealed class ShellCommandsModule : Module { }
public sealed class ShellUtilitiesModule : Module { }  // Separate assembly!
```

### Module Assembly Contents

A module assembly should contain:

1. **One VisoraModule implementation** (module entry point)
2. **Zero or more VisoraComponent implementations**
3. **Zero or more VisoraCommand implementations**
4. **Supporting types** (helpers, models, etc.)

**Example Structure**:
```
VSCC.vixm.dll
├── ShellCommandsModule           (1 module)
├── CoreUtilitiesComponent        (1 component)
├── PingCommand                   (3 commands)
├── EnvironmentInfoCommand
├── ModuleProbeCommand
└── (internal helper types)
```

### Dependencies in Module Folder

All dependencies are copied to the module's version folder:

```
/Modules/visora.shell.commands.core/0.1.0/
├── VSCC.vixm.dll              # The module
├── Visora.Core.dll            # Dependency
├── Visora.Contracts.dll       # Dependency
├── Visora.Shared.dll          # Dependency
└── McMaster.NETCore.Plugins.dll  # Transitive dependency
```

This enables:
- **Isolation**: Each module version has its own dependencies
- **No conflicts**: Different modules can use different dependency versions
- **Self-contained**: Module folder can be copied/moved independently

---

## Shim Generation

### What are Shims?

Shims are small wrapper scripts that allow invoking a module as if it were an executable.

**Purpose**:
- Provide command-line access to modules
- Enable PATH-based discovery
- Support platform-specific execution

### Shim Types

| Type | Extension | Platform | Purpose |
|------|-----------|----------|---------|
| **Command Script** | `.cmd` | Windows | Batch file wrapper |
| **PowerShell Script** | `.ps1` | Windows/Cross-platform | PowerShell wrapper |
| **Shell Script** | `.sh` | Linux/macOS | Bash wrapper |

### Command Script Shim (.cmd)

```batch
@echo off
REM Shim for Visora Shell Commands Core module
REM Generated: 2025-11-10

SET MODULE_PATH=%~dp0VSCC.vixm.dll
SET VISORA_CLI=visora.exe

IF EXIST "%MODULE_PATH%" (
    "%VISORA_CLI%" module invoke "%MODULE_PATH%" %*
) ELSE (
    ECHO ERROR: Module assembly not found: %MODULE_PATH%
    EXIT /B 1
)
```

**Location**: `/Modules/visora.shell.commands.core/0.1.0/vscc.cmd`

**Usage**:
```cmd
vscc ping
vscc env.info
```

### PowerShell Script Shim (.ps1)

```powershell
#!/usr/bin/env pwsh
# Shim for Visora Shell Commands Core module
# Generated: 2025-11-10

$ModulePath = Join-Path $PSScriptRoot "VSCC.vixm.dll"
$VisoraCli = "visora.exe"

if (Test-Path $ModulePath) {
    & $VisoraCli module invoke $ModulePath @args
} else {
    Write-Error "Module assembly not found: $ModulePath"
    exit 1
}
```

**Location**: `/Modules/visora.shell.commands.core/0.1.0/vscc.ps1`

**Usage**:
```powershell
.\vscc.ps1 ping
.\vscc.ps1 env.info
```

### Bash Script Shim (.sh)

```bash
#!/bin/bash
# Shim for Visora Shell Commands Core module
# Generated: 2025-11-10

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE_PATH="${SCRIPT_DIR}/VSCC.vixm.dll"
VISORA_CLI="visora"

if [ -f "$MODULE_PATH" ]; then
    "$VISORA_CLI" module invoke "$MODULE_PATH" "$@"
else
    echo "ERROR: Module assembly not found: $MODULE_PATH" >&2
    exit 1
fi
```

**Location**: `/Modules/visora.shell.commands.core/0.1.0/vscc.sh`

**Usage**:
```bash
./vscc.sh ping
./vscc.sh env.info
```

### Shim Naming Convention

Shim base name derives from module runtime hints:

```csharp
// In module descriptor
runtimeHints: ModuleRuntimeHints.Create(
    executableRelativePath: null,
    exposeExecutableGlobally: false,
    aliases: new[] { "vscc" })  // Shim base name

// Generated shims:
// - vscc.cmd
// - vscc.ps1
// - vscc.sh
```

### When to Generate Shims

Shims should be generated when:
- Module has `ExposeExecutableGlobally = true` in RuntimeHints
- Module provides commands intended for CLI invocation
- Module includes `Aliases` in RuntimeHints

**Example**:
```csharp
runtimeHints: ModuleRuntimeHints.Create(
    executableRelativePath: null,
    exposeExecutableGlobally: true,    // Generate shims
    aliases: new[] { "vscc", "shell" }) // Generate vscc.cmd and shell.cmd
```

---

## Deployment Locations

### Probing Path Hierarchy

VISORA searches for modules in multiple locations with a defined priority:

| Priority | Location | Purpose | Example |
|----------|----------|---------|---------|
| 1 (Highest) | `./Modules/` | Local development | `C:\MyProject\Modules\` |
| 2 | `%VISORA_PATH%/Modules/` | User-installed | `C:\Users\Name\.visora\Modules\` |
| 3 (Lowest) | `%ProgramFiles%/Visora/Modules/` | System-wide | `C:\Program Files\Visora\Modules\` |

### Local Development Modules

**Location**: `./Modules/` (relative to current directory)

**Use Cases**:
- Project-specific modules
- Module development and testing
- Temporary overrides

**Example**:
```
C:\Projects\MyApp\
├── Modules\                       # Local modules (highest priority)
│   └── visora.custom\
│       └── 1.0.0\
│           └── Custom.vixm.dll
└── MyApp.exe
```

### User Modules

**Location**: `%VISORA_PATH%/Modules/` or `~/.visora/Modules/`

**Use Cases**:
- User-installed modules
- Personal customizations
- Non-admin installations

**Windows Example**:
```
C:\Users\JohnDoe\.visora\
└── Modules\
    ├── visora.shell.commands.core\
    │   └── 0.1.0\
    └── visora.terminal\
        └── 1.0.0\
```

**Linux/macOS Example**:
```
/home/johndoe/.visora/
└── Modules/
    ├── visora.shell.commands.core/
    │   └── 0.1.0/
    └── visora.terminal/
        └── 1.0.0/
```

### System-Wide Modules

**Location**: `%ProgramFiles%/Visora/Modules/` or `/usr/share/visora/Modules/`

**Use Cases**:
- Pre-installed modules
- System administrator deployments
- Shared across all users

**Windows Example**:
```
C:\Program Files\Visora\
└── Modules\
    ├── visora.shell.commands.core\
    └── visora.terminal\
```

**Linux Example**:
```
/usr/share/visora/
└── Modules/
    ├── visora.shell.commands.core/
    └── visora.terminal/
```

### Deployment Priority Rules

1. **Highest priority wins**: Local `./Modules/` overrides user and system
2. **Later versions preferred**: When multiple versions exist in same location
3. **Search order matters**: Stops at first match unless enumerating all

---

## Module Discovery

### Discovery Process

The `ModuleLocator` class implements module discovery:

```csharp
public static IEnumerable<string> EnumerateCandidateFiles(ModuleCatalogOptions options)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 1. Explicit files (highest priority)
    foreach (var explicitFile in options.ExplicitModuleFiles)
    {
        if (File.Exists(explicitFile) && seen.Add(explicitFile))
            yield return explicitFile;
    }

    // 2. Probing paths (in order)
    var searchOption = options.RecurseSubdirectories
        ? SearchOption.AllDirectories
        : SearchOption.TopDirectoryOnly;

    foreach (var root in options.ProbingPaths)
    {
        if (!Directory.Exists(root) || ShouldSkipPath(root))
            continue;

        foreach (var file in Directory.EnumerateFiles(root, options.SearchPattern, searchOption))
        {
            if (ShouldSkipPath(file) || !seen.Add(file))
                continue;

            yield return file;
        }
    }
}
```

### Skipping Build Artifacts

Discovery skips intermediate build output:

```csharp
private static bool ShouldSkipPath(string path)
{
    var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar);

    // Skip /obj/ directories (intermediate build output)
    return normalized.Contains(
        Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);
}
```

**Skipped Paths**:
```
✅ Loaded:
  C:\Modules\visora.shell.commands.core\0.1.0\VSCC.vixm.dll

❌ Skipped:
  C:\Projects\Visora.Shell.Commands.Core\obj\Debug\net9.0\VSCC.vixm.dll
  C:\Projects\Visora.Shell.Commands.Core\bin\obj\VSCC.vixm.dll
```

### Search Patterns

Default search pattern: `*.vixm.dll`

```csharp
var options = new ModuleCatalogOptions
{
    SearchPattern = "*.vixm.dll",      // Find all module assemblies
    RecurseSubdirectories = true       // Search nested folders
};
```

**Pattern Examples**:
| Pattern | Matches |
|---------|---------|
| `*.vixm.dll` | All modules |
| `VS*.vixm.dll` | Modules starting with VS |
| `VSCC.vixm.dll` | Specific module assembly |

### Recursive vs Non-Recursive Search

**Recursive** (default):
```
/Modules/
  visora.shell.commands.core/
    0.1.0/
      VSCC.vixm.dll           ✅ Found (nested)
```

**Non-Recursive**:
```
/Modules/
  visora.shell.commands.core/
    0.1.0/
      VSCC.vixm.dll           ❌ Not found (too deep)
  VSCC.vixm.dll               ✅ Found (top-level)
```

---

## Assembly Metadata

### Required Assembly Attributes

All VISORA assemblies should include:

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

// Assembly identity
[assembly: AssemblyTitle("Visora Shell Commands Core")]
[assembly: AssemblyDescription("Baseline commands for diagnostics and exploration.")]
[assembly: AssemblyCompany("Advanced Labs")]
[assembly: AssemblyProduct("VISORA Platform")]
[assembly: AssemblyCopyright("Copyright © 2025 Advanced Labs")]

// Versioning
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

// Platform
[assembly: ComVisible(false)]
[assembly: Guid("a1234567-89ab-cdef-0123-456789abcdef")]
```

### Configuring in .csproj

Modern SDK-style projects can set metadata in the project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AssemblyName>VSCC.vixm</AssemblyName>

    <!-- Assembly metadata -->
    <AssemblyTitle>Visora Shell Commands Core</AssemblyTitle>
    <Description>Baseline commands for diagnostics and exploration.</Description>
    <Company>Advanced Labs</Company>
    <Product>VISORA Platform</Product>
    <Copyright>Copyright © 2025 Advanced Labs</Copyright>

    <!-- Versioning -->
    <Version>0.1.0</Version>
    <FileVersion>0.1.0.0</FileVersion>
    <AssemblyVersion>0.1.0.0</AssemblyVersion>

    <!-- Generate assembly info automatically -->
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>
</Project>
```

### Module Metadata Pattern

Modules embed metadata in `ModuleDescriptor`:

```csharp
private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Visora Shell Commands",
    version: new Version(0, 1, 0),
    description: "Baseline commands for diagnostics and exploration.",
    tags: new Dictionary<string, string>
    {
        ["category"] = "shell",
        ["platform"] = "cross-platform",
        ["author"] = "Advanced Labs"
    },
    runtimeHints: ModuleRuntimeHints.Create(
        executableRelativePath: null,
        exposeExecutableGlobally: false,
        aliases: new[] { "vscc" }));
```

**Metadata Fields**:
| Field | Source | Purpose |
|-------|--------|---------|
| **Id** | ModuleDescriptor | Unique module identifier |
| **Name** | ModuleDescriptor | Human-readable name |
| **Version** | ModuleDescriptor | Module version |
| **Description** | ModuleDescriptor / Assembly | Module purpose |
| **Tags** | ModuleDescriptor | Searchable metadata |
| **RuntimeHints** | ModuleDescriptor | Execution hints |

---

## Dependency Management

### Module Dependencies

Modules reference core VISORA assemblies:

```xml
<ItemGroup>
  <!-- Required references -->
  <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />

  <!-- Optional references -->
  <ProjectReference Include="..\Visora.Shared\Visora.Shared.csproj" />
</ItemGroup>
```

### Dependency Copying

By default, all dependencies are copied to output directory:

```xml
<PropertyGroup>
  <!-- Copy dependencies to output (default: true) -->
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

**Result**:
```
/bin/Debug/net9.0/
├── VSCC.vixm.dll
├── Visora.Core.dll           # Copied
├── Visora.Contracts.dll      # Copied
├── Visora.Shared.dll         # Copied
└── McMaster.NETCore.Plugins.dll  # Transitive dependency copied
```

### Avoiding Dependency Conflicts

Each module version has its own dependency copies:

```
/Modules/
├── visora.shell.commands.core/
│   └── 0.1.0/
│       ├── VSCC.vixm.dll
│       └── Visora.Core.dll    # Version X
└── visora.terminal/
    └── 1.0.0/
        ├── VT.vixm.dll
        └── Visora.Core.dll    # Could be different version
```

This allows:
- **Isolation**: Different modules can use different Core versions
- **Stability**: Updating one module doesn't break others
- **Testing**: New versions can be tested without affecting existing modules

### Shared Dependencies

For system-wide shared assemblies:

```
%ProgramFiles%/Visora/
├── bin/                      # Shared assemblies
│   ├── Visora.Core.dll
│   ├── Visora.Contracts.dll
│   └── Visora.Shared.dll
└── Modules/
    └── visora.shell.commands.core/
        └── 0.1.0/
            └── VSCC.vixm.dll  # References shared assemblies
```

Module loader can resolve from shared location to reduce duplication.

---

## Build Configuration

### Debug vs Release

Standard build configurations apply:

**Debug Build**:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <DebugType>full</DebugType>
  <Optimize>false</Optimize>
  <DefineConstants>DEBUG;TRACE</DefineConstants>
</PropertyGroup>
```

**Output**: `/bin/Debug/net9.0/VSCC.vixm.dll`

**Release Build**:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DebugType>pdbonly</DebugType>
  <Optimize>true</Optimize>
  <DefineConstants>TRACE</DefineConstants>
</PropertyGroup>
```

**Output**: `/bin/Release/net9.0/VSCC.vixm.dll`

### Platform Targeting

Some projects target specific platforms:

```xml
<!-- Cross-platform (Any CPU) -->
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
</PropertyGroup>

<!-- x64 only -->
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Platforms>x64</Platforms>
</PropertyGroup>
```

**Examples**:
- `Visora.Contracts`: Any CPU (cross-platform)
- `Visora.Core`: x64 only
- `Visora.CLI`: x64 only

### Output Path Customization

Customize output path if needed:

```xml
<PropertyGroup>
  <OutputPath>..\..\Modules\visora.shell.commands.core\0.1.0\</OutputPath>
</PropertyGroup>
```

This builds directly to deployment location.

### Post-Build Events

Generate shims or copy files after build:

```xml
<Target Name="GenerateShims" AfterTargets="Build">
  <Exec Command="powershell -File &quot;$(ProjectDir)tools\generate-shims.ps1&quot; -AssemblyPath &quot;$(TargetPath)&quot;" />
</Target>
```

---

## Anti-Patterns

### 1. Non-Standard Module Naming

```
❌ Bad: Regular .dll extension
<AssemblyName>ShellCommands</AssemblyName>
Output: ShellCommands.dll

✅ Good: .vixm.dll extension
<AssemblyName>VSCC.vixm</AssemblyName>
Output: VSCC.vixm.dll
```

### 2. Flat Module Deployment

```
❌ Bad: All versions in one folder
/Modules/
  VSCC-0.1.0.vixm.dll
  VSCC-0.2.0.vixm.dll
  VT-1.0.0.vixm.dll

✅ Good: Hierarchical structure
/Modules/
  visora.shell.commands.core/
    0.1.0/
      VSCC.vixm.dll
    0.2.0/
      VSCC.vixm.dll
```

### 3. Missing ProduceReferenceAssembly

```
❌ Bad: Generates reference assembly (unnecessary for modules)
<PropertyGroup>
  <!-- Default is true in some SDK versions -->
</PropertyGroup>

✅ Good: Disable reference assembly
<PropertyGroup>
  <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
</PropertyGroup>
```

### 4. Multiple Modules Per Assembly

```
❌ Bad: Two modules in one assembly
public class ModuleA : VisoraModule { }
public class ModuleB : VisoraModule { }

✅ Good: One module per assembly
// Assembly A.vixm.dll
public class ModuleA : VisoraModule { }

// Assembly B.vixm.dll
public class ModuleB : VisoraModule { }
```

### 5. Hard-Coded Paths in Shims

```
❌ Bad: Hard-coded absolute path
SET MODULE_PATH=C:\Visora\Modules\VSCC.vixm.dll

✅ Good: Relative path from shim location
SET MODULE_PATH=%~dp0VSCC.vixm.dll
```

### 6. Missing Version in Folder Name

```
❌ Bad: No version folder
/Modules/visora.shell.commands.core/
  VSCC.vixm.dll

✅ Good: Version folder
/Modules/visora.shell.commands.core/
  0.1.0/
    VSCC.vixm.dll
```

### 7. Including Build Artifacts in Discovery

```
❌ Bad: Loading from /obj/ directories
/obj/Debug/net9.0/VSCC.vixm.dll  # Intermediate build output!

✅ Good: Skip /obj/ directories
ShouldSkipPath() returns true for paths containing "/obj/"
```

---

## Cross-References

### Related Documentation

- **[Naming Conventions](./naming-conventions.md)**: Module ID and assembly naming
- **[Project Structure](./project-structure.md)**: Solution organization
- **[Code Style](./code-style.md)**: C# coding standards
- **[Module Pattern](../patterns/module-pattern.md)**: Module architecture
- **[Deployment Pattern](../patterns/deployment-pattern.md)**: Deployment strategies

### Pattern Documents

- **Module Discovery**: `../patterns/module-discovery-pattern.md`
- **Version Management**: `../patterns/version-management-pattern.md`
- **Plugin Loading**: `../patterns/plugin-loading-pattern.md`

### External References

- **Semantic Versioning**: https://semver.org/
- **McMaster.NETCore.Plugins**: https://github.com/natemcmaster/DotNetCorePlugins
- **.NET Assembly Loading**: https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/overview

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-10 | Initial assembly conventions documentation |

---

## Appendix: Quick Reference Tables

### Assembly Extension Guide

| Type | Extension | Example |
|------|-----------|---------|
| Module | `.vixm.dll` | `VSCC.vixm.dll` |
| Library | `.dll` | `Visora.Core.dll` |
| Executable | `.exe` | `Visora.CLI.exe` |

### Deployment Path Priority

| Priority | Location | Variable |
|----------|----------|----------|
| 1 | `./Modules/` | Current directory |
| 2 | `~/.visora/Modules/` | `%VISORA_PATH%` |
| 3 | `/usr/share/visora/Modules/` | System path |

### Version Folder Examples

| Version Object | Folder Name |
|----------------|-------------|
| `new Version(0, 1, 0)` | `0.1.0/` |
| `new Version(1, 2, 3)` | `1.2.3/` |
| `new Version(2, 0, 0)` | `2.0.0/` |

### Shim Types

| Type | Extension | Platform | Command |
|------|-----------|----------|---------|
| Batch | `.cmd` | Windows | `vscc.cmd ping` |
| PowerShell | `.ps1` | Cross-platform | `./vscc.ps1 ping` |
| Bash | `.sh` | Linux/macOS | `./vscc.sh ping` |

---

**See Also:**
- [VISORA Architecture Overview](../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md)
- [Pattern Quick Reference](../PATTERNS_QUICK_REFERENCE.md)
- [Quick Start Guide](../00-quick-start/README.md)
