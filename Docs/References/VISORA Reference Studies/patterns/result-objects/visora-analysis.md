# Result Objects Pattern - VISORA Analysis

**Last Updated**: 2025-11-10
**VISORA Version**: .NET 9.0
**Pattern Tier**: Tier 2 (Communication & Execution)

---

## Pattern Overview

VISORA uses **Result Objects** (`CommandResult`) to represent command execution outcomes without throwing exceptions. This pattern enables clean error handling across process, language, and serialization boundaries.

### What is This Pattern?

Result objects are immutable value types that:
- Encapsulate operation outcome (Success, Cancelled, Failed)
- Include optional message and payload
- Use factory methods for consistent construction
- Avoid exceptions for expected failure cases
- Are serialization-friendly (no exception objects)

### Why VISORA Uses Result Objects

**Problem**: Commands execute across different surfaces (CLI, UI, remote). Throwing exceptions across these boundaries is problematic:
- Stack traces don't serialize well
- Exception types may not exist in target runtime
- Exceptions represent control flow, not data
- Expensive performance overhead for expected failures

**Solution**: Return structured result objects that describe the outcome.

**Benefits**:
- Clean separation of expected vs unexpected failures
- Serialization-friendly (JSON, binary)
- Explicit outcome handling (no hidden exceptions)
- Performance-friendly (no exception overhead)
- Works across language boundaries

---

## VISORA Implementation

### CommandResult Structure

**Definition** (`src/Visora.Contracts/Commands/CommandResult.cs:8-40`)

```csharp
/// <summary>
/// Represents the result of a command execution.
/// Uses readonly struct for value semantics and performance.
/// </summary>
public readonly record struct CommandResult(
    CommandOutcome Outcome,
    string? Message = null,
    object? Payload = null)
{
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static CommandResult Success(string? message = null, object? payload = null)
        => new(CommandOutcome.Success, message, payload);

    /// <summary>
    /// Create a cancelled result.
    /// </summary>
    public static CommandResult Cancelled(string? message = null)
        => new(CommandOutcome.Cancelled, message, null);

    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static CommandResult Failed(string? message = null, object? payload = null)
        => new(CommandOutcome.Failed, message, payload);

    /// <summary>
    /// Possible outcomes for command execution.
    /// </summary>
    public enum CommandOutcome
    {
        Success,    // Command completed successfully
        Cancelled,  // Command was cancelled by user or system
        Failed      // Command failed (error details in Message/Payload)
    }
}
```

**Key Design Decisions**:

1. **`readonly record struct`**: Value semantics, immutability, automatic equality
2. **Factory methods**: `Success()`, `Cancelled()`, `Failed()` provide clarity at call site
3. **Optional message**: Human-readable outcome description
4. **Optional payload**: Structured data (success data or error details)
5. **No exception field**: Keeps results serialization-friendly

---

## Factory Methods in Detail

### Success Factory

```csharp
public static CommandResult Success(string? message = null, object? payload = null)
    => new(CommandOutcome.Success, message, payload);
```

**Usage Examples:**

```csharp
// Simple success
return CommandResult.Success();

// Success with message
return CommandResult.Success("File saved successfully");

// Success with data
return CommandResult.Success(
    "Retrieved 42 records",
    new { Count = 42, Records = records });

// Success with anonymous payload
return CommandResult.Success(
    "Environment info retrieved",
    new
    {
        ProcessId = Environment.ProcessId,
        MachineName = Environment.MachineName,
        WorkingDirectory = Environment.CurrentDirectory
    });
```

### Failed Factory

```csharp
public static CommandResult Failed(string? message = null, object? payload = null)
    => new(CommandOutcome.Failed, message, payload);
```

**Usage Examples:**

```csharp
// Simple failure
return CommandResult.Failed("Operation failed");

// Failure with error details
return CommandResult.Failed(
    "File not found",
    new { Path = filePath, Searched = searchPaths });

// Failure with exception details (serializable)
return CommandResult.Failed(
    $"Database error: {ex.Message}",
    new
    {
        ErrorType = ex.GetType().Name,
        Message = ex.Message,
        StackTrace = ex.StackTrace?.Split('\n').Take(5) // Truncated stack
    });

// Validation failure
return CommandResult.Failed(
    "Validation failed",
    new
    {
        Errors = new[]
        {
            new { Field = "Email", Error = "Invalid format" },
            new { Field = "Age", Error = "Must be positive" }
        }
    });
```

### Cancelled Factory

```csharp
public static CommandResult Cancelled(string? message = null)
    => new(CommandOutcome.Cancelled, message, null);
```

**Usage Examples:**

```csharp
// Simple cancellation
return CommandResult.Cancelled();

// Cancellation with context
return CommandResult.Cancelled("Operation cancelled by user");

// Cancellation with partial progress
return CommandResult.Cancelled(
    $"Cancelled after processing {processedCount} of {totalCount} items");
```

