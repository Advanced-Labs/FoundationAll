# Immutable Metadata Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
**Related Patterns:** Factory Pattern, Value Objects, Domain-Driven Design

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Immutable Metadata](#why-visora-uses-immutable-metadata)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Value Semantics and Equality](#value-semantics-and-equality)
7. [Serialization Benefits](#serialization-benefits)
8. [Why Sealed Records Over Classes](#why-sealed-records-over-classes)
9. [Testing with Value Equality](#testing-with-value-equality)
10. [Best Practices](#best-practices)
11. [Advanced Topics](#advanced-topics)
12. [Common Pitfalls](#common-pitfalls)

---

## Pattern Overview

### What is Immutable Metadata?

**Definition:** Immutable metadata uses sealed record types to represent unchanging descriptive information about modules, components, and commands. Once created, these metadata objects cannot be modified, ensuring thread-safety, predictable behavior, and enabling value-based equality.

**Key Characteristics:**
- **Immutability:** All properties are `init`-only or read-only
- **Value Semantics:** Equality is based on property values, not reference identity
- **Sealed Records:** Cannot be inherited, preventing polymorphic surprises
- **Structural Equality:** Two records with the same values are considered equal
- **Thread-Safe:** No mutations means no race conditions

### Core Components

```
Descriptor Hierarchy (VISORA)
├─ ModuleDescriptor
│  ├─ Id: string
│  ├─ Name: string
│  ├─ Version: Version
│  ├─ Description: string?
│  ├─ Tags: IReadOnlyDictionary<string, string>?
│  └─ RuntimeHints: ModuleRuntimeHints?
│
├─ ComponentDescriptor
│  ├─ Id: string
│  ├─ Name: string
│  ├─ Description: string?
│  ├─ Tags: IReadOnlyCollection<string>?
│  └─ Kind: ComponentKind (enum)
│
└─ CommandDescriptor
   ├─ Id: string
   ├─ Title: string
   ├─ Description: string?
   ├─ Kind: CommandKind (enum)
   ├─ Aliases: IReadOnlyCollection<string>?
   ├─ Keywords: IReadOnlyCollection<string>?
   ├─ IsVisible: bool
   ├─ IsInstanceScoped: bool
   └─ Ui: CommandUiHint?
```

**Pattern Formula:**
```
sealed record + positional parameters + init-only properties + factory method
= Immutable Metadata
```

---

## Why VISORA Uses Immutable Metadata

### Design Goals

1. **Thread-Safety Without Locking**
   - Metadata is read by multiple threads during discovery and execution
   - No locks needed because values cannot change
   - Safe to cache and share across contexts

2. **Value-Based Equality**
   - Two descriptors with identical values are considered equal
   - Enables reliable comparison and deduplication
   - Simplifies testing and assertions

3. **Serialization-Friendly**
   - Records serialize cleanly to JSON, XML, and binary formats
   - No circular references or complex object graphs
   - Compatible with gRPC, MessagePack, and protobuf

4. **Predictable Behavior**
   - Once created, metadata never changes
   - No temporal coupling or state mutations
   - Easier to reason about and debug

5. **Domain Modeling**
   - Metadata represents facts about modules/components/commands
   - Facts don't change - if they do, create new metadata
   - Aligns with Domain-Driven Design value object pattern

### Key Decision Points

**Q: Why not just use classes with properties?**
**A:** Classes use reference equality by default, requiring manual `Equals()` and `GetHashCode()` implementation. Records provide this for free.

**Q: Why sealed?**
**A:** Inheritance of records can break equality semantics. Sealing prevents derived types from introducing mutability or behavior.

**Q: Why positional parameters?**
**A:** Enables concise construction syntax while maintaining immutability. Factory methods provide named parameter flexibility.

---

## VISORA Implementation

### ModuleDescriptor

**File:** `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` (lines 6-22)

```csharp
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
```

**Design Decisions:**
- **Positional parameters:** `Id`, `Name`, `Version` are required
- **Optional parameters:** `Description`, `Tags`, `RuntimeHints` have defaults
- **Factory method:** `Create()` provides named parameter clarity
- **Read-only collections:** `IReadOnlyDictionary` prevents mutation
- **Nullable references:** Explicit optionality with `?`

**Usage Example:**
```csharp
var descriptor = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Shell Commands Core",
    version: new Version(1, 0, 0),
    description: "Core shell utilities and diagnostics",
    tags: new Dictionary<string, string>
    {
        ["category"] = "shell",
        ["maturity"] = "stable"
    }.AsReadOnly());

// Immutable - this would be a compile error:
// descriptor.Name = "New Name"; // ❌ Error: init-only property
```

### ComponentDescriptor

**File:** `/src/Visora.Contracts/Components/ComponentDescriptor.cs` (lines 9-18)

```csharp
public sealed record ComponentDescriptor(
    string Id,
    string Name,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    ComponentKind Kind = ComponentKind.Generic)
{
    public static ComponentDescriptor Create(
        string id,
        string name,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        ComponentKind kind = ComponentKind.Generic)
        => new(id, name, description, tags, kind);
}
```

**ComponentKind Enum:**
```csharp
public enum ComponentKind
{
    Generic,
    Service,
    Ui,
    Console,
    ShellExtension
}
```

**Design Decisions:**
- Simpler than ModuleDescriptor (no versioning, simpler tags)
- Uses `IReadOnlyCollection` instead of `IReadOnlyDictionary`
- Enum for categorization (type-safe, discoverable)

**Usage Example:**
```csharp
var descriptor = ComponentDescriptor.Create(
    id: "visora.shell.commands.core.utilities",
    name: "Core Utilities",
    description: "Diagnostics and helper commands",
    tags: new[] { "core", "shell", "diagnostics" },
    kind: ComponentKind.Console);

// Value equality works:
var descriptor2 = ComponentDescriptor.Create(
    id: "visora.shell.commands.core.utilities",
    name: "Core Utilities",
    description: "Diagnostics and helper commands",
    tags: new[] { "core", "shell", "diagnostics" },
    kind: ComponentKind.Console);

Assert.AreEqual(descriptor, descriptor2); // ✅ Passes!
```

### CommandDescriptor

**File:** `/src/Visora.Contracts/Commands/CommandDescriptor.cs` (lines 9-31)

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
    CommandUiHint? Ui = null)
{
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
        => new(id, title, description, kind, aliases, keywords,
               isVisible, isInstanceScoped, ui);
}
```

**CommandUiHint (Nested Record):**
```csharp
public sealed record CommandUiHint(
    string? MenuPath = null,
    string? Icon = null,
    string? DefaultGesture = null);
```

**CommandKind Enum:**
```csharp
public enum CommandKind
{
    General,
    Navigation,
    Tool,
    Shell,
    Automation
}
```

**Design Decisions:**
- Most complex descriptor (9 parameters)
- Nested record for UI hints (composition)
- Boolean flags with sensible defaults
- Supports aliases and keywords for discoverability

**Usage Example:**
```csharp
var descriptor = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping",
    description: "Checks connectivity with the Visora host",
    kind: CommandKind.Automation,
    aliases: new[] { "heartbeat", "check" },
    keywords: new[] { "diagnostics", "ping", "health" },
    isVisible: true,
    isInstanceScoped: false,
    ui: new CommandUiHint(
        menuPath: "Tools/Diagnostics/Ping",
        icon: "icon-ping.png",
        defaultGesture: "Ctrl+Shift+P"));
```

---

## Code Examples

### Example 1: Creating Descriptors in Real Modules

**From:** `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

```csharp
public sealed class ShellCommandsModule : Module
{
    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands (Core)",
        version: new Version(0, 1, 0),
        description: "Shell-based commands for core Visora functions and diagnostics.",
        tags: new Dictionary<string, string>
        {
            ["category"] = "shell",
            ["author"] = "Advanced Labs"
        }.AsReadOnly(),
        runtimeHints: null);

    public override ModuleDescriptor Descriptor => Info;
}
```

**Pattern:**
- Static field holds descriptor (created once)
- Property returns static field (no allocations)
- Factory method with named parameters for clarity

**From:** `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs`

```csharp
public sealed class CoreUtilitiesComponent : Component
{
    private static readonly ComponentDescriptor Info =
        ComponentDescriptor.Create(
            id: "visora.shell.commands.core.utilities",
            name: "Core Utilities",
            description: "Diagnostics and helper commands for Visora shell experiments.",
            tags: new[] { "core", "shell", "diagnostics" },
            kind: ComponentKind.Console);

    public override ComponentDescriptor Descriptor => Info;
}
```

**From:** `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs`

```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.",
        kind: CommandKind.Automation,
        aliases: null,
        keywords: new[] { "diagnostics", "ping" },
        isVisible: true,
        isInstanceScoped: false,
        ui: null);

    public override CommandDescriptor Descriptor => Info;
}
```

**Common Pattern Across All Three:**
1. Static field with descriptive name (`Info`)
2. Initialized with factory method
3. Returned via property
4. Immutable and thread-safe

### Example 2: Comparing Descriptors

```csharp
// Value equality demonstration
var desc1 = ComponentDescriptor.Create(
    id: "test.component",
    name: "Test Component");

var desc2 = ComponentDescriptor.Create(
    id: "test.component",
    name: "Test Component");

// Reference equality fails:
Assert.IsFalse(ReferenceEquals(desc1, desc2)); // Different instances

// Value equality succeeds:
Assert.IsTrue(desc1 == desc2); // ✅ Same values
Assert.AreEqual(desc1, desc2); // ✅ Structural equality
Assert.AreEqual(desc1.GetHashCode(), desc2.GetHashCode()); // ✅ Same hash
```

### Example 3: With Pattern Matching

```csharp
public void ProcessDescriptor(ComponentDescriptor descriptor)
{
    // Pattern matching on record properties
    switch (descriptor)
    {
        case { Kind: ComponentKind.Console, Tags: not null }:
            Console.WriteLine($"Console component with tags: {string.Join(", ", descriptor.Tags)}");
            break;

        case { Kind: ComponentKind.Service, Description: var desc } when desc != null:
            Console.WriteLine($"Service: {desc}");
            break;

        case { Id: var id, Name: var name }:
            Console.WriteLine($"{name} ({id})");
            break;
    }
}
```

### Example 4: Deconstruction

```csharp
// Records support deconstruction
var descriptor = ModuleDescriptor.Create(
    id: "test.module",
    name: "Test Module",
    version: new Version(1, 0, 0));

// Positional deconstruction
var (id, name, version, description, tags, hints) = descriptor;

Console.WriteLine($"Module ID: {id}");
Console.WriteLine($"Module Name: {name}");
Console.WriteLine($"Module Version: {version}");
```

### Example 5: With Expressions (Creating Modified Copies)

```csharp
var original = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping",
    kind: CommandKind.Automation);

// Create modified copy with 'with' expression
var withDescription = original with
{
    Description = "Enhanced ping with metrics"
};

// Original is unchanged:
Assert.IsNull(original.Description); // ✅ Still null

// New instance has description:
Assert.AreEqual("Enhanced ping with metrics", withDescription.Description); // ✅

// Other properties are preserved:
Assert.AreEqual(original.Id, withDescription.Id);
Assert.AreEqual(original.Title, withDescription.Title);
```

---

## File References

### Primary Descriptor Files

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` | 6-22 | Module metadata definition |
| `/src/Visora.Contracts/Components/ComponentDescriptor.cs` | 9-18 | Component metadata definition |
| `/src/Visora.Contracts/Commands/CommandDescriptor.cs` | 9-31 | Command metadata definition |
| `/src/Visora.Contracts/Commands/CommandDescriptor.cs` | 36 | CommandUiHint nested record |
| `/src/Visora.Contracts/Modules/ModuleRuntimeHints.cs` | N/A | Runtime deployment hints |

### Usage Examples

| File | Lines | Usage Pattern |
|------|-------|---------------|
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | ~15-25 | ModuleDescriptor creation |
| `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs` | ~10-17 | ComponentDescriptor creation |
| `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` | ~10-17 | CommandDescriptor creation |
| `/src/Visora.Shell.Commands.Core/Commands/EnvironmentInfoCommand.cs` | ~10-17 | CommandDescriptor with keywords |
| `/src/Visora.Shell.Commands.Core/Commands/ModuleProbeCommand.cs` | ~10-17 | CommandDescriptor for diagnostics |

### Inspection and Usage

| File | Lines | How Descriptors Are Used |
|------|-------|--------------------------|
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 49, 72 | Access module descriptor |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 111, 119 | Collect descriptors during inspection |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 160-173 | ModuleInspection, ComponentInspection, CommandInspection records |
| `/src/Visora.CLI/Program.cs` | 200+ | Display descriptors in CLI output |

---

## Value Semantics and Equality

### Reference vs. Value Equality

**Reference Equality (Classes):**
```csharp
public class ModuleDescriptorClass
{
    public string Id { get; init; }
    public string Name { get; init; }
    // ... more properties
}

var desc1 = new ModuleDescriptorClass { Id = "test", Name = "Test" };
var desc2 = new ModuleDescriptorClass { Id = "test", Name = "Test" };

// Reference equality:
Assert.IsFalse(desc1 == desc2); // ❌ Different references
Assert.IsFalse(desc1.Equals(desc2)); // ❌ Default Equals uses reference equality
```

**Value Equality (Records):**
```csharp
public sealed record ModuleDescriptorRecord(string Id, string Name);

var desc1 = new ModuleDescriptorRecord("test", "Test");
var desc2 = new ModuleDescriptorRecord("test", "Test");

// Value equality:
Assert.IsTrue(desc1 == desc2); // ✅ Same values
Assert.IsTrue(desc1.Equals(desc2)); // ✅ Value-based Equals
Assert.AreEqual(desc1.GetHashCode(), desc2.GetHashCode()); // ✅ Consistent hash codes
```

### What Records Generate

Records automatically synthesize:

1. **Value-based `Equals()`:**
```csharp
// Compiler-generated (conceptual):
public override bool Equals(object? obj)
    => obj is ModuleDescriptor other
       && Id == other.Id
       && Name == other.Name
       && Version == other.Version
       && EqualityComparer<string?>.Default.Equals(Description, other.Description)
       && EqualityComparer<IReadOnlyDictionary<string, string>?>.Default.Equals(Tags, other.Tags)
       && Equals(RuntimeHints, other.RuntimeHints);
```

2. **Value-based `GetHashCode()`:**
```csharp
// Compiler-generated (conceptual):
public override int GetHashCode()
{
    var hash = new HashCode();
    hash.Add(Id);
    hash.Add(Name);
    hash.Add(Version);
    hash.Add(Description);
    hash.Add(Tags);
    hash.Add(RuntimeHints);
    return hash.ToHashCode();
}
```

3. **Operators `==` and `!=`:**
```csharp
public static bool operator ==(ModuleDescriptor? left, ModuleDescriptor? right)
    => EqualityComparer<ModuleDescriptor>.Default.Equals(left, right);

public static bool operator !=(ModuleDescriptor? left, ModuleDescriptor? right)
    => !(left == right);
```

4. **`ToString()`:**
```csharp
public override string ToString()
    => $"ModuleDescriptor {{ Id = {Id}, Name = {Name}, Version = {Version}, ... }}";
```

### Benefits for VISORA

**1. Reliable Comparison:**
```csharp
// Find duplicate modules by descriptor:
var uniqueModules = catalog.Modules
    .DistinctBy(m => m.Descriptor)
    .ToList();
```

**2. Dictionary/Set Keys:**
```csharp
// Use descriptors as dictionary keys:
var commandsByDescriptor = new Dictionary<CommandDescriptor, VisoraCommand>();

var descriptor = CommandDescriptor.Create("shell.ping", "Ping");
commandsByDescriptor[descriptor] = new PingCommand();

// Later, same descriptor retrieves command:
var sameDescriptor = CommandDescriptor.Create("shell.ping", "Ping");
var command = commandsByDescriptor[sameDescriptor]; // ✅ Works!
```

**3. Testing Assertions:**
```csharp
[TestMethod]
public void Module_Descriptor_MatchesExpected()
{
    var module = new ShellCommandsModule();

    var expected = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands (Core)",
        version: new Version(0, 1, 0));

    Assert.AreEqual(expected, module.Descriptor); // ✅ Value equality
}
```

---

## Serialization Benefits

### JSON Serialization

**System.Text.Json:**
```csharp
using System.Text.Json;

var descriptor = ModuleDescriptor.Create(
    id: "test.module",
    name: "Test Module",
    version: new Version(1, 2, 3),
    description: "A test module");

// Serialize to JSON
string json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions
{
    WriteIndented = true
});

Console.WriteLine(json);
// Output:
// {
//   "Id": "test.module",
//   "Name": "Test Module",
//   "Version": "1.2.3",
//   "Description": "A test module",
//   "Tags": null,
//   "RuntimeHints": null
// }

// Deserialize from JSON
var deserialized = JsonSerializer.Deserialize<ModuleDescriptor>(json);

Assert.AreEqual(descriptor, deserialized); // ✅ Value equality works!
```

**Key Benefits:**
- Records serialize cleanly (no circular references)
- Read-only collections work with serializers
- Nullable reference types map to JSON nulls
- Value equality ensures round-trip consistency

### Newtonsoft.Json (Json.NET):
```csharp
using Newtonsoft.Json;

var descriptor = ComponentDescriptor.Create(
    id: "test.component",
    name: "Test Component",
    kind: ComponentKind.Console);

string json = JsonConvert.SerializeObject(descriptor, Formatting.Indented);

var deserialized = JsonConvert.DeserializeObject<ComponentDescriptor>(json);

Assert.AreEqual(descriptor, deserialized); // ✅ Works!
```

### MessagePack (Binary Serialization):
```csharp
using MessagePack;

[MessagePackObject]
public sealed record CommandDescriptor(
    [property: Key(0)] string Id,
    [property: Key(1)] string Title,
    [property: Key(2)] string? Description = null,
    [property: Key(3)] CommandKind Kind = CommandKind.General);

var descriptor = CommandDescriptor.Create("shell.ping", "Ping");

byte[] bytes = MessagePackSerializer.Serialize(descriptor);
var deserialized = MessagePackSerializer.Deserialize<CommandDescriptor>(bytes);

Assert.AreEqual(descriptor, deserialized); // ✅ Works!
```

### gRPC / Protobuf Compatibility

Records can be used as DTOs in gRPC services:

```csharp
// .proto file (conceptual mapping):
message ModuleDescriptor {
    string id = 1;
    string name = 2;
    string version = 3;
    optional string description = 4;
    map<string, string> tags = 5;
}
```

**Benefits:**
- Immutable = thread-safe during serialization
- Value equality = reliable comparison of deserialized objects
- No hidden state = what you see is what gets serialized

---

## Why Sealed Records Over Classes

### Comparison Table

| Aspect | Class | Record | Sealed Record (VISORA) |
|--------|-------|--------|------------------------|
| **Equality** | Reference | Value | Value |
| **Inheritance** | Allowed | Allowed | Prevented (sealed) |
| **Mutability** | Mutable by default | Immutable by default | Immutable by default |
| **ToString()** | Type name only | All properties | All properties |
| **Deconstruction** | Manual | Automatic | Automatic |
| **With expressions** | Not supported | Supported | Supported |
| **GetHashCode()** | Reference-based | Value-based | Value-based |

### Why Not Classes?

**Problem with Classes:**
```csharp
public class ModuleDescriptorClass
{
    public string Id { get; init; }
    public string Name { get; init; }
    public Version Version { get; init; }

    // Must manually implement:
    public override bool Equals(object? obj)
    {
        if (obj is not ModuleDescriptorClass other) return false;
        return Id == other.Id && Name == other.Name && Version == other.Version;
    }

    public override int GetHashCode()
        => HashCode.Combine(Id, Name, Version);

    public override string ToString()
        => $"ModuleDescriptor {{ Id = {Id}, Name = {Name}, Version = {Version} }}";

    // Static factory
    public static ModuleDescriptorClass Create(...) => new() { ... };
}
```

**Boilerplate:** ~30+ lines for equality, hashing, and ToString.

**Record Equivalent:**
```csharp
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version)
{
    public static ModuleDescriptor Create(string id, string name, Version version)
        => new(id, name, version);
}
```

**Same Functionality:** 8 lines, compiler generates the rest.

### Why Sealed?

**Problem: Inheritance Breaks Equality**

```csharp
public record ComponentDescriptor(string Id, string Name);
public record ExtendedComponentDescriptor(string Id, string Name, string Extra)
    : ComponentDescriptor(Id, Name);

var base1 = new ComponentDescriptor("test", "Test");
var derived = new ExtendedComponentDescriptor("test", "Test", "Extra");

// Equality is broken:
Assert.IsFalse(base1 == derived); // ❌ Different types
// But:
Assert.IsTrue(base1.Id == derived.Id && base1.Name == derived.Name); // ✅ Same values

// Dictionary behavior is unpredictable:
var dict = new Dictionary<ComponentDescriptor, string>();
dict[base1] = "Base";
dict[derived] = "Derived"; // Different key, even with same Id/Name!
```

**Solution: Seal Records**
```csharp
public sealed record ComponentDescriptor(string Id, string Name);

// This is now a compile error:
// public record ExtendedComponentDescriptor(...) : ComponentDescriptor(...); // ❌ Error
```

**Benefits:**
- Equality semantics are predictable
- No runtime type checks needed
- HashCode is stable
- Descriptors are pure data, not polymorphic types

### When to Use Each

| Use Case | Choice | Rationale |
|----------|--------|-----------|
| **Metadata/descriptors** | Sealed record | Immutable, value equality, no inheritance |
| **Entities with identity** | Class | Reference equality, lifecycle management |
| **Value objects** | Sealed record | Equality by value, no identity |
| **Polymorphic hierarchies** | Abstract class or interface | Behavior varies by type |
| **DTOs** | Record or sealed record | Serialization, value equality |
| **Configuration** | Class (may be mutable) | Updated at runtime |

---

## Testing with Value Equality

### Example 1: Assert Descriptor Values

```csharp
[TestClass]
public class ModuleDescriptorTests
{
    [TestMethod]
    public void Create_WithAllParameters_ReturnsExpectedDescriptor()
    {
        // Arrange
        var tags = new Dictionary<string, string> { ["key"] = "value" };

        // Act
        var descriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0),
            description: "Test description",
            tags: tags.AsReadOnly());

        // Assert - value equality works perfectly
        var expected = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0),
            description: "Test description",
            tags: tags.AsReadOnly());

        Assert.AreEqual(expected, descriptor); // ✅ Value equality
    }
}
```

### Example 2: Collection Assertions

```csharp
[TestMethod]
public async Task InspectAsync_ReturnsExpectedDescriptors()
{
    // Arrange
    var handle = await ModuleHandle.LoadAsync("test.vixm.dll", options, ct);

    // Act
    var inspection = await handle.InspectAsync(ct);

    // Assert - find descriptor by value
    var expectedDescriptor = ComponentDescriptor.Create(
        id: "test.component",
        name: "Test Component");

    var found = inspection.Components
        .Select(c => c.Descriptor)
        .Contains(expectedDescriptor); // ✅ Value equality in collection

    Assert.IsTrue(found);
}
```

### Example 3: Mocking with Value Equality

```csharp
[TestMethod]
public void ProcessCommand_WithDescriptor_ExecutesCorrectCommand()
{
    // Arrange
    var descriptor = CommandDescriptor.Create("shell.ping", "Ping");

    var mockCommand = new Mock<VisoraCommand>();
    mockCommand.Setup(c => c.Descriptor).Returns(descriptor);

    var registry = new Dictionary<CommandDescriptor, VisoraCommand>
    {
        [descriptor] = mockCommand.Object
    };

    // Act - lookup by value-equal descriptor
    var lookupDescriptor = CommandDescriptor.Create("shell.ping", "Ping");
    var command = registry[lookupDescriptor]; // ✅ Finds by value equality

    // Assert
    Assert.AreSame(mockCommand.Object, command);
}
```

### Example 4: Property-Based Testing

```csharp
[TestMethod]
public void Descriptor_Equality_IsReflexiveSymmetricTransitive()
{
    var desc1 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0));
    var desc2 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0));
    var desc3 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0));

    // Reflexive: x == x
    Assert.IsTrue(desc1 == desc1);

    // Symmetric: x == y implies y == x
    Assert.IsTrue(desc1 == desc2);
    Assert.IsTrue(desc2 == desc1);

    // Transitive: x == y and y == z implies x == z
    Assert.IsTrue(desc1 == desc2);
    Assert.IsTrue(desc2 == desc3);
    Assert.IsTrue(desc1 == desc3);
}
```

---

## Best Practices

### 1. Always Use Factory Methods

**✅ Good:**
```csharp
var descriptor = ModuleDescriptor.Create(
    id: "test.module",
    name: "Test",
    version: new Version(1, 0, 0));
