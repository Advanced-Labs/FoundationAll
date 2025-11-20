# Result Objects - Meta-Platform Illustrations

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Document Type:** Illustrative Examples / Thought Experiments

---

## ⚠️ IMPORTANT DISCLAIMER

**This document contains ILLUSTRATIVE EXAMPLES and THOUGHT EXPERIMENTS only.**

These are NOT:
- ❌ Prescriptive designs
- ❌ Proven implementations
- ❌ Production-ready code
- ❌ Official recommendations

These ARE:
- ✅ Conceptual explorations
- ✅ Inspiration for possibilities
- ✅ Starting points for investigation
- ✅ Creative adaptations of VISORA patterns

**Use these examples to spark imagination, not as blueprints.**

---

## Table of Contents

1. [Conceptual Adaptation](#conceptual-adaptation)
2. [Python Result Objects](#python-result-objects)
3. [Node.js Result Objects](#nodejs-result-objects)
4. [Error Marshaling Across Boundaries](#error-marshaling-across-boundaries)
5. [Exception Translation Patterns](#exception-translation-patterns)
6. [Serialization Challenges](#serialization-challenges)
7. [Cross-Language Result Patterns](#cross-language-result-patterns)
8. [Challenges & Considerations](#challenges--considerations)

---

## Conceptual Adaptation

### From .NET CommandResult to Polyglot Result Objects

**VISORA's .NET Pattern:**
```csharp
public readonly record struct CommandResult(
    CommandOutcome Outcome,      // Success | Cancelled | Failed
    string? Message = null,      // Human-readable description
    object? Payload = null)      // Structured data or error details
{
    public static CommandResult Success(string? message, object? payload);
    public static CommandResult Failed(string? message, object? payload);
    public static CommandResult Cancelled(string? message);
}
```

**Conceptual Meta-Platform Adaptation:**
```
Universal Result Pattern
  │
  ├─ .NET: CommandResult (readonly struct)
  │  └─ Serialized to: JSON, MessagePack, etc.
  │
  ├─ Python: Result dataclass
  │  └─ Deserialized from: JSON, dict
  │
  ├─ Node.js: Result object/class
  │  └─ Deserialized from: JSON
  │
  └─ Common Schema
     ├─ outcome: "success" | "cancelled" | "failed"
     ├─ message: string | null
     └─ payload: any | null
```

---

## Python Result Objects

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Dataclass-Based Result

**Conceptual Approach:** Use Python dataclasses to mirror .NET CommandResult.

**Illustrative Python Code:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE - visora_result.py

from dataclasses import dataclass, field
from typing import Any, Optional, Literal
from enum import Enum

class CommandOutcome(str, Enum):
    """
    Possible outcomes for command execution.
    Mirrors .NET CommandOutcome enum.
    """
    SUCCESS = "success"
    CANCELLED = "cancelled"
    FAILED = "failed"


@dataclass(frozen=True)
class CommandResult:
    """
    Immutable result object for command execution.
    Mirrors .NET CommandResult structure.
    """
    outcome: CommandOutcome
    message: Optional[str] = None
    payload: Optional[Any] = None

    @staticmethod
    def success(message: Optional[str] = None, payload: Optional[Any] = None):
        """Create a successful result."""
        return CommandResult(
            outcome=CommandOutcome.SUCCESS,
            message=message,
            payload=payload
        )

    @staticmethod
    def failed(message: Optional[str] = None, payload: Optional[Any] = None):
        """Create a failed result."""
        return CommandResult(
            outcome=CommandOutcome.FAILED,
            message=message,
            payload=payload
        )

    @staticmethod
    def cancelled(message: Optional[str] = None):
        """Create a cancelled result."""
        return CommandResult(
            outcome=CommandOutcome.CANCELLED,
            message=message,
            payload=None
        )

    def to_dict(self) -> dict:
        """
        Serialize to dictionary for JSON serialization.
        """
        return {
            "outcome": self.outcome.value,
            "message": self.message,
            "payload": self.payload
        }

    @classmethod
    def from_dict(cls, data: dict):
        """
        Deserialize from dictionary (from JSON).
        """
        return cls(
            outcome=CommandOutcome(data["outcome"]),
            message=data.get("message"),
            payload=data.get("payload")
        )

    def is_success(self) -> bool:
        """Check if result represents success."""
        return self.outcome == CommandOutcome.SUCCESS

    def is_failed(self) -> bool:
        """Check if result represents failure."""
        return self.outcome == CommandOutcome.FAILED

    def is_cancelled(self) -> bool:
        """Check if result represents cancellation."""
        return self.outcome == CommandOutcome.CANCELLED


# ⚠️ ILLUSTRATIVE USAGE EXAMPLES

def example_command(param: str) -> CommandResult:
    """Example command returning result."""

    # Validation failure
    if not param:
        return CommandResult.failed(
            "Parameter is required",
            {"field": "param", "error": "empty"}
        )

    # Success with data
    return CommandResult.success(
        f"Processed: {param}",
        {"input": param, "length": len(param)}
    )


async def async_file_read(path: str) -> CommandResult:
    """Async command with result object."""
    import aiofiles
    import os

    # Check file exists
    if not os.path.exists(path):
        return CommandResult.failed(
            f"File not found: {path}",
            {"path": path, "exists": False}
        )

    try:
        async with aiofiles.open(path, 'r') as f:
            content = await f.read()

        return CommandResult.success(
            f"Read {len(content)} characters",
            {
                "path": path,
                "length": len(content),
                "content": content
            }
        )

    except PermissionError:
        return CommandResult.failed(
            "Access denied",
            {"path": path, "error": "PermissionError"}
        )

    except Exception as ex:
        return CommandResult.failed(
            f"Error reading file: {str(ex)}",
            {
                "path": path,
                "error_type": type(ex).__name__,
                "error_message": str(ex)
            }
        )
```

---

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Pattern Matching with Results

**Python 3.10+ Pattern Matching:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE

def handle_result(result: CommandResult) -> None:
    """
    Handle result using pattern matching (Python 3.10+).
    """
    match result.outcome:
        case CommandOutcome.SUCCESS:
            print(f"✓ Success: {result.message}")
            if result.payload:
                print(f"  Data: {result.payload}")

        case CommandOutcome.CANCELLED:
            print(f"⚠ Cancelled: {result.message}")

        case CommandOutcome.FAILED:
            print(f"✗ Failed: {result.message}")
            if result.payload:
                print(f"  Error details: {result.payload}")


# Alternative: if/elif style
def handle_result_classic(result: CommandResult) -> None:
    """Handle result using if/elif."""

    if result.is_success():
        print(f"✓ {result.message}")
        process_success_payload(result.payload)

    elif result.is_cancelled():
        print(f"⚠ {result.message}")

    elif result.is_failed():
        print(f"✗ {result.message}")
        log_error(result.payload)
```

---

## Node.js Result Objects

### ⚠️ ILLUSTRATIVE EXAMPLE: TypeScript Result Class

**Conceptual Approach:** Use TypeScript class with static factory methods.

**Illustrative TypeScript Code:**

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE - CommandResult.ts

/**
 * Possible outcomes for command execution.
 */
export enum CommandOutcome {
    Success = 'success',
    Cancelled = 'cancelled',
    Failed = 'failed'
}

/**
 * Result object for command execution.
 * Mirrors .NET CommandResult structure.
 */
export class CommandResult<TPayload = any> {
    constructor(
        public readonly outcome: CommandOutcome,
        public readonly message?: string,
        public readonly payload?: TPayload
    ) {}

    /**
     * Create a successful result.
     */
    static success<T = any>(message?: string, payload?: T): CommandResult<T> {
        return new CommandResult(CommandOutcome.Success, message, payload);
    }

    /**
     * Create a failed result.
     */
    static failed<T = any>(message?: string, payload?: T): CommandResult<T> {
        return new CommandResult(CommandOutcome.Failed, message, payload);
    }

    /**
     * Create a cancelled result.
     */
    static cancelled(message?: string): CommandResult<never> {
        return new CommandResult(CommandOutcome.Cancelled, message);
    }

    /**
     * Check if result represents success.
     */
    isSuccess(): boolean {
        return this.outcome === CommandOutcome.Success;
    }

    /**
     * Check if result represents failure.
     */
    isFailed(): boolean {
        return this.outcome === CommandOutcome.Failed;
    }

    /**
     * Check if result represents cancellation.
     */
    isCancelled(): boolean {
        return this.outcome === CommandOutcome.Cancelled;
    }

    /**
     * Serialize to plain object for JSON.
     */
    toJSON(): object {
        return {
            outcome: this.outcome,
            message: this.message,
            payload: this.payload
        };
    }

    /**
     * Deserialize from plain object.
     */
    static fromJSON<T = any>(data: any): CommandResult<T> {
        return new CommandResult(
            data.outcome,
            data.message,
            data.payload
        );
    }
}


// ⚠️ ILLUSTRATIVE USAGE EXAMPLES

interface FileReadPayload {
    path: string;
    length: number;
    content: string;
}

async function fileReadCommand(path: string): Promise<CommandResult<FileReadPayload>> {
    const fs = require('fs').promises;

    try {
        // Check file exists
        await fs.access(path);

        // Read file
        const content = await fs.readFile(path, 'utf8');

        return CommandResult.success(
            `Read ${content.length} characters`,
            {
                path: path,
                length: content.length,
                content: content
            }
        );

    } catch (error: any) {
        if (error.code === 'ENOENT') {
            return CommandResult.failed(
                `File not found: ${path}`,
                { path, exists: false }
            );
        }

        if (error.code === 'EACCES') {
            return CommandResult.failed(
                'Access denied',
                { path, error: 'PermissionDenied' }
            );
        }

        return CommandResult.failed(
            `Error reading file: ${error.message}`,
            {
                path,
                errorType: error.constructor.name,
                errorMessage: error.message
            }
        );
    }
}


// Handle result
async function main() {
    const result = await fileReadCommand('example.txt');

    switch (result.outcome) {
        case CommandOutcome.Success:
            console.log(`✓ ${result.message}`);
            console.log(`  Content length: ${result.payload?.length}`);
            break;

        case CommandOutcome.Cancelled:
            console.log(`⚠ ${result.message}`);
            break;

        case CommandOutcome.Failed:
            console.error(`✗ ${result.message}`);
            console.error(`  Error:`, result.payload);
            break;
    }
}
```

---

### ⚠️ ILLUSTRATIVE EXAMPLE: Plain JavaScript Result Object

**For environments without TypeScript:**

```javascript
// ⚠️ ILLUSTRATIVE EXAMPLE - commandResult.js

const CommandOutcome = {
    SUCCESS: 'success',
    CANCELLED: 'cancelled',
    FAILED: 'failed'
};

class CommandResult {
    constructor(outcome, message = null, payload = null) {
        this.outcome = outcome;
        this.message = message;
        this.payload = payload;
        Object.freeze(this); // Make immutable
    }

    static success(message, payload) {
        return new CommandResult(CommandOutcome.SUCCESS, message, payload);
    }

    static failed(message, payload) {
        return new CommandResult(CommandOutcome.FAILED, message, payload);
    }

    static cancelled(message) {
        return new CommandResult(CommandOutcome.CANCELLED, message);
    }

    isSuccess() {
        return this.outcome === CommandOutcome.SUCCESS;
    }

    isFailed() {
        return this.outcome === CommandOutcome.FAILED;
    }

    isCancelled() {
        return this.outcome === CommandOutcome.CANCELLED;
    }
}

module.exports = { CommandResult, CommandOutcome };
```

---

## Error Marshaling Across Boundaries

### ⚠️ ILLUSTRATIVE PATTERN: .NET → Python Error Translation

**Conceptual Challenge:** .NET exception needs to become Python result.

**Illustrative C# Code:**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class DotNetToPythonResultMarshaler
{
    public dynamic MarshalResultToPython(CommandResult dotnetResult)
    {
        using (Py.GIL())
        {
            dynamic visora_result = Py.Import("visora_result");

            switch (dotnetResult.Outcome)
            {
                case CommandOutcome.Success:
                    return visora_result.CommandResult.success(
                        dotnetResult.Message,
                        ConvertPayloadToPython(dotnetResult.Payload)
                    );

                case CommandOutcome.Cancelled:
                    return visora_result.CommandResult.cancelled(
                        dotnetResult.Message
                    );

                case CommandOutcome.Failed:
                    return visora_result.CommandResult.failed(
                        dotnetResult.Message,
                        ConvertPayloadToPython(dotnetResult.Payload)
                    );

                default:
                    throw new InvalidOperationException(
                        $"Unknown outcome: {dotnetResult.Outcome}");
            }
        }
    }

    private dynamic ConvertPayloadToPython(object? payload)
    {
        if (payload is null)
            return Py.None;

        // Serialize to JSON and let Python deserialize
        var json = JsonSerializer.Serialize(payload);

        using (Py.GIL())
        {
            dynamic jsonModule = Py.Import("json");
            return jsonModule.loads(json);
        }
    }

    public CommandResult MarshalResultFromPython(dynamic pythonResult)
    {
        using (Py.GIL())
        {
            // Convert Python result to dictionary
            var resultDict = pythonResult.to_dict();

            var outcome = ParseOutcome(resultDict["outcome"].ToString());
            var message = resultDict["message"]?.ToString();
            var payload = ConvertPayloadFromPython(resultDict["payload"]);

            return outcome switch
            {
                CommandOutcome.Success => CommandResult.Success(message, payload),
                CommandOutcome.Cancelled => CommandResult.Cancelled(message),
                CommandOutcome.Failed => CommandResult.Failed(message, payload),
                _ => throw new InvalidOperationException($"Unknown outcome: {outcome}")
            };
        }
    }

    private object? ConvertPayloadFromPython(dynamic pythonPayload)
    {
        if (pythonPayload is null || pythonPayload == Py.None)
            return null;

        // Convert to JSON and deserialize
        using (Py.GIL())
        {
            dynamic jsonModule = Py.Import("json");
            var json = jsonModule.dumps(pythonPayload).ToString();
            return JsonSerializer.Deserialize<object>(json);
        }
    }

    private CommandOutcome ParseOutcome(string outcome) => outcome.ToLowerInvariant() switch
    {
        "success" => CommandOutcome.Success,
        "cancelled" => CommandOutcome.Cancelled,
        "failed" => CommandOutcome.Failed,
        _ => throw new ArgumentException($"Invalid outcome: {outcome}")
    };
}
```

---

### ⚠️ ILLUSTRATIVE PATTERN: .NET → Node.js Error Translation

**Illustrative C# Code:**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class DotNetToNodeResultMarshaler
{
    public async Task<string> MarshalResultToNodeJson(CommandResult result)
    {
        var jsonObject = new
        {
            outcome = result.Outcome.ToString().ToLowerInvariant(),
            message = result.Message,
            payload = result.Payload
        };

        return JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public CommandResult MarshalResultFromNodeJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var outcome = ParseOutcome(root.GetProperty("outcome").GetString()!);
        var message = root.TryGetProperty("message", out var msgProp)
            ? msgProp.GetString()
            : null;

        object? payload = null;
        if (root.TryGetProperty("payload", out var payloadProp))
        {
            payload = JsonSerializer.Deserialize<object>(payloadProp.GetRawText());
        }

        return outcome switch
        {
            CommandOutcome.Success => CommandResult.Success(message, payload),
            CommandOutcome.Cancelled => CommandResult.Cancelled(message),
            CommandOutcome.Failed => CommandResult.Failed(message, payload),
            _ => throw new InvalidOperationException($"Unknown outcome: {outcome}")
        };
    }

    private CommandOutcome ParseOutcome(string outcome) => outcome switch
    {
        "success" => CommandOutcome.Success,
        "cancelled" => CommandOutcome.Cancelled,
        "failed" => CommandOutcome.Failed,
        _ => throw new ArgumentException($"Invalid outcome: {outcome}")
    };
}
```

**Illustrative Node.js Code:**

```javascript
// ⚠️ ILLUSTRATIVE EXAMPLE

/**
 * Marshal .NET CommandResult JSON to Node.js CommandResult
 */
function marshalResultFromDotNet(json) {
    const data = JSON.parse(json);

    switch (data.outcome) {
        case 'success':
            return CommandResult.success(data.message, data.payload);

        case 'cancelled':
            return CommandResult.cancelled(data.message);

        case 'failed':
            return CommandResult.failed(data.message, data.payload);

        default:
            throw new Error(`Unknown outcome: ${data.outcome}`);
    }
}

/**
 * Marshal Node.js CommandResult to JSON for .NET
 */
function marshalResultToDotNet(result) {
    return JSON.stringify({
        outcome: result.outcome,
        message: result.message,
        payload: result.payload
    });
}
```

---

## Exception Translation Patterns

### ⚠️ ILLUSTRATIVE PATTERN: Exception → Result Conversion

**Python Exception to Result:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE

def exception_to_result(func):
    """
    Decorator that converts exceptions to CommandResult.
    """
    def wrapper(*args, **kwargs):
        try:
            result = func(*args, **kwargs)
            # If function returns result, pass through
            if isinstance(result, CommandResult):
                return result
            # Otherwise wrap in success
            return CommandResult.success(payload=result)

        except asyncio.CancelledError:
            return CommandResult.cancelled("Operation was cancelled")

        except FileNotFoundError as ex:
            return CommandResult.failed(
                f"File not found: {ex.filename}",
                {"error_type": "FileNotFoundError", "filename": ex.filename}
            )

        except PermissionError as ex:
            return CommandResult.failed(
                f"Permission denied: {ex.filename}",
                {"error_type": "PermissionError", "filename": ex.filename}
            )

        except Exception as ex:
            return CommandResult.failed(
                f"Unexpected error: {str(ex)}",
                {
                    "error_type": type(ex).__name__,
                    "error_message": str(ex),
                    "traceback": traceback.format_exc()
                }
            )

    return wrapper


# Usage
@exception_to_result
def risky_operation(param):
    """Operation that might throw exceptions."""
    if not param:
        raise ValueError("Parameter cannot be empty")

    # Do work...
    return {"result": "success"}


# Returns CommandResult
result = risky_operation("test")
```

**Node.js Exception to Result:**

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE

function exceptionToResult<T>(
    func: (...args: any[]) => T | Promise<T>
): (...args: any[]) => Promise<CommandResult<T>> {
    return async (...args: any[]) => {
        try {
            const result = await func(...args);

            // If already a result, pass through
            if (result instanceof CommandResult) {
                return result as CommandResult<T>;
            }

            // Otherwise wrap in success
            return CommandResult.success<T>(undefined, result);

        } catch (error: any) {
            // Cancellation/Abort
            if (error.name === 'AbortError') {
                return CommandResult.cancelled('Operation was cancelled');
            }

            // File system errors
            if (error.code === 'ENOENT') {
                return CommandResult.failed(
                    `File not found: ${error.path}`,
                    { errorType: 'FileNotFound', path: error.path }
                );
            }

            if (error.code === 'EACCES') {
                return CommandResult.failed(
                    'Permission denied',
                    { errorType: 'PermissionDenied', path: error.path }
                );
            }

            // Generic error
            return CommandResult.failed(
                `Unexpected error: ${error.message}`,
                {
                    errorType: error.constructor.name,
                    errorMessage: error.message,
                    stack: error.stack
                }
            );
        }
    };
}

// Usage
const safeFileRead = exceptionToResult(async (path: string) => {
    const fs = require('fs').promises;
    return await fs.readFile(path, 'utf8');
});

// Returns CommandResult
const result = await safeFileRead('example.txt');
```

---

## Serialization Challenges

### Challenge 1: Complex Payload Serialization

**Problem:** .NET types might not serialize cleanly to Python/Node.js.

**⚠️ ILLUSTRATIVE SOLUTION: Serialization Layer**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class CrossRuntimeResultSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public string SerializeResult(CommandResult result)
    {
        var dto = new ResultDto
        {
            Outcome = result.Outcome.ToString().ToLowerInvariant(),
            Message = result.Message,
            Payload = SerializePayload(result.Payload)
        };

        return JsonSerializer.Serialize(dto, _options);
    }

    private object? SerializePayload(object? payload)
    {
        if (payload is null) return null;

        // Convert complex types to serializable format
        if (payload is Exception ex)
        {
            return new
            {
                ErrorType = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace?.Split('\n').Take(10).ToArray()
            };
        }

        if (payload is IDictionary<string, object?> dict)
        {
            // Already serializable
            return dict;
        }

        // For anonymous types and POCOs, serialize directly
        return payload;
    }

    private class ResultDto
    {
        public string Outcome { get; set; }
        public string? Message { get; set; }
        public object? Payload { get; set; }
    }
}
```

### Challenge 2: Stack Trace Preservation

**Problem:** Stack traces don't translate across language boundaries.

**⚠️ ILLUSTRATIVE SOLUTION: Structured Error Details**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public sealed record ErrorDetails(
    string ErrorType,
    string Message,
    string? StackSummary,
    Dictionary<string, string>? AdditionalData);

public static CommandResult CreateFailedFromException(Exception ex)
{
    var errorDetails = new ErrorDetails(
        ErrorType: ex.GetType().Name,
        Message: ex.Message,
        StackSummary: GetStackSummary(ex),
        AdditionalData: ExtractAdditionalData(ex)
    );

    return CommandResult.Failed(ex.Message, errorDetails);
}

private static string GetStackSummary(Exception ex)
{
    // Take first 5 frames only
    var frames = ex.StackTrace?.Split('\n')
        .Take(5)
        .Select(line => line.Trim())
        .ToArray();

    return frames != null ? string.Join(" → ", frames) : null;
}

private static Dictionary<string, string>? ExtractAdditionalData(Exception ex)
{
    var data = new Dictionary<string, string>();

    if (ex is FileNotFoundException fileEx)
    {
        data["FileName"] = fileEx.FileName ?? "unknown";
    }
    else if (ex is UnauthorizedAccessException)
    {
        data["AccessType"] = "Unauthorized";
    }

    return data.Any() ? data : null;
}
```

---

## Cross-Language Result Patterns

### ⚠️ ILLUSTRATIVE PATTERN: Unified Result Protocol

**JSON Schema for Cross-Language Results:**

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CommandResult",
  "type": "object",
  "required": ["outcome"],
  "properties": {
    "outcome": {
      "type": "string",
      "enum": ["success", "cancelled", "failed"]
    },
    "message": {
      "type": "string"
    },
    "payload": {
      "type": "object",
      "description": "Arbitrary structured data"
    }
  }
}
```

**Usage Across Runtimes:**

```csharp
// .NET
var result = CommandResult.Success("Done", new { Count = 42 });
var json = JsonSerializer.Serialize(result);
// Send to Python/Node.js
```

```python
# Python receives JSON
import json
result_dict = json.loads(json_from_dotnet)
result = CommandResult.from_dict(result_dict)
```

```javascript
// Node.js receives JSON
const data = JSON.parse(jsonFromDotNet);
const result = CommandResult.fromJSON(data);
```

---

## Challenges & Considerations

### Technical Challenges

**1. Type Mismatches**
- .NET: Strong typing
- Python: Duck typing
- Node.js: Dynamic (or TypeScript)
- **Solution:** JSON as common denominator, runtime validation

**2. Null vs None vs undefined**
- .NET: `null`
- Python: `None`
- Node.js: `null` and `undefined`
- **Solution:** Normalize to JSON null

**3. Enum Serialization**
- .NET: `CommandOutcome.Success`
- Python: `CommandOutcome.SUCCESS`
- Node.js: `CommandOutcome.Success`
- **Solution:** Use string literals ("success", "cancelled", "failed")

**4. Date/Time Serialization**
- Different formats across platforms
- **Solution:** ISO 8601 strings

**5. Binary Data**
- .NET: `byte[]`
- Python: `bytes`
- Node.js: `Buffer`
- **Solution:** Base64 encoding in JSON

---

## Related Documentation

- [Result Objects - VISORA Analysis](./visora-analysis.md) - Actual .NET implementation
- [Async Patterns - Meta-Platform](../async-patterns/meta-platform-illustrations.md)
- [Command Execution - VISORA Analysis](../command-execution/visora-analysis.md)

---

## Further Reading

### Serialization
- [JSON Schema](https://json-schema.org/)
- [MessagePack](https://msgpack.org/)
- [Protocol Buffers](https://developers.google.com/protocol-buffers)

### Error Handling Patterns
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Result Types in Functional Programming](https://adambennett.dev/2020/05/the-result-monad/)

---

**Remember:** These are ILLUSTRATIVE EXAMPLES to inspire exploration, not production-ready solutions. Cross-language result handling requires careful schema design, comprehensive testing, and clear documentation of serialization contracts.