---

## When to Use Results vs Exceptions

### Use Result Objects For:

1. **Expected Failure Conditions**
   ```csharp
   // File not found is expected
   if (!File.Exists(path))
       return CommandResult.Failed($"File not found: {path}");
   ```

2. **Validation Errors**
   ```csharp
   if (string.IsNullOrWhiteSpace(email))
       return CommandResult.Failed("Email is required");
   ```

3. **Business Rule Violations**
   ```csharp
   if (account.Balance < amount)
       return CommandResult.Failed("Insufficient funds");
   ```

4. **User Cancellation**
   ```csharp
   if (ct.IsCancellationRequested)
       return CommandResult.Cancelled("User cancelled operation");
   ```

5. **Cross-Boundary Communication**
   ```csharp
   // Results serialize cleanly across process/language boundaries
   var result = await RemoteCommandExecutor.ExecuteAsync(commandId, parameters);
   if (result.Outcome == CommandOutcome.Failed)
       Console.WriteLine($"Remote command failed: {result.Message}");
   ```

### Use Exceptions For:

1. **Programming Errors**
   ```csharp
   if (context is null)
       throw new ArgumentNullException(nameof(context));
   ```

2. **Infrastructure Failures**
   ```csharp
   // Database connection failure is unexpected
   if (!await database.ConnectAsync())
       throw new InvalidOperationException("Database unavailable");
   ```

3. **Contract Violations**
   ```csharp
   if (module.Descriptor is null)
       throw new InvalidOperationException(
           $"Module '{moduleType}' returned null descriptor");
   ```

4. **Truly Exceptional Conditions**
   ```csharp
   // Out of memory, corrupted data, etc.
   if (data.Length > MaxSize)
       throw new InvalidOperationException(
           $"Data exceeds maximum size: {data.Length} > {MaxSize}");
   ```

### Decision Matrix

| Scenario | Use Result | Use Exception |
|----------|-----------|---------------|
| File not found | ✅ | ❌ |
| Invalid user input | ✅ | ❌ |
| Network timeout | ✅ | ❌ |
| User cancellation | ✅ | ❌ |
| Null argument | ❌ | ✅ |
| Out of memory | ❌ | ✅ |
| Programming bug | ❌ | ✅ |
| Database unavailable | ❌ | ✅ |

**General Rule**: If it's **expected** in normal operation, use a result. If it's **unexpected** and represents a bug or infrastructure failure, use an exception.

---

## Payload Handling Patterns

### Pattern 1: Anonymous Objects for Success Data

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var data = FetchData();

    return ValueTask.FromResult(CommandResult.Success(
        $"Retrieved {data.Count} items",
        new
        {
            Count = data.Count,
            Items = data.Items,
            Timestamp = DateTimeOffset.UtcNow
        }));
}
```

**Benefits**: Clean, type-safe, serializable

### Pattern 2: Dictionaries for Dynamic Data

```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var payload = new Dictionary<string, object?>
    {
        ["ProcessId"] = Environment.ProcessId,
        ["MachineName"] = Environment.MachineName,
        ["WorkingDirectory"] = Environment.CurrentDirectory
    };

    return ValueTask.FromResult(CommandResult.Success(
        "Environment info",
        payload));
}
```

**Benefits**: Dynamic, extensible, key-value access

### Pattern 3: Typed Result Classes

```csharp
public sealed record FileOperationResult(
    string Path,
    long Size,
    DateTimeOffset LastModified,
    string[] Warnings = null);

public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var fileInfo = new FileInfo(path);
    var result = new FileOperationResult(
        fileInfo.FullName,
        fileInfo.Length,
        fileInfo.LastWriteTimeUtc);

    return CommandResult.Success(
        $"File info retrieved: {fileInfo.Name}",
        result);
}
```

**Benefits**: Strongly typed, IntelliSense support, compile-time safety

### Pattern 4: Error Details in Payload

```csharp
public sealed record ValidationError(string Field, string Message);