```

**❌ Avoid:**
```csharp
var descriptor = new ModuleDescriptor(
    "test.module",  // Positional - unclear what this is
    "Test",
    new Version(1, 0, 0));
```

**Rationale:** Named parameters are self-documenting.

### 2. Use Static Fields for Constant Descriptors

**✅ Good:**
```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping");

    public override CommandDescriptor Descriptor => Info;
}
```

**❌ Avoid:**
```csharp
public sealed class PingCommand : VisoraCommand
{
    // Recreates descriptor on every access!
    public override CommandDescriptor Descriptor => CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping");
}
```

**Rationale:** Allocates once, not per-access. Immutable means safe to share.

### 3. Prefer Read-Only Collections

**✅ Good:**
```csharp
var tags = new Dictionary<string, string> { ["key"] = "value" };
var descriptor = ModuleDescriptor.Create(
    id: "test",
    name: "Test",
    version: new Version(1, 0, 0),
    tags: tags.AsReadOnly()); // Immutable view
```

**❌ Avoid:**
```csharp
var tags = new Dictionary<string, string> { ["key"] = "value" };
var descriptor = new ModuleDescriptor(
    "test",
    "Test",
    new Version(1, 0, 0),
    Tags: tags); // Mutable dictionary!

// External code can mutate:
tags["key2"] = "value2"; // Descriptor sees changes!
```

### 4. Use Nullable Reference Types Correctly

**✅ Good:**
```csharp
public sealed record ModuleDescriptor(
    string Id,                                 // Required
    string Name,                               // Required
    Version Version,                           // Required
    string? Description = null,                // Optional
    IReadOnlyDictionary<string, string>? Tags = null); // Optional
