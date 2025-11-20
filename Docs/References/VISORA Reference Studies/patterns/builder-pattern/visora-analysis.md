# Builder Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
**Related Patterns:** Factory Pattern, Immutable Objects, Fluent Interface

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Builder Pattern](#why-visora-uses-builder-pattern)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Fluent Interface Design](#fluent-interface-design)
7. [Validation and Error Handling](#validation-and-error-handling)
8. [Immutable Product Pattern](#immutable-product-pattern)
9. [Testing Builders](#testing-builders)
10. [Best Practices](#best-practices)
11. [Advanced Topics](#advanced-topics)
12. [Common Pitfalls](#common-pitfalls)

---

## Pattern Overview

### What is the Builder Pattern?

**Definition:** The Builder pattern separates the construction of a complex object from its representation, allowing the same construction process to create different representations. It uses a fluent interface to configure the object step-by-step.

**Key Characteristics:**
- **Fluent Interface:** Method chaining for readability
- **Step-by-Step Construction:** Configure object incrementally
- **Immutable Product:** Build() returns immutable object
- **Validation:** Validate on Build(), not per-step
- **Separation of Concerns:** Builder logic separate from product

### Core Components

```
Builder Pattern Structure
├─ Builder Class
│  ├─ Add/Set methods (fluent, return this)
│  ├─ Internal state (mutable during building)
│  └─ Build() method (returns immutable product)
│
└─ Product Class
   └─ Immutable once built
```

**Pattern Formula:**
```
var product = new Builder()
    .Add(item1)
    .Add(item2)
    .Set(property)
    .Build();  // Returns immutable product
```

---

## Why VISORA Uses Builder Pattern

### Design Goals

1. **Complex Configuration**
   - CapabilityProvider can have many capabilities
   - Adding them one-by-one is clearer than constructor with 10+ parameters
   - Builder provides a clean API

2. **Fluent Readability**
   ```csharp
   var capabilities = CapabilityProviders.CreateBuilder()
       .Add<IConsoleHost>(consoleHost)
       .Add<IUIManager>(uiManager)
       .Add<ILogger>(logger)
       .Build();
   ```
   vs.
   ```csharp
   var capabilities = new CapabilityProvider(
       consoleHost,
       uiManager,
       logger);
   ```

3. **Optional Configuration**
   - Not all capabilities are required
   - Builder allows selective addition
   - Empty builder produces valid (empty) provider

4. **Immutable Product**
   - Builder is mutable during construction
   - Build() returns immutable ICapabilityProvider
   - Safe to share across threads

5. **Validation at Build Time**
   - Builder can validate completeness when Build() is called
   - Early configuration errors caught before use

### Key Decision Points

**Q: Why not just use object initializers?**
**A:**
```csharp
// Object initializer (limited flexibility):
var provider = new CapabilityProvider
{
    Capabilities = new Dictionary<Type, object>
    {
        [typeof(IConsoleHost)] = consoleHost,
        [typeof(IUIManager)] = uiManager
    }
};

// Builder (type-safe, fluent):
var provider = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(consoleHost)
    .Add<IUIManager>(uiManager)
    .Build();
```

Builder provides type safety (`Add<T>`) and clearer intent.

**Q: Why separate Builder from Product?**
**A:**
- Builder is mutable (adding capabilities)
- Product is immutable (ICapabilityProvider)
- Clear separation of construction vs. usage

---

## VISORA Implementation

### CapabilityProviderBuilder

**File:** `/src/Visora.Core/Capabilities/CapabilityProviders.cs` (lines 17-69)

```csharp
public static class CapabilityProviders
{
    /// <summary>
    /// An empty provider that never resolves capabilities.
    /// </summary>
    public static ICapabilityProvider Empty { get; } = new NullCapabilityProvider();

    public static CapabilityProviderBuilder CreateBuilder() => new();

    private sealed class NullCapabilityProvider : ICapabilityProvider
    {
        public bool TryGet<TCapability>(out TCapability? capability)
            where TCapability : class
        {
            capability = null;
            return false;
        }
    }
}

/// <summary>
/// Builds an <see cref="ICapabilityProvider"/> backed by a dictionary.
/// </summary>
public sealed class CapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    /// <summary>
    /// Add a capability to the provider.
    /// </summary>
    /// <typeparam name="TCapability">The capability type (interface or class)</typeparam>
    /// <param name="capability">The capability instance</param>
    /// <returns>This builder for fluent chaining</returns>
    public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
        where TCapability : class
    {
        if (capability is null) throw new ArgumentNullException(nameof(capability));
        _registrations[typeof(TCapability)] = capability;
        return this;
    }

    /// <summary>
    /// Build the immutable capability provider.
    /// </summary>
    /// <returns>An immutable ICapabilityProvider</returns>
    public ICapabilityProvider Build()
    {
        if (_registrations.Count == 0)
            return CapabilityProviders.Empty;

        return new DictionaryCapabilityProvider(
            new Dictionary<Type, object>(_registrations));
    }

    private sealed class DictionaryCapabilityProvider : ICapabilityProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _registrations;

        public DictionaryCapabilityProvider(IReadOnlyDictionary<Type, object> registrations)
            => _registrations = registrations;

        public bool TryGet<TCapability>(out TCapability? capability)
            where TCapability : class
        {
            if (_registrations.TryGetValue(typeof(TCapability), out var value))
            {
                capability = (TCapability)value;
                return true;
            }

            capability = null;
            return false;
        }
    }
}
```

**Design Decisions:**

1. **Fluent Interface:**
   - `Add<T>()` returns `this` (the builder)
   - Enables method chaining
   - Reads like a sentence

2. **Type-Safe Addition:**
   - Generic `Add<TCapability>()` method
   - Compile-time type checking
   - No casting needed in client code

3. **Null Validation:**
   - `Add()` throws `ArgumentNullException` if capability is null
   - Prevents invalid state early

4. **Immutable Product:**
   - `Build()` creates a new `DictionaryCapabilityProvider`
   - Constructor takes `IReadOnlyDictionary` (immutable view)
   - Original builder can continue to be used (not consumed)

5. **Empty Provider Optimization:**
   - If no capabilities added, returns singleton `Empty`
   - Avoids allocating empty dictionaries

6. **Private Implementation:**
   - `DictionaryCapabilityProvider` is private nested class
   - Clients only see `ICapabilityProvider` interface
   - Implementation details hidden

### Usage Pattern

```csharp
// In host setup (e.g., Visora.CLI, Visora.Terminal):
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(consoleHost)
    .Add<IUIManager>(uiManager)
    .Add<ILogger>(logger)
    .Add<IFileSystem>(fileSystem)
    .Build();

// Pass to ModuleCatalogOptions:
var options = new ModuleCatalogOptions
{
    Capabilities = capabilities
};

// Modules receive capabilities in context:
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.LogInformation("Module initializing...");
}
```

---

## Code Examples

### Example 1: Basic Builder Usage

```csharp
// Create builder
var builder = CapabilityProviders.CreateBuilder();

// Add capabilities one by one
builder.Add<IConsoleHost>(new ConsoleHost());
builder.Add<ILogger>(new ConsoleLogger());

// Build immutable provider
var capabilities = builder.Build();

// Use provider
if (capabilities.TryGet<ILogger>(out var logger))
{
    logger.LogInformation("Capabilities ready");
}
```

### Example 2: Fluent Chaining

```csharp
// All in one statement
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(new ConsoleHost())
    .Add<ILogger>(new ConsoleLogger())
    .Add<IUIManager>(new UIManager())
    .Build();
```

### Example 3: Conditional Building

```csharp
var builder = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(new ConsoleLogger());

// Add optional capabilities
if (enableUI)
{
    builder.Add<IUIManager>(new UIManager());
}

if (enableFileAccess)
{
    builder.Add<IFileSystem>(new FileSystem());
}

var capabilities = builder.Build();
```

### Example 4: Reusing Builder

```csharp
var builder = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(new ConsoleLogger());

// Build first provider
var capabilities1 = builder.Build();

// Continue building (builder is not consumed)
builder.Add<IUIManager>(new UIManager());

// Build second provider (has logger + UI)
var capabilities2 = builder.Build();

// capabilities1 still only has logger
// capabilities2 has logger + UI
```

### Example 5: Builder for Testing

```csharp
[TestClass]
public class ModuleTests
{
    [TestMethod]
    public async Task Initialize_WithLogger_LogsMessage()
    {
        // Arrange - Create mock logger
        var mockLogger = new Mock<ILogger>();

        // Build capabilities with mock
        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(mockLogger.Object)
            .Build();

        var context = new ModuleContext(
            descriptor,
            services: null,
            capabilities,
            properties: null);

        var module = new TestModule();

        // Act
        await module.InitializeAsync(context, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.LogInformation(It.IsAny<string>()),
            Times.Once);
    }
}
```

### Example 6: Empty Builder

```csharp
// Create empty provider (no capabilities)
var emptyCapabilities = CapabilityProviders.CreateBuilder().Build();

// Or use the singleton directly:
var emptyCapabilities2 = CapabilityProviders.Empty;

// Both are valid, Empty is more efficient
Assert.AreSame(emptyCapabilities, emptyCapabilities2); // ✅ Same instance
```

---

## File References

### Primary Implementation

| File | Lines | Component |
|------|-------|-----------|
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 10-17 | `CapabilityProviders` static class |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 32-69 | `CapabilityProviderBuilder` class |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 51-69 | `DictionaryCapabilityProvider` (product) |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 19-27 | `NullCapabilityProvider` (empty) |

### Interface

| File | Lines | Component |
|------|-------|-----------|
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | ~5-10 | `ICapabilityProvider` interface |
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | ~12-30 | Extension methods (GetOptional, GetRequired) |

### Usage Examples

| File | Lines | Usage Pattern |
|------|-------|---------------|
| `/src/Visora.CLI/Program.cs` | ~400+ | Creating capabilities for CLI host |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 74 | Using capabilities from options |
| `/src/Visora.Core/Modules/ModuleCatalogOptions.cs` | ~25 | Default empty capabilities |

---

## Fluent Interface Design

### What is a Fluent Interface?

**Definition:** A fluent interface is an object-oriented API that relies extensively on method chaining to create DSL-like readable code.

**Characteristics:**
- Methods return `this` (or the builder instance)
- Enables chaining: `builder.A().B().C()`
- Reads like natural language

### VISORA's Fluent Design

```csharp
public sealed class CapabilityProviderBuilder
{
    // Fluent method: returns this
    public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
        where TCapability : class
    {
        if (capability is null) throw new ArgumentNullException(nameof(capability));
        _registrations[typeof(TCapability)] = capability;
        return this; // ← Key to fluency
    }

    // Terminal method: returns product
    public ICapabilityProvider Build()
    {
        // Build and return immutable product
        return new DictionaryCapabilityProvider(...);
    }
}
```

**Usage:**
```csharp
var capabilities = CapabilityProviders.CreateBuilder()  // Returns builder
    .Add<ILogger>(logger)        // Returns builder
    .Add<IUIManager>(uiManager)  // Returns builder
    .Build();                     // Returns ICapabilityProvider
```

### Benefits of Fluent Design

1. **Readability:**
   ```csharp
   // Fluent:
   var capabilities = builder
       .Add<ILogger>(logger)
       .Add<IUIManager>(uiManager)
       .Build();

   // Non-fluent:
   var builder = new CapabilityProviderBuilder();
   builder.Add<ILogger>(logger);
   builder.Add<IUIManager>(uiManager);
   var capabilities = builder.Build();
   ```

2. **Discoverability:**
   - IntelliSense shows available methods after each call
   - Natural progression from Add() to Add() to Build()

3. **Compact Syntax:**
   - Fewer lines of code
   - Less variable clutter

4. **Immutability of Result:**
   - Builder is mutable (during construction)
   - Product is immutable (after Build())

### Fluent Method Patterns

**Pattern 1: Configuration Method**
```csharp
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    // Configure internal state
    _registrations[typeof(T)] = capability;
    return this; // Return builder for chaining
}
```

**Pattern 2: Terminal Method**
```csharp
public ICapabilityProvider Build()
{
    // Create and return immutable product
    return new DictionaryCapabilityProvider(_registrations);
}
```

**Pattern 3: Validation Method**
```csharp
public CapabilityProviderBuilder Validate()
{
    // Validate current state
    if (_registrations.Count == 0)
        throw new InvalidOperationException("No capabilities added");

    return this; // Return builder for chaining
}
```

---

## Validation and Error Handling

### When to Validate

**VISORA's Approach: Validate Early (per-Add)**

```csharp
public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
    where TCapability : class
{
    if (capability is null)
        throw new ArgumentNullException(nameof(capability));

    _registrations[typeof(TCapability)] = capability;
    return this;
}
```

**Benefits:**
- Fail fast (error on Add, not on Build)
- Clear error messages (knows which capability is null)

**Alternative: Validate on Build**

```csharp
public ICapabilityProvider Build()
{
    // Validate all registrations
    foreach (var (type, instance) in _registrations)
    {
        if (instance is null)
            throw new InvalidOperationException($"Capability {type} is null");
    }

    return new DictionaryCapabilityProvider(_registrations);
}
```

**Benefits:**
- Defer validation until needed
- Can accumulate multiple errors

**VISORA's Choice:** Validate early (per-Add) for immediate feedback.

### Error Handling Patterns

**Pattern 1: ArgumentNullException for Null Inputs**

```csharp
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    if (capability is null)
        throw new ArgumentNullException(nameof(capability));

    // ...
}
```

**Pattern 2: InvalidOperationException for State Violations**

```csharp
public ICapabilityProvider Build()
{
    if (_alreadyBuilt)
        throw new InvalidOperationException("Builder has already been built");

    _alreadyBuilt = true;
    return new DictionaryCapabilityProvider(_registrations);
}
```

**Pattern 3: ArgumentException for Invalid Values**

```csharp
public CapabilityProviderBuilder Add<T>(T capability, string? name = null)
    where T : class
{
    if (capability is null)
        throw new ArgumentNullException(nameof(capability));

    if (name != null && string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty", nameof(name));

    // ...
}
```

### Example: Builder with Validation

```csharp
public sealed class ValidatedCapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();
    private bool _built = false;

    public ValidatedCapabilityProviderBuilder Add<T>(T capability) where T : class
    {
        if (_built)
            throw new InvalidOperationException("Cannot add after Build()");

        if (capability is null)
            throw new ArgumentNullException(nameof(capability));

        if (_registrations.ContainsKey(typeof(T)))
            throw new InvalidOperationException(
                $"Capability {typeof(T).Name} already registered");

        _registrations[typeof(T)] = capability;
        return this;
    }

    public ICapabilityProvider Build()
    {
        if (_built)
            throw new InvalidOperationException("Builder already built");

        _built = true;
        return new DictionaryCapabilityProvider(_registrations);
    }
}
```

---

## Immutable Product Pattern

### Why Immutable Products?

**Problem:** If builder returns mutable product, external code can modify it.

```csharp
// If product is mutable:
var capabilities = builder.Build();
capabilities.Add<ILogger>(someLogger); // ❌ Modifies shared state!
```

**Solution:** Builder returns immutable product.

```csharp
// Product is immutable:
var capabilities = builder.Build();
// No Add() method available on ICapabilityProvider
// Only TryGet<T>() is available
```

### VISORA's Immutable Product

```csharp
public ICapabilityProvider Build()
{
    if (_registrations.Count == 0)
        return CapabilityProviders.Empty;

    // Create immutable copy of dictionary
    return new DictionaryCapabilityProvider(
        new Dictionary<Type, object>(_registrations));
}

private sealed class DictionaryCapabilityProvider : ICapabilityProvider
{
    private readonly IReadOnlyDictionary<Type, object> _registrations;

    public DictionaryCapabilityProvider(IReadOnlyDictionary<Type, object> registrations)
        => _registrations = registrations;

    // Only read operation:
    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        // Read-only access
    }
}
```

**Key Points:**
1. Constructor takes `IReadOnlyDictionary` (not `Dictionary`)
2. No methods to modify the dictionary
3. `ICapabilityProvider` interface has no mutation methods

### Benefits

- **Thread-Safe:** Immutable objects are safe to share across threads
- **Predictable:** State never changes after construction
- **Cacheable:** Can cache and reuse without fear of mutation

### Builder Remains Mutable

```csharp
var builder = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger1);

// Build first product
var capabilities1 = builder.Build();

// Builder is still mutable
builder.Add<IUIManager>(uiManager);

// Build second product
var capabilities2 = builder.Build();

// capabilities1 is unchanged (immutable)
// capabilities2 has both logger and UI manager
```

**Pattern:** Builder is mutable during construction, product is immutable after Build().

---

## Testing Builders

### Example 1: Testing Builder Construction

```csharp
[TestClass]
public class CapabilityProviderBuilderTests
{
    [TestMethod]
    public void Add_SingleCapability_CanRetrieve()
    {
        // Arrange
        var logger = new Mock<ILogger>().Object;

        // Act
        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(logger)
            .Build();

        // Assert
        Assert.IsTrue(capabilities.TryGet<ILogger>(out var retrieved));
        Assert.AreSame(logger, retrieved);
    }

    [TestMethod]
    public void Add_MultipleCapabilities_AllRetrievable()
    {
        // Arrange
        var logger = new Mock<ILogger>().Object;
        var uiManager = new Mock<IUIManager>().Object;

        // Act
        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(logger)
            .Add<IUIManager>(uiManager)
            .Build();

        // Assert
        Assert.IsTrue(capabilities.TryGet<ILogger>(out var retrievedLogger));
        Assert.IsTrue(capabilities.TryGet<IUIManager>(out var retrievedUI));
        Assert.AreSame(logger, retrievedLogger);
        Assert.AreSame(uiManager, retrievedUI);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Add_NullCapability_ThrowsException()
    {
        // Act & Assert
        CapabilityProviders.CreateBuilder()
            .Add<ILogger>(null!); // Should throw
    }

    [TestMethod]
    public void Build_NoCapabilities_ReturnsEmpty()
    {
        // Act
        var capabilities = CapabilityProviders.CreateBuilder().Build();

        // Assert
        Assert.IsFalse(capabilities.TryGet<ILogger>(out _));
    }
}
```

### Example 2: Testing with Builder in Integration Tests

```csharp
[TestClass]
public class ModuleIntegrationTests
{
    [TestMethod]
    public async Task Module_WithCapabilities_UsesThemCorrectly()
    {
        // Arrange - Build test capabilities
        var mockLogger = new Mock<ILogger>();
        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(mockLogger.Object)
            .Build();

        var descriptor = ModuleDescriptor.Create("test", "Test", new Version(1, 0, 0));
        var context = new ModuleContext(descriptor, null, capabilities, null);
        var module = new TestModule();

        // Act
        await module.InitializeAsync(context, CancellationToken.None);

        // Assert
        mockLogger.Verify(x => x.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
    }
}
```

### Example 3: Property-Based Testing

```csharp
[TestMethod]
public void Builder_IsIdempotent()
{
    // Build same configuration twice
    var logger = new Mock<ILogger>().Object;

    var capabilities1 = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(logger)
        .Build();

    var capabilities2 = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(logger)
        .Build();

    // Both should retrieve the same logger instance
    capabilities1.TryGet<ILogger>(out var retrieved1);
    capabilities2.TryGet<ILogger>(out var retrieved2);

    Assert.AreSame(retrieved1, retrieved2);
}
```

---

## Best Practices

### 1. Return `this` from Configuration Methods

**✅ Good:**
```csharp
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    // ... logic
    return this; // Enable chaining
}
```

**❌ Avoid:**
```csharp
public void Add<T>(T capability) where T : class
{
    // ... logic
    // No return - breaks fluency
}
```

### 2. Use Generic Methods for Type Safety

**✅ Good:**
```csharp
builder.Add<ILogger>(logger); // Type-safe
```

**❌ Avoid:**
```csharp
builder.Add(typeof(ILogger), logger); // Loses type safety
```

### 3. Make Product Immutable

```csharp
public ICapabilityProvider Build()
{
    // Return immutable product
    return new DictionaryCapabilityProvider(
        new Dictionary<Type, object>(_registrations)); // Copy
}
```

### 4. Validate Inputs Early

```csharp
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    if (capability is null)
        throw new ArgumentNullException(nameof(capability)); // Fail fast

    _registrations[typeof(T)] = capability;
    return this;
}
```

### 5. Provide Empty/Default Options

```csharp
// Empty provider for no capabilities
public static ICapabilityProvider Empty { get; } = new NullCapabilityProvider();
```

### 6. Use Descriptive Method Names

**✅ Good:**
```csharp
builder.Add<ILogger>(logger)
builder.Build()
```

**❌ Avoid:**
```csharp
builder.Register<ILogger>(logger)  // Inconsistent with other methods
builder.Create()                   // Ambiguous
```

---

## Advanced Topics

### Topic 1: Builder with Fluent Validation

```csharp
public sealed class ValidatingCapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    public ValidatingCapabilityProviderBuilder Add<T>(T capability) where T : class
    {
        if (capability is null)
            throw new ArgumentNullException(nameof(capability));

        _registrations[typeof(T)] = capability;
        return this;
    }

    public ValidatingCapabilityProviderBuilder RequireAtLeastOne()
    {
        if (_registrations.Count == 0)
            throw new InvalidOperationException("At least one capability required");

        return this;
    }

    public ValidatingCapabilityProviderBuilder Require<T>() where T : class
    {
        if (!_registrations.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"Required capability {typeof(T).Name} not added");

        return this;
    }

    public ICapabilityProvider Build()
    {
        return new DictionaryCapabilityProvider(_registrations);
    }
}

// Usage:
var capabilities = new ValidatingCapabilityProviderBuilder()
    .Add<ILogger>(logger)
    .Add<IUIManager>(uiManager)
    .Require<ILogger>()        // Validate ILogger was added
    .RequireAtLeastOne()       // Validate not empty
    .Build();
```

### Topic 2: Builder with Conditional Registration

```csharp
public sealed class ConditionalCapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    public ConditionalCapabilityProviderBuilder Add<T>(T capability) where T : class
    {
        if (capability is null)
            throw new ArgumentNullException(nameof(capability));

        _registrations[typeof(T)] = capability;
        return this;
    }

    public ConditionalCapabilityProviderBuilder AddIf<T>(
        bool condition,
        T capability) where T : class
    {
        if (condition)
        {
            return Add(capability);
        }
        return this;
    }

    public ConditionalCapabilityProviderBuilder AddIf<T>(
        bool condition,
        Func<T> capabilityFactory) where T : class
    {
        if (condition)
        {
            return Add(capabilityFactory());
        }
        return this;
    }

    public ICapabilityProvider Build()
    {
        return new DictionaryCapabilityProvider(_registrations);
    }
}

// Usage:
var capabilities = new ConditionalCapabilityProviderBuilder()
    .Add<ILogger>(logger)
    .AddIf(enableUI, new UIManager())
    .AddIf(enableFileAccess, () => new FileSystem(basePath))
    .Build();
```

### Topic 3: Builder with Named Capabilities

```csharp
public sealed class NamedCapabilityProviderBuilder
{
    private readonly Dictionary<(Type Type, string? Name), object> _registrations = new();

    public NamedCapabilityProviderBuilder Add<T>(
        T capability,
        string? name = null) where T : class
    {
        if (capability is null)
            throw new ArgumentNullException(nameof(capability));

        _registrations[(typeof(T), name)] = capability;
        return this;
    }

    public INamedCapabilityProvider Build()
    {
        return new NamedCapabilityProvider(_registrations);
    }
}

// Usage:
var capabilities = new NamedCapabilityProviderBuilder()
    .Add<ILogger>(consoleLogger, name: "console")
    .Add<ILogger>(fileLogger, name: "file")
    .Add<ILogger>(defaultLogger)  // No name = default
    .Build();

// Retrieve by name:
var consoleLogger = capabilities.TryGet<ILogger>("console", out var logger);
```

---

## Common Pitfalls

### Pitfall 1: Not Returning `this`

```csharp
// ❌ BAD: Breaks fluency
public void Add<T>(T capability) where T : class
{
    _registrations[typeof(T)] = capability;
    // No return!
}

// Usage is cumbersome:
var builder = new Builder();
builder.Add<ILogger>(logger);
builder.Add<IUIManager>(uiManager);
var product = builder.Build();

// ✅ GOOD: Return this
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    _registrations[typeof(T)] = capability;
    return this;
}

// Usage is fluent:
var product = builder
    .Add<ILogger>(logger)
    .Add<IUIManager>(uiManager)
    .Build();
```

### Pitfall 2: Mutable Product

```csharp
// ❌ BAD: Product is mutable
public Dictionary<Type, object> Build()
{
    return _registrations; // Returns reference to mutable dictionary!
}

// Client can mutate:
var product = builder.Build();
product[typeof(ILogger)] = someOtherLogger; // ❌ Mutates shared state

// ✅ GOOD: Product is immutable
public ICapabilityProvider Build()
{
    return new DictionaryCapabilityProvider(
        new Dictionary<Type, object>(_registrations)); // Immutable copy
}
```

### Pitfall 3: No Validation

```csharp
// ❌ BAD: No validation
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    _registrations[typeof(T)] = capability; // Accepts null!
    return this;
}

// Null sneaks in:
builder.Add<ILogger>(null); // No error yet
var product = builder.Build(); // Error later?

// ✅ GOOD: Validate early
public CapabilityProviderBuilder Add<T>(T capability) where T : class
{
    if (capability is null)
        throw new ArgumentNullException(nameof(capability)); // Fail fast

    _registrations[typeof(T)] = capability;
    return this;
}
```

### Pitfall 4: Builder State Leaks to Product

```csharp
// ❌ BAD: Product references builder's mutable state
public class BadBuilder
{
    private Dictionary<Type, object> _registrations = new();

    public BadBuilder Add<T>(T capability) where T : class
    {
        _registrations[typeof(T)] = capability;
        return this;
    }

    public BadProduct Build()
    {
        return new BadProduct(_registrations); // Passes reference!
    }
}

public class BadProduct
{
    private Dictionary<Type, object> _registrations; // Mutable!

    public BadProduct(Dictionary<Type, object> registrations)
    {
        _registrations = registrations; // Shared reference!
    }
}

// Problem:
var builder = new BadBuilder().Add<ILogger>(logger);
var product = builder.Build();
builder.Add<IUIManager>(uiManager); // ❌ Also modifies product!

// ✅ GOOD: Product has own immutable copy
public ICapabilityProvider Build()
{
    return new DictionaryCapabilityProvider(
        new Dictionary<Type, object>(_registrations)); // Copy
}
```

---

## Summary

### Key Takeaways

1. **Builder Pattern for Complex Configuration**
   - Fluent interface for readability
   - Step-by-step construction
   - Immutable product

2. **Fluent Interface Design**
   - Methods return `this`
   - Enable chaining
   - Terminal method (Build) returns product

3. **Validation Strategies**
   - Validate per-step (VISORA's approach) for immediate feedback
   - Or validate on Build for deferred checking

4. **Immutable Products**
   - Builder is mutable during construction
   - Product is immutable after Build()
   - Thread-safe and predictable

5. **Testing Benefits**
   - Easy to mock capabilities in tests
   - Fluent API makes test setup readable
   - Can verify builder behavior independently

### VISORA's Builder Pattern

- **CapabilityProviderBuilder:** Fluent API for configuring capabilities
- **Add<T>():** Type-safe capability registration
- **Build():** Returns immutable ICapabilityProvider
- **Validation:** Null checks per-Add, fail fast

### Common Patterns

- **Fluent Configuration:** Return `this` for chaining
- **Terminal Method:** `Build()` returns product
- **Immutable Product:** Copy internal state on Build
- **Empty Optimization:** Return singleton for empty state

### Anti-Patterns to Avoid

- ❌ Not returning `this` (breaks fluency)
- ❌ Mutable product (allows external mutation)
- ❌ No validation (allows invalid state)
- ❌ Leaking builder state to product

### Related Patterns

- **Factory Pattern:** Builder is a sophisticated factory
- **Immutable Objects:** Products are immutable
- **Fluent Interface:** Method chaining for readability
- **Dependency Injection:** Builder configures DI container equivalent

---

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