public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var errors = new List<ValidationError>();

    if (string.IsNullOrWhiteSpace(name))
        errors.Add(new ValidationError("Name", "Name is required"));

    if (age < 0)
        errors.Add(new ValidationError("Age", "Age must be positive"));

    if (errors.Any())
    {
        return ValueTask.FromResult(CommandResult.Failed(
            $"Validation failed: {errors.Count} errors",
            new { Errors = errors }));
    }

    // Success path...
    return ValueTask.FromResult(CommandResult.Success("Validated successfully"));
}
```

### Pattern 5: Partial Success with Warnings

```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var warnings = new List<string>();
    var processed = 0;

    foreach (var item in items)
    {
        try
        {
            await ProcessItemAsync(item, ct);
            processed++;
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to process {item}: {ex.Message}");
        }
    }

    var payload = new
    {
        ProcessedCount = processed,
        TotalCount = items.Count,
        Warnings = warnings
    };

    return CommandResult.Success(
        $"Processed {processed} of {items.Count} items",
        payload);
}
```

---

## Complete Command Examples

### Example 1: File Read Command

```csharp
public sealed class FileReadCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "file.read",
        title: "Read File",
        description: "Reads content from a file.");

    public override CommandDescriptor Descriptor => Info;

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken ct)
    {
        // Extract parameters
        if (!context.Parameters.TryGetValue("path", out var pathObj) ||
            pathObj is not string path)
        {
            return CommandResult.Failed(
                "Parameter 'path' is required and must be a string");
        }

        // Validate file exists
        if (!File.Exists(path))
        {
            return CommandResult.Failed(
                $"File not found: {path}",
                new { Path = path, Exists = false });
        }

        try
        {
            // Check cancellation before I/O
            ct.ThrowIfCancellationRequested();

            // Read file
            var content = await File.ReadAllTextAsync(path, ct);

            // Return success with payload
            return CommandResult.Success(
                $"Read {content.Length} characters from {Path.GetFileName(path)}",
                new
                {
                    Path = path,
                    Length = content.Length,
                    Content = content,
                    ReadAt = DateTimeOffset.UtcNow
                });
        }
        catch (OperationCanceledException)
        {
            return CommandResult.Cancelled(
                $"File read cancelled: {Path.GetFileName(path)}");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult.Failed(
                "Access denied",
                new { Path = path, Error = "UnauthorizedAccess" });
        }
        catch (IOException ex)
        {
            return CommandResult.Failed(
                $"I/O error: {ex.Message}",
                new { Path = path, Error = ex.GetType().Name, Message = ex.Message });
        }
    }
}
```

### Example 2: Database Query Command

```csharp
public sealed class DatabaseQueryCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken ct)
    {
        // Get database capability
        var db = context.Capabilities.GetOptional<IDatabase>();
        if (db is null)
        {
            return CommandResult.Failed(
                "Database capability not available",
                new { RequiredCapability = "IDatabase" });
        }

        // Get query parameter
        if (!context.Parameters.TryGetValue("query", out var queryObj) ||
            queryObj is not string query)
        {
            return CommandResult.Failed("Parameter 'query' is required");
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            // Execute query
            var results = await db.QueryAsync(query, ct);

            return CommandResult.Success(
                $"Query returned {results.Count} rows",
                new
                {
                    Query = query,
                    RowCount = results.Count,
                    Results = results,
                    ExecutedAt = DateTimeOffset.UtcNow
                });
        }
        catch (OperationCanceledException)
        {
            return CommandResult.Cancelled("Query cancelled");
        }
        catch (DbException ex)
        {
            return CommandResult.Failed(
                $"Database error: {ex.Message}",
                new
                {
                    Query = query,
                    ErrorCode = ex.ErrorCode,
                    Message = ex.Message
                });
        }
    }
}
```

---

## Testing Result Objects

### Testing Success Cases

```csharp
[TestClass]
public class ResultObjectTests
{
    [TestMethod]
    public void Success_WithMessageAndPayload_CreatesCorrectResult()
    {
        // Arrange
        var message = "Operation completed";
        var payload = new { Count = 42 };

        // Act
        var result = CommandResult.Success(message, payload);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.AreEqual(message, result.Message);
        Assert.IsNotNull(result.Payload);

        dynamic payloadDynamic = result.Payload;
        Assert.AreEqual(42, payloadDynamic.Count);
    }

    [TestMethod]
    public void Success_WithoutParameters_CreatesSuccessResult()
    {
        // Act
        var result = CommandResult.Success();

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNull(result.Message);
        Assert.IsNull(result.Payload);
    }
}
```

### Testing Failure Cases

```csharp
[TestClass]
public class CommandFailureTests
{
    [TestMethod]
    public async Task FileReadCommand_FileNotFound_ReturnsFailedResult()
    {
        // Arrange
        var command = new FileReadCommand();
        var context = new CommandContext(
            module: CreateMockModule(),
            component: null,
            surface: CommandSurface.Programmatic,
            capabilities: CapabilityProviders.Empty,
            parameters: new Dictionary<string, object?>
            {
                ["path"] = "nonexistent.txt"
            });

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Failed, result.Outcome);
        Assert.IsTrue(result.Message.Contains("not found"));
        Assert.IsNotNull(result.Payload);
    }
}
```

### Testing Cancellation

```csharp
[TestClass]
public class CancellationTests
{
    [TestMethod]
    public async Task LongRunningCommand_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var command = new LongRunningCommand();
        var context = CreateTestContext();
        var cts = new CancellationTokenSource();