```

**❌ Avoid:**
```csharp
public sealed record ModuleDescriptor(
    string? Id,  // ❌ Id should always be present
    string? Name, // ❌ Name should always be present
    ...);
```

**Rationale:** Nullable types document optionality and enable compiler checks.

### 5. Seal All Descriptor Records

**✅ Good:**
```csharp
public sealed record ComponentDescriptor(...);
```

**❌ Avoid:**
```csharp
public record ComponentDescriptor(...); // Not sealed - allows inheritance
```

**Rationale:** Prevents equality issues from derived types.

### 6. Test Value Equality Explicitly

```csharp
[TestMethod]
public void Descriptors_WithSameValues_AreEqual()
{
    var desc1 = ComponentDescriptor.Create("id", "Name");
    var desc2 = ComponentDescriptor.Create("id", "Name");

    Assert.AreEqual(desc1, desc2); // Value equality
    Assert.AreEqual(desc1.GetHashCode(), desc2.GetHashCode()); // Hash consistency
}
```

---

## Advanced Topics

### Topic 1: Record Structs vs. Record Classes

VISORA uses record classes (reference types), but record structs are also available:

```csharp
// Record class (VISORA approach):
public sealed record ComponentDescriptor(string Id, string Name);

// Record struct (alternative for small value types):
public readonly record struct CommandId(string Value);
```

**When to Use Record Structs:**
- Very small (< 16 bytes)
- Short-lived (avoids heap allocation)
- Passed by value semantics are desired

**VISORA Uses Record Classes Because:**
- Descriptors contain collections (always heap-allocated)
- Passed by reference reduces copying
- Nullable reference types work naturally

### Topic 2: With Expressions and Mutation

Records support non-destructive mutation via `with`:

```csharp
var original = ModuleDescriptor.Create(
    id: "test.module",
    name: "Test",
    version: new Version(1, 0, 0));

