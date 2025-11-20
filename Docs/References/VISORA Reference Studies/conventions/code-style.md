# VISORA Code Style Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Applies To:** All VISORA C# code

---

## Table of Contents

1. [Overview](#overview)
2. [C# Language Features](#c-language-features)
3. [Async/Await Patterns](#asyncawait-patterns)
4. [Error Handling](#error-handling)
5. [Resource Disposal](#resource-disposal)
6. [Nullable Reference Types](#nullable-reference-types)
7. [Expression-Bodied Members](#expression-bodied-members)
8. [Pattern Matching](#pattern-matching)
9. [LINQ Usage](#linq-usage)
10. [Immutability Patterns](#immutability-patterns)
11. [Formatting and Style](#formatting-and-style)
12. [Comments and Documentation](#comments-and-documentation)
13. [Anti-Patterns](#anti-patterns)
14. [Cross-References](#cross-references)

---

## Overview

VISORA uses modern C# features to create clean, maintainable code. This guide establishes consistent patterns across the codebase.

### Core Principles

1. **Modern C#**: Use latest language features appropriately
2. **Async Throughout**: Async/await for all I/O operations
3. **Immutability First**: Prefer immutable types and readonly
4. **Null Safety**: Enable nullable reference types
5. **Explicit Intent**: Code should clearly express purpose

### Language Version

All VISORA projects target **.NET 9.0** with **C# 13** features enabled:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <LangVersion>latest</LangVersion>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

---

## C# Language Features

### Records for Descriptors

Use **records** for immutable data transfer objects and descriptors.

```csharp
// ✅ Good: Record with positional parameters
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    ModuleRuntimeHints? RuntimeHints = null)
{
    public static ModuleDescriptor Create(
        string id,
        string name,
        Version version,
        string? description = null,
        IReadOnlyDictionary<string, string>? tags = null,
        ModuleRuntimeHints? runtimeHints = null)
        => new(id, name, version, description, tags, runtimeHints);
}

// ✅ Good: Simple record with defaults
public sealed record CommandUiHint(
    string? MenuPath = null,
    string? Icon = null,
    string? DefaultGesture = null);

// ❌ Bad: Class when record is appropriate
public class ModuleDescriptor
{
    public string Id { get; set; }
    public string Name { get; set; }
    // ... mutable properties
}
```

### Sealed Classes

Use **sealed** for concrete implementations that shouldn't be inherited.

```csharp
// ✅ Good: Sealed concrete class
public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();

    public IReadOnlyList<ModuleHandle> Modules => _modules;

    // ...
}

// ✅ Good: Sealed command implementation
public sealed class PingCommand : VisoraCommand
{
    // No one should inherit from PingCommand
}

// ❌ Bad: Non-sealed concrete class (allows unintended inheritance)
public class ModuleCatalog : IAsyncDisposable
{
    // ...
}
```

### Abstract Base Classes

Use **abstract** for extensibility points in the framework.

```csharp
// ✅ Good: Abstract base for extension
public abstract class VisoraModule : IAsyncDisposable
{
    public abstract ModuleDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// ✅ Good: Simple convenience base
public abstract class Module : VisoraModule
{
    // Provides no additional functionality, just convenience
}
```

### Static Fields for Constants

Use **static readonly** fields for descriptors and configuration.

```csharp
// ✅ Good: Static readonly descriptor
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.",
        kind: CommandKind.Automation,
        keywords: new[] { "diagnostics", "ping" });

    public override CommandDescriptor Descriptor => Info;
}

// ✅ Good: Static readonly for constants
private static readonly string[] DefaultKeywords = { "core", "system" };

// ❌ Bad: Creating descriptor every time property is accessed
public override CommandDescriptor Descriptor =>
    CommandDescriptor.Create("shell.ping", "Ping");  // Inefficient!
```

### Collection Expressions (C# 12+)

Use modern collection initialization syntax.

```csharp
// ✅ Good: Collection expression
private static readonly string[] Tags = ["core", "shell", "diagnostics"];

// ✅ Good: Array initialization
keywords: new[] { "diagnostics", "ping" }

// ✅ Good: Empty collection
public virtual IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    => Array.Empty<VisoraCommand>();

// ❌ Bad: Old-style initialization
private static readonly string[] Tags = new string[] { "core", "shell" };
```

### Enums for Fixed Sets

Use **enums** for type-safe constants.

```csharp
// ✅ Good: Enum for command kinds
public enum CommandKind
{
    General,
    Navigation,
    Tool,
    Shell,
    Automation
}

// ✅ Good: Enum for component kinds
public enum ComponentKind
{
    Generic,
    Service,
    Ui,
    Console,
    ShellExtension
}

// Usage
kind: CommandKind.Automation

// ❌ Bad: String constants
public const string CommandKindGeneral = "General";
public const string CommandKindShell = "Shell";
```

---

## Async/Await Patterns

### Always Use Async Suffix

All async methods must have `Async` suffix.

```csharp
// ✅ Good: Async suffix on all async methods
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
    await ConnectAsync(cancellationToken).ConfigureAwait(false);
}

public ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    // ...
}

// ❌ Bad: Missing Async suffix
public async Task Initialize() { }
public Task<CommandResult> Execute() { }
```

### ConfigureAwait(false) in Libraries

Use `ConfigureAwait(false)` for all awaits in library code.

```csharp
// ✅ Good: ConfigureAwait(false) in library code
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_modules.Any(m => string.Equals(m.AssemblyPath, path, StringComparison.OrdinalIgnoreCase)))
            continue;

        var handle = await ModuleHandle.LoadAsync(path, options, cancellationToken)
            .ConfigureAwait(false);
        _modules.Add(handle);
    }
}

public async ValueTask DisposeAsync()
{
    foreach (var module in _modules)
    {
        await module.DisposeAsync().ConfigureAwait(false);
    }
    _modules.Clear();
}

// ❌ Bad: Missing ConfigureAwait in library code
var result = await LoadAsync();  // Context capture in library!
```

**Why ConfigureAwait(false)?**
- Avoids unnecessary context capture in library code
- Improves performance (no synchronization context marshaling)
- Prevents deadlocks in synchronous-over-async scenarios

### CancellationToken Pattern

Always accept `CancellationToken` with default parameter.

```csharp
// ✅ Good: CancellationToken with default
public virtual ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Check cancellation before long-running operations
    cancellationToken.ThrowIfCancellationRequested();

    // Pass token to all async calls
    return PerformInitializationAsync(context, cancellationToken);
}

// ✅ Good: Propagate cancellation tokens
public async Task ProcessAsync(CancellationToken cancellationToken = default)
{
    await Step1Async(cancellationToken).ConfigureAwait(false);
    await Step2Async(cancellationToken).ConfigureAwait(false);
}

// ❌ Bad: No cancellation support
public async Task ProcessAsync()
{
    await Step1Async();  // Can't cancel!
}
```

### ValueTask vs Task

Use `ValueTask<T>` for frequently-called methods that may complete synchronously.

```csharp
// ✅ Good: ValueTask for potentially-synchronous operations
public virtual ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
    => ValueTask.CompletedTask;  // Synchronous completion

public virtual ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    var result = CommandResult.Success();
    return ValueTask.FromResult(result);  // Synchronous result
}

// ✅ Good: Task for always-async operations
public async Task<ModuleHandle> LoadAsync(string path, CancellationToken cancellationToken)
{
    // Always performs I/O - use Task
    var assembly = await LoadAssemblyAsync(path, cancellationToken).ConfigureAwait(false);
    return CreateHandle(assembly);
}

// ❌ Bad: Task when ValueTask is more appropriate
public Task InitializeAsync()  // Should be ValueTask
    => Task.CompletedTask;
```

**ValueTask Guidelines**:
- ✅ Use for virtual methods with default no-op implementations
- ✅ Use when method often completes synchronously
- ✅ Use for high-frequency calls (hot path)
- ❌ Don't await multiple times (consume once only)
- ❌ Don't store in fields (consume immediately)

### Async Iterator Pattern

Use `async IAsyncEnumerable<T>` for streaming results.

```csharp
// ✅ Good: Async enumerable for streaming
public async IAsyncEnumerable<ModuleHandle> DiscoverModulesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    foreach (var path in GetCandidatePaths())
    {
        cancellationToken.ThrowIfCancellationRequested();

        var handle = await LoadModuleAsync(path, cancellationToken)
            .ConfigureAwait(false);

        yield return handle;
    }
}

// Usage
await foreach (var module in DiscoverModulesAsync(cancellationToken))
{
    Console.WriteLine(module.Descriptor.Name);
}
```

---

## Error Handling

### When to Use Exceptions

**Use exceptions for**:
- Programming errors (ArgumentNullException, InvalidOperationException)
- Unexpected failures (FileNotFoundException, IOException)
- Framework violations (contract breaches)

**Don't use exceptions for**:
- Expected business logic outcomes
- Flow control
- Validation failures that caller should handle

```csharp
// ✅ Good: Exception for programming error
public ModuleHandle GetModuleRequired(string moduleId)
{
    if (string.IsNullOrWhiteSpace(moduleId))
        throw new ArgumentException("Module ID cannot be null or empty.", nameof(moduleId));

    return _modules.FirstOrDefault(m => m.Descriptor.Id == moduleId)
        ?? throw new ModuleNotFoundException(moduleId);
}

// ✅ Good: Nullable return for expected "not found"
public ModuleHandle? GetModule(string moduleId)
    => _modules.FirstOrDefault(m => string.Equals(
        m.Descriptor.Id, moduleId, StringComparison.OrdinalIgnoreCase));

// ❌ Bad: Exception for expected case
public ModuleHandle GetModule(string moduleId)
{
    var module = _modules.FirstOrDefault(m => m.Descriptor.Id == moduleId);
    if (module == null)
        throw new Exception("Module not found");  // Bad!
    return module;
}
```

### Result Types for Commands

Commands return `CommandResult` for success/failure without exceptions.

```csharp
// ✅ Good: CommandResult pattern
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    try
    {
        var data = ProcessCommand(context);
        return ValueTask.FromResult(CommandResult.Success("Operation completed", data));
    }
    catch (Exception ex)
    {
        // Catch exceptions, return failure result
        return ValueTask.FromResult(CommandResult.Failure(ex.Message));
    }
}

// ✅ Good: Different result factory methods
CommandResult.Success()
CommandResult.Success(message: "Done!")
CommandResult.Success(message: "Done!", payload: data)
CommandResult.Failure(error: "Something went wrong")

// ❌ Bad: Throwing from command execution (unless truly exceptional)
public override ValueTask<CommandResult> ExecuteAsync(...)
{
    if (invalid)
        throw new InvalidOperationException();  // Should return Failure result
}
```

### Argument Validation

Validate arguments at public API boundaries.

```csharp
// ✅ Good: Guard clauses with ArgumentException
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    if (options is null)
        throw new ArgumentNullException(nameof(options));

    // Proceed with logic
}

// ✅ Good: ArgumentNullException.ThrowIfNull (.NET 6+)
public void SetConfiguration(Configuration config)
{
    ArgumentNullException.ThrowIfNull(config);
    _configuration = config;
}

// ✅ Good: ArgumentException for invalid values
public void SetModuleId(string id)
{
    if (string.IsNullOrWhiteSpace(id))
        throw new ArgumentException("Module ID cannot be empty.", nameof(id));

    if (!id.StartsWith("visora.", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Module ID must start with 'visora.'", nameof(id));

    _moduleId = id;
}

// ❌ Bad: No validation
public void SetConfiguration(Configuration config)
{
    _configuration = config;  // What if null?
}
```

### Exception Handling in Disposal

Suppress exceptions in disposal methods.

```csharp
// ✅ Good: Suppress exceptions during disposal
public async ValueTask DisposeAsync()
{
    foreach (var module in _modules)
    {
        try
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't throw during disposal
            Debug.WriteLine($"Error disposing module: {ex.Message}");
        }
    }
    _modules.Clear();
}

// ❌ Bad: Letting exceptions escape disposal
public async ValueTask DisposeAsync()
{
    foreach (var module in _modules)
    {
        await module.DisposeAsync().ConfigureAwait(false);  // May throw!
    }
}
```

---

## Resource Disposal

### IAsyncDisposable Pattern

Implement `IAsyncDisposable` for async cleanup.

```csharp
// ✅ Good: IAsyncDisposable implementation
public abstract class VisoraModule : IAsyncDisposable
{
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var module in _modules)
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }
        _modules.Clear();
    }
}

// ✅ Good: Using await using
await using var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options);
// Automatically disposed
```

### Prefer using Declarations

Use `using` declarations over `using` statements when possible.

```csharp
// ✅ Good: using declaration (C# 8+)
public async Task ProcessFileAsync(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);

    var content = await reader.ReadToEndAsync().ConfigureAwait(false);
    ProcessContent(content);

    // Disposed automatically at end of scope
}

// ✅ Acceptable: using statement when scope control needed
public async Task ProcessMultipleFilesAsync(string[] paths)
{
    foreach (var path in paths)
    {
        using (var stream = File.OpenRead(path))
        {
            await ProcessStreamAsync(stream).ConfigureAwait(false);
        }
        // Disposed after each iteration
    }
}

// ❌ Bad: Manual disposal
public async Task ProcessFileAsync(string path)
{
    var stream = File.OpenRead(path);
    try
    {
        await ProcessStreamAsync(stream).ConfigureAwait(false);
    }
    finally
    {
        stream?.Dispose();  // Use 'using' instead!
    }
}
```

### Await using for IAsyncDisposable

Use `await using` for async disposal.

```csharp
// ✅ Good: await using for IAsyncDisposable
public async Task UseModuleAsync()
{
    await using var catalog = new ModuleCatalog();
    await catalog.DiscoverAsync(options).ConfigureAwait(false);

    // Use catalog

    // Automatically disposed with DisposeAsync
}

// ✅ Good: await using in loop
foreach (var path in modulePaths)
{
    await using var handle = await ModuleHandle.LoadAsync(path).ConfigureAwait(false);
    await handle.InitializeAsync().ConfigureAwait(false);
}

// ❌ Bad: Regular using for IAsyncDisposable
using var catalog = new ModuleCatalog();  // Uses Dispose, not DisposeAsync!
```

---

## Nullable Reference Types

### Enable Nullable Context

All VISORA projects have nullable reference types enabled.

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Nullable Parameter Patterns

Use `?` suffix for nullable parameters and properties.

```csharp
// ✅ Good: Explicit nullable annotations
public sealed record ModuleDescriptor(
    string Id,                                        // Required
    string Name,                                      // Required
    Version Version,                                  // Required
    string? Description = null,                       // Optional
    IReadOnlyDictionary<string, string>? Tags = null, // Optional
    ModuleRuntimeHints? RuntimeHints = null);         // Optional

// ✅ Good: Nullable return type
public ModuleHandle? GetModule(string id)
    => _modules.FirstOrDefault(m => m.Id == id);

// ✅ Good: Non-null return type
public ModuleHandle GetModuleRequired(string id)
    => GetModule(id) ?? throw new ModuleNotFoundException(id);
```

### Null-Coalescing Operators

Use null-coalescing operators for defaults.

```csharp
// ✅ Good: ?? operator for default values
public string GetDescription() => Descriptor.Description ?? "No description";

// ✅ Good: ??= for lazy initialization
private ModuleOptions? _options;
public ModuleOptions Options => _options ??= LoadDefaultOptions();

// ✅ Good: ?. for safe navigation
public string? GetModuleName(string id)
    => GetModule(id)?.Descriptor.Name;

// ❌ Bad: Manual null checks
public string GetDescription()
{
    if (Descriptor.Description == null)
        return "No description";
    return Descriptor.Description;
}
```

### Null Validation

Use appropriate null checks at boundaries.

```csharp
// ✅ Good: ArgumentNullException.ThrowIfNull
public void SetOptions(ModuleOptions options)
{
    ArgumentNullException.ThrowIfNull(options);
    _options = options;
}

// ✅ Good: is null pattern
public void Process(ModuleHandle? handle)
{
    if (handle is null)
        return;

    // handle is not null here
    Console.WriteLine(handle.Descriptor.Name);
}

// ✅ Good: Null-forgiving operator when you know better
public void Initialize()
{
    // _descriptor assigned in constructor, compiler doesn't know
    Console.WriteLine(_descriptor!.Name);
}

// ❌ Bad: Old-style null check
if (handle == null)  // Use 'is null' pattern instead
```

---

## Expression-Bodied Members

### When to Use Expression Bodies

Use expression-bodied members for simple, single-expression implementations.

```csharp
// ✅ Good: Expression-bodied property
public override ModuleDescriptor Descriptor => _descriptor;

// ✅ Good: Expression-bodied method
public string GetDisplayName() => $"{Name} v{Version}";

// ✅ Good: Expression-bodied virtual method with simple default
public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

// ✅ Good: Expression-bodied lambda factory
public static ModuleDescriptor Create(string id, string name)
    => new(id, name, new Version(1, 0, 0));

// ❌ Bad: Expression body for complex logic
public ModuleHandle? FindModule(string id) => _modules.Where(m => m.IsActive).FirstOrDefault(m => string.Equals(m.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase) && m.IsInitialized);
// Too complex! Use block body instead.

// ✅ Good: Block body for complex logic
public ModuleHandle? FindModule(string id)
{
    return _modules
        .Where(m => m.IsActive)
        .FirstOrDefault(m =>
            string.Equals(m.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase)
            && m.IsInitialized);
}
```

### Expression Bodies in Records

Records often use expression bodies for computed properties.

```csharp
// ✅ Good: Expression-bodied members in record
public sealed record ModuleDescriptor(string Id, string Name, Version Version)
{
    public string DisplayName => $"{Name} ({Id})";
    public string VersionString => Version.ToString();
    public bool IsPrerelease => Version.Major == 0;
}
```

---

## Pattern Matching

### Type Patterns

Use pattern matching for type checks and casts.

```csharp
// ✅ Good: Pattern matching with is
if (obj is ModuleDescriptor descriptor)
{
    Console.WriteLine(descriptor.Name);
}

// ✅ Good: Pattern matching in switch expression
public string GetKindName(ComponentKind kind) => kind switch
{
    ComponentKind.Generic => "Generic",
    ComponentKind.Service => "Service",
    ComponentKind.Ui => "UI",
    ComponentKind.Console => "Console",
    ComponentKind.ShellExtension => "Shell Extension",
    _ => "Unknown"
};

// ✅ Good: Property patterns
public bool IsShellCommand(CommandDescriptor cmd) => cmd is
{
    Kind: CommandKind.Shell,
    IsVisible: true
};

// ❌ Bad: Old-style type check and cast
if (obj is ModuleDescriptor)
{
    var descriptor = (ModuleDescriptor)obj;  // Use pattern matching!
    Console.WriteLine(descriptor.Name);
}
```

### Switch Expressions

Prefer switch expressions over switch statements for value returns.

```csharp
// ✅ Good: Switch expression
public string GetIconName(CommandKind kind) => kind switch
{
    CommandKind.General => "icon-general",
    CommandKind.Navigation => "icon-nav",
    CommandKind.Tool => "icon-tool",
    CommandKind.Shell => "icon-shell",
    CommandKind.Automation => "icon-auto",
    _ => "icon-default"
};

// ✅ Good: Switch expression with complex patterns
public int GetPriority(ComponentDescriptor component) => component switch
{
    { Kind: ComponentKind.Service } => 100,
    { Kind: ComponentKind.Console } => 50,
    { Tags: var tags } when tags?.Contains("core") == true => 75,
    _ => 0
};

// ❌ Bad: Switch statement for simple value return
public string GetIconName(CommandKind kind)
{
    switch (kind)
    {
        case CommandKind.General:
            return "icon-general";
        case CommandKind.Navigation:
            return "icon-nav";
        // ... etc
    }
}
```

### Null Patterns

Use `is null` and `is not null` patterns.

```csharp
// ✅ Good: is null pattern
if (descriptor is null)
    throw new ArgumentNullException(nameof(descriptor));

// ✅ Good: is not null pattern
if (module is not null)
    await module.InitializeAsync();

// ✅ Good: Null pattern in switch
public string GetDescription(ModuleDescriptor? descriptor) => descriptor switch
{
    null => "No descriptor",
    { Description: not null } => descriptor.Description,
    _ => "No description provided"
};

// ❌ Bad: == null comparison
if (descriptor == null)  // Use 'is null'
if (module != null)      // Use 'is not null'
```

---

## LINQ Usage

### Prefer Method Syntax

Use method syntax for LINQ queries in most cases.

```csharp
// ✅ Good: Method syntax
public IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
    => context.EnumerateComponentCandidates()
        .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

// ✅ Good: Chained LINQ methods
var activeModules = _modules
    .Where(m => m.IsActive)
    .OrderBy(m => m.Descriptor.Name)
    .Select(m => m.Descriptor);

// ✅ Good: Query syntax for complex joins
var modulesWithCommands =
    from module in _modules
    from component in module.Components
    from command in component.Commands
    select new { module.Descriptor.Name, command.Descriptor.Title };

// ❌ Bad: Query syntax for simple operations
var active = from m in _modules
             where m.IsActive
             select m;
// Use method syntax: _modules.Where(m => m.IsActive)
```

### FirstOrDefault vs Single

Use appropriate LINQ methods for collection operations.

```csharp
// ✅ Good: FirstOrDefault for 0 or 1 expected
public ModuleHandle? GetModule(string id)
    => _modules.FirstOrDefault(m =>
        string.Equals(m.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase));

// ✅ Good: Single for exactly 1 expected (throws if 0 or >1)
public ModuleHandle GetPrimaryModule()
    => _modules.Single(m => m.IsPrimary);

// ✅ Good: SingleOrDefault for 0 or 1 expected (throws if >1)
public ModuleHandle? GetOptionalPrimaryModule()
    => _modules.SingleOrDefault(m => m.IsPrimary);

// ✅ Good: First for at least 1 expected (throws if 0)
public ModuleHandle GetAnyActiveModule()
    => _modules.First(m => m.IsActive);

// ❌ Bad: Single when multiple might exist
var module = _modules.Single(m => m.Tags.Contains("utility"));  // Throws if >1!
```

### Any vs Count

Use `Any()` for existence checks, not `Count()`.

```csharp
// ✅ Good: Any() for existence check
if (_modules.Any())
    Console.WriteLine("Modules loaded");

if (_modules.Any(m => m.IsActive))
    Console.WriteLine("Has active modules");

// ✅ Good: Count() when you need the actual count
var count = _modules.Count(m => m.IsActive);
Console.WriteLine($"Active modules: {count}");

// ❌ Bad: Count() for existence check (less efficient)
if (_modules.Count() > 0)  // Use Any()
if (_modules.Count(m => m.IsActive) > 0)  // Use Any(predicate)
```

---

## Immutability Patterns

### Readonly Fields

Use `readonly` for fields that don't change after initialization.

```csharp
// ✅ Good: Readonly fields
public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();
    private readonly ModuleCatalogOptions _options;

    public ModuleCatalog(ModuleCatalogOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
}

// ❌ Bad: Mutable field that should be readonly
private List<ModuleHandle> _modules = new();  // Should be readonly
```

### Init-Only Properties

Use `init` accessors for immutable but constructible objects.

```csharp
// ✅ Good: Init-only properties
public class ModuleCatalogOptions
{
    public required string[] ProbingPaths { get; init; }
    public string SearchPattern { get; init; } = "*.vixm.dll";
    public bool RecurseSubdirectories { get; init; } = true;
}

// Usage
var options = new ModuleCatalogOptions
{
    ProbingPaths = ["./Modules", "%VISORA_PATH%/Modules"],
    RecurseSubdirectories = true
};

// ❌ Bad: Mutable properties
public class ModuleCatalogOptions
{
    public string[] ProbingPaths { get; set; }  // Can change after init
}
```

### ReadOnly Collections

Expose collections as `IReadOnly*` interfaces.

```csharp
// ✅ Good: IReadOnlyList exposure
public sealed class ModuleCatalog
{
    private readonly List<ModuleHandle> _modules = new();

    public IReadOnlyList<ModuleHandle> Modules => _modules;
}

// ✅ Good: IReadOnlyCollection in parameters
public sealed record ComponentDescriptor(
    string Id,
    string Name,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null);

// ❌ Bad: Exposing mutable collection
public List<ModuleHandle> Modules => _modules;  // Allows external mutation!
```

---

## Formatting and Style

### Indentation and Braces

- **4 spaces** for indentation (no tabs)
- **Allman style** braces (braces on new line)
- **Braces required** for all control structures

```csharp
// ✅ Good: Allman braces, 4-space indent
public async Task ProcessAsync(CancellationToken cancellationToken = default)
{
    if (IsReady)
    {
        await DoWorkAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
        await PrepareAsync(cancellationToken).ConfigureAwait(false);
    }
}

// ❌ Bad: K&R braces
public async Task ProcessAsync() {
    if (IsReady) {
        await DoWorkAsync();
    }
}

// ❌ Bad: Missing braces
if (IsReady)
    await DoWorkAsync();  // Add braces!
```

### Line Length

- **120 characters** maximum line length preferred
- Break long lines at logical points

```csharp
// ✅ Good: Broken at logical points
var descriptor = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Visora Shell Commands",
    version: new Version(0, 1, 0),
    description: "Baseline commands for diagnostics and exploration.");

// ✅ Good: LINQ broken across lines
var active = _modules
    .Where(m => m.IsActive)
    .OrderBy(m => m.Descriptor.Name)
    .Select(m => m.Descriptor);

// ❌ Bad: Excessively long line
var descriptor = ModuleDescriptor.Create(id: "visora.shell.commands.core", name: "Visora Shell Commands", version: new Version(0, 1, 0), description: "Baseline commands");
```

### Access Modifiers

Always specify access modifiers explicitly.

```csharp
// ✅ Good: Explicit access modifiers
public sealed class MyClass
{
    private readonly string _field;

    public string PublicProperty { get; }
    private string PrivateProperty { get; }

    public void PublicMethod() { }
    private void PrivateMethod() { }
}

// ❌ Bad: Implicit internal access
class MyClass  // Should be 'internal class' or 'public class'
{
    string _field;  // Should be 'private'
}
```

### Field Naming

Private fields use `_camelCase` with underscore prefix.

```csharp
// ✅ Good: Underscore-prefixed private fields
private readonly ModuleCatalog _catalog;
private readonly List<ModuleHandle> _modules;
private static readonly CommandDescriptor Info = ...;

// ❌ Bad: Other naming styles
private readonly ModuleCatalog catalog;      // Missing underscore
private readonly ModuleCatalog m_catalog;    // Wrong prefix
private readonly ModuleCatalog _Catalog;     // Wrong casing
```

---

## Comments and Documentation

### XML Documentation

Document all public APIs with XML comments.

```csharp
/// <summary>
/// Base type for Visora modules.
/// </summary>
public abstract class VisoraModule : IAsyncDisposable
{
    /// <summary>
    /// Describes the module to hosts.
    /// </summary>
    public abstract ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Called when the module is being initialized.
    /// </summary>
    /// <param name="context">The module context providing access to host services.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
```

### Code Comments

Use comments sparingly for non-obvious logic.

```csharp
// ✅ Good: Comment explains WHY, not WHAT
// Skip /obj/ directories to avoid loading intermediate build output
private static bool ShouldSkipPath(string path)
{
    var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar);
    return normalized.Contains(
        Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);
}

// ❌ Bad: Comment restates the code
// Check if path is null
if (path is null)  // Code is self-documenting
    throw new ArgumentNullException(nameof(path));
```

### TODO Comments

Use TODO comments for planned improvements.

```csharp
// TODO: Add caching to improve performance
// TODO: Support parallel module loading
// TODO(username): Implement retry logic
```

---

## Anti-Patterns

### 1. Synchronous I/O

```csharp
// ❌ Bad: Blocking I/O
public ModuleHandle LoadModule(string path)
{
    var assembly = Assembly.LoadFrom(path);  // Blocks!
    return CreateHandle(assembly);
}

// ✅ Good: Async I/O
public async Task<ModuleHandle> LoadModuleAsync(
    string path,
    CancellationToken cancellationToken = default)
{
    var assembly = await LoadAssemblyAsync(path, cancellationToken)
        .ConfigureAwait(false);
    return CreateHandle(assembly);
}
```

### 2. Async Void

```csharp
// ❌ Bad: async void (except event handlers)
public async void ProcessModule()  // Exceptions can't be caught!
{
    await LoadAsync();
}

// ✅ Good: async Task
public async Task ProcessModuleAsync()
{
    await LoadAsync();
}
```

### 3. Missing ConfigureAwait

```csharp
// ❌ Bad: Missing ConfigureAwait in library
public async Task LoadAsync()
{
    await File.ReadAllTextAsync(path);  // Captures context!
}

// ✅ Good: ConfigureAwait(false) in library
public async Task LoadAsync()
{
    await File.ReadAllTextAsync(path).ConfigureAwait(false);
}
```

### 4. String Concatenation in Loops

```csharp
// ❌ Bad: String concatenation in loop
string result = "";
foreach (var module in modules)
{
    result += module.Name + ", ";  // Creates new string each iteration
}

// ✅ Good: StringBuilder
var builder = new StringBuilder();
foreach (var module in modules)
{
    builder.Append(module.Name).Append(", ");
}
var result = builder.ToString();

// ✅ Better: string.Join
var result = string.Join(", ", modules.Select(m => m.Name));
```

### 5. Catching General Exception

```csharp
// ❌ Bad: Catching general exception
try
{
    await ProcessAsync();
}
catch (Exception)  // Too broad!
{
    // Swallows everything including OutOfMemoryException
}

// ✅ Good: Catch specific exceptions
try
{
    await ProcessAsync();
}
catch (IOException ex)
{
    LogError(ex);
}
catch (InvalidOperationException ex)
{
    LogError(ex);
}
```

---

## Cross-References

### Related Documentation

- **[Naming Conventions](./naming-conventions.md)**: Identifier naming rules
- **[Project Structure](./project-structure.md)**: Solution organization
- **[Assembly Conventions](./assembly-conventions.md)**: Build and deployment
- **[Async Patterns](../patterns/async-patterns.md)**: Advanced async patterns

### Pattern Documents

- **Module Pattern**: `../patterns/module-pattern.md`
- **Component Pattern**: `../patterns/component-pattern.md`
- **Descriptor Pattern**: `../patterns/descriptor-pattern.md`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-10 | Initial code style guide |

---

**See Also:**
- [VISORA Architecture Overview](../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md)
- [Pattern Quick Reference](../PATTERNS_QUICK_REFERENCE.md)
- [Quick Start Guide](../00-quick-start/README.md)