        // Act
        var task = command.ExecuteAsync(context, cts.Token);
        await Task.Delay(100); // Let it start
        cts.Cancel(); // Cancel mid-execution
        var result = await task;

        // Assert
        Assert.AreEqual(CommandOutcome.Cancelled, result.Outcome);
        Assert.IsNotNull(result.Message);
    }
}
```

### Testing Payload Structure

```csharp
[TestClass]
public class PayloadTests
{
    [TestMethod]
    public async Task EnvironmentInfoCommand_ReturnsCorrectPayloadStructure()
    {
        // Arrange
        var command = new EnvironmentInfoCommand();
        var context = CreateTestContext();

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Payload);

        // Verify payload has expected fields
        var payload = result.Payload as Dictionary<string, object?>;
        Assert.IsNotNull(payload);
        Assert.IsTrue(payload.ContainsKey("ProcessId"));
        Assert.IsTrue(payload.ContainsKey("MachineName"));
        Assert.IsTrue(payload.ContainsKey("OSVersion"));
    }
}
```

---

## Best Practices

### 1. Consistent Factory Method Usage

```csharp
// ✅ Good: Use factory methods
return CommandResult.Success("Done");
return CommandResult.Failed("Error");
return CommandResult.Cancelled();

// ❌ Bad: Direct construction
return new CommandResult(CommandOutcome.Success, "Done");
```

### 2. Descriptive Messages

```csharp
// ✅ Good: Clear, actionable message
return CommandResult.Failed(
    "File 'config.json' not found in directory 'C:\\App\\Config'");

// ❌ Bad: Vague message
return CommandResult.Failed("Error");
```

### 3. Structured Payloads

```csharp
// ✅ Good: Structured data
return CommandResult.Success("Imported data", new
{
    ImportedCount = 100,
    SkippedCount = 5,
    Errors = errorList,
    Duration = stopwatch.Elapsed
});

// ❌ Bad: Unstructured string
return CommandResult.Success(
    "Imported 100, skipped 5, took 2.5s");
```

### 4. Don't Embed Exceptions in Payload

```csharp
// ✅ Good: Serialize exception details
catch (Exception ex)
{
    return CommandResult.Failed(
        $"Error: {ex.Message}",
        new
        {
            ErrorType = ex.GetType().Name,
            Message = ex.Message
        });
}

// ❌ Bad: Store exception object
catch (Exception ex)
{
    return CommandResult.Failed("Error", ex); // Won't serialize!
}
```

### 5. Use Typed Payloads for Complex Results

```csharp
// ✅ Good: Typed result
public record ImportResult(
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<string> Errors,
    TimeSpan Duration);

return CommandResult.Success(
    "Import complete",
    new ImportResult(100, 5, errors, duration));

// ❌ Avoid: Dictionary for complex data
var payload = new Dictionary<string, object>
{
    ["ImportedCount"] = 100,
    ["SkippedCount"] = 5,
    // ... typos and type errors possible
};
```

### 6. Handle All Outcome Types

```csharp
// ✅ Good: Handle all cases
var result = await command.ExecuteAsync(context, ct);

switch (result.Outcome)
{
    case CommandOutcome.Success:
        Console.WriteLine($"✓ {result.Message}");
        ProcessPayload(result.Payload);
        break;

    case CommandOutcome.Cancelled:
        Console.WriteLine($"⚠ {result.Message}");
        break;

    case CommandOutcome.Failed:
        Console.Error.WriteLine($"✗ {result.Message}");
        LogError(result.Payload);
        break;
}

// ❌ Bad: Only check success
if (result.Outcome == CommandOutcome.Success)
{
    ProcessPayload(result.Payload);
}
// What about failures and cancellations?
```

---

## Related Patterns

- **[Command Execution](../command-execution/visora-analysis.md)**: How commands use result objects
- **[Async Patterns](../async-patterns/visora-analysis.md)**: Async result handling
- **[Multi-Surface Execution](../multi-surface-execution/visora-analysis.md)**: Results across different surfaces
- **[Result Objects - Meta-Platform](./meta-platform-illustrations.md)**: Cross-language result patterns

---

## Summary

VISORA's Result Objects pattern provides:
- ✅ Clean separation of expected vs unexpected failures
- ✅ Serialization-friendly outcomes (no exception objects)
- ✅ Explicit outcome handling (Success/Failed/Cancelled)
- ✅ Flexible payload support (anonymous objects, typed records)
- ✅ Performance-friendly (no exception overhead)

**Key Takeaway**: Use `CommandResult` for all command execution outcomes. Reserve exceptions for truly exceptional conditions (programming errors, infrastructure failures). This enables clean error handling across process and language boundaries.