// Create new version with updated name:
var updated = original with { Name = "Updated Test" };

Assert.AreEqual("Test", original.Name); // Original unchanged
Assert.AreEqual("Updated Test", updated.Name);
Assert.AreEqual(original.Id, updated.Id); // Other properties copied
```

**Use Cases:**
- Testing with variations
- Versioning metadata
- Template-based creation

### Topic 3: Deep Equality with Collections

Records perform shallow equality on collections:

```csharp
var tags1 = new[] { "tag1", "tag2" };
var tags2 = new[] { "tag1", "tag2" };

var desc1 = ComponentDescriptor.Create("id", "Name", tags: tags1);
var desc2 = ComponentDescriptor.Create("id", "Name", tags: tags2);

// This might fail! Arrays use reference equality:
Assert.AreNotEqual(desc1, desc2); // ❌ tags1 != tags2 by reference
```

**Solution: Use IReadOnlyCollection from same source or implement custom comparer:**

```csharp
var tags = new[] { "tag1", "tag2" };

var desc1 = ComponentDescriptor.Create("id", "Name", tags: tags);
var desc2 = ComponentDescriptor.Create("id", "Name", tags: tags);

Assert.AreEqual(desc1, desc2); // ✅ Same array reference
```

**Best Practice for Testing:**
```csharp
// Use SequenceEqual for collection comparison:
Assert.IsTrue(desc1.Tags.SequenceEqual(desc2.Tags));
```

### Topic 4: Serialization Edge Cases

**Handling Version Serialization:**

`System.Version` serializes as a string in JSON:
```json
{
  "Version": "1.2.3"
}
```

Ensure your serializer supports this:
```csharp
var options = new JsonSerializerOptions
{
    Converters = { new VersionConverter() } // Custom converter if needed
};
```

**Handling Read-Only Collections:**

Some serializers struggle with read-only collections:
```csharp
// May need to map to List<T> for serialization:
[JsonConverter(typeof(ReadOnlyCollectionConverter))]
public IReadOnlyCollection<string>? Tags { get; init; }
```

---

## Common Pitfalls

### Pitfall 1: Mutating "Immutable" Collections

```csharp
// ❌ BAD: Passing mutable collection
var tags = new List<string> { "tag1", "tag2" };
var descriptor = ComponentDescriptor.Create("id", "Name", tags: tags);

// External mutation visible through descriptor!
tags.Add("tag3");
Console.WriteLine(descriptor.Tags.Count); // 3 - mutated!
```

**Fix:**
```csharp
// ✅ GOOD: Use AsReadOnly() or ToArray()
var tags = new List<string> { "tag1", "tag2" };
var descriptor = ComponentDescriptor.Create("id", "Name", tags: tags.AsReadOnly());

tags.Add("tag3");
Console.WriteLine(descriptor.Tags.Count); // 2 - unchanged!
```

### Pitfall 2: Assuming Reference Equality

```csharp
var desc1 = ComponentDescriptor.Create("id", "Name");
var desc2 = ComponentDescriptor.Create("id", "Name");

// ❌ Wrong assumption:
if (ReferenceEquals(desc1, desc2)) // Always false
{
    // Never executes
}

// ✅ Correct:
if (desc1 == desc2) // Value equality
{
    // Executes because values match
}
```

### Pitfall 3: Forgetting to Seal

```csharp
// ❌ Not sealed
public record ComponentDescriptor(string Id, string Name);

// Someone derives:
public record ExtendedDescriptor(string Id, string Name, string Extra)
    : ComponentDescriptor(Id, Name);

// Equality breaks:
ComponentDescriptor base1 = new ComponentDescriptor("id", "Name");
ComponentDescriptor derived = new ExtendedDescriptor("id", "Name", "extra");
Assert.IsFalse(base1 == derived); // ❌ Unexpected inequality
```

**Fix:**
```csharp
// ✅ Sealed prevents inheritance
public sealed record ComponentDescriptor(string Id, string Name);
```

### Pitfall 4: Ignoring Null in Equality

```csharp
var desc1 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0), description: null);
var desc2 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0), description: null);

Assert.AreEqual(desc1, desc2); // ✅ Works - null == null

// But:
var desc3 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0)); // Default null
var desc4 = ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0), description: "");

Assert.AreNotEqual(desc3, desc4); // ✅ null != ""
```

**Lesson:** Be explicit about null vs. empty string.

---

## Summary

### Key Takeaways

1. **Immutable Metadata = Sealed Records**
   - Thread-safe without locking
   - Value-based equality
   - Compiler-generated boilerplate

2. **Factory Methods for Clarity**
   - Named parameters document intent
   - Static fields avoid allocations

3. **Serialization-Friendly**
   - JSON, XML, binary all work
   - No circular references
   - Round-trip with value equality

4. **Seal to Prevent Inheritance**
   - Equality semantics stay predictable
   - No polymorphic surprises

5. **Read-Only Collections**
   - Use `IReadOnlyCollection<T>` and `IReadOnlyDictionary<K, V>`
   - Call `.AsReadOnly()` when passing mutable collections

6. **Value Equality Enables:**
   - Dictionary/Set keys
   - Collection comparisons
   - Reliable test assertions

### Anti-Patterns to Avoid

- ❌ Mutable descriptors
- ❌ Positional construction without factory
- ❌ Unsealed records
- ❌ Mutable collections in descriptors
- ❌ Assuming reference equality

### Related Patterns

- **Factory Pattern:** Descriptor.Create() methods
- **Value Object (DDD):** Descriptors are value objects
- **Data Transfer Object:** Descriptors as DTOs
- **Builder Pattern:** Can create descriptors fluently

---

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
