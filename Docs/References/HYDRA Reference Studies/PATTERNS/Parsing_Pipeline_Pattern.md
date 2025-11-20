# Parsing Pipeline Pattern - Multi-Stage State Machine Parsing

**Pattern Category:** Data Processing / Transformation
**Complexity:** High
**Reusability:** High - applicable to any structured text parsing in .NET

---

## Pattern Intent

Provide robust multi-stage parsing with:
- Sequential character-by-character state machines
- Balanced delimiter tracking (parentheses, braces)
- Context-aware splitting (respect string literals, nesting)
- Graceful error recovery (partial results, section isolation)
- Immutable outputs for safety

## Problem Being Solved

When parsing complex nested formats (VISOR protocol in HYDRA's case):
- Simple regex fails on nested structures
- String literals contain delimiters that shouldn't be matched
- Unbalanced brackets should be detected gracefully
- One malformed section shouldn't break entire parse
- Need to extract structured data from markdown-style fenced blocks

## HYDRA Implementation

### Three-Stage Pipeline

```
Stage 1: Block Extraction
   Raw Message → VBlockPreParser → List<VBlock(Prolog, Body)>

Stage 2: Structure Parsing
   List<VBlock> → Json1sParser → List<HydraEnvelope>

Stage 3: Orchestration
   VisorParsePipeline coordinates stages + error recovery
```

---

### Stage 1: VBlockPreParser - Fenced Block Extraction

**File:** `Platform/Foundations/Visor Foundation/Parsing/VBlockPreParser.cs` (291 lines)

**Purpose:** Extract VISOR protocol blocks from markdown-style fenced syntax

**Example Input:**
````
Here's some chat text.

```VISOR json1s
mcp({ "method": "tools/list" })
```

More chat text.
````

**Output:**
```csharp
VBlock(Prolog: "mcp(", Body: "{ \"method\": \"tools/list\" }")
```

#### Block Extraction with Regex (Lines 71-90)

```csharp
private static Regex FencedBlockRegex = new Regex(
    @"```\s*VISOR\s*[\r\n]+(.*?)```",
    RegexOptions.Singleline | RegexOptions.IgnoreCase
);

public static IReadOnlyList<string> ExtractVisorFencedBlocks(string message)
{
    var matches = FencedBlockRegex.Matches(message);
    return matches.Select(m => m.Groups[1].Value).ToList().AsReadOnly();
}
```

**Key Features:**
- Case-insensitive `VISOR` marker
- Handles both `\n` and `\r\n` line endings
- Non-greedy `(.*?)` captures multiple blocks
- Returns immutable list

#### V-Call Extraction State Machine (Lines 96-172)

**State Machine Pattern: Sequential Character Parser**

```csharp
public static IReadOnlyList<VBlock> ExtractVCalls(string body)
{
    int pos = 0;
    var results = new List<VBlock>();

    while (pos < body.Length)
    {
        // State 1: Skip Whitespace
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos >= body.Length) break;

        // State 2: Extract Prolog
        int prologEnd = FindPrologEnd(body, pos);
        if (prologEnd == -1) break;  // Invalid format → stop

        string prolog = body.Substring(pos, prologEnd - pos);

        // State 3: Validate Body Start (expect '{')
        pos = prologEnd;
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos >= body.Length || body[pos] != '{')
            break;  // No body → stop

        // State 4: Extract Balanced Body
        int bodyEnd = FindBalancedBraceEnd(body, pos);
        if (bodyEnd == -1) break;  // Unbalanced → stop

        string bodyContent = body.Substring(pos + 1, bodyEnd - pos - 2);  // Exclude { }

        results.Add(new VBlock(prolog, bodyContent));

        // State 5: Handle Terminator
        pos = bodyEnd;
        if (pos < body.Length && body[pos] == ';')
            pos++;  // Skip optional semicolon
    }

    return results.AsReadOnly();
}
```

**Error Recovery:** Break on invalid transitions, return partial results

#### Balanced Delimiter Tracker (Lines 178-241)

**State Machine Pattern: Depth Tracking with Context**

```csharp
private static int FindPrologEnd(string input, int start)
{
    int pos = start;

    // Expect identifier: [letter|_][alphanumeric|_]*
    if (!char.IsLetter(input[pos]) && input[pos] != '_')
        return -1;

    while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_'))
        pos++;

    // Expect '('
    if (pos >= input.Length || input[pos] != '(')
        return -1;

    pos++;  // Skip '('

    // Track balanced parentheses with string awareness
    int parenDepth = 1;
    bool inString = false;
    char prevChar = '\0';

    while (pos < input.Length && parenDepth > 0)
    {
        char c = input[pos];

        // Track string context
        if (c == '"' && prevChar != '\\')
        {
            inString = !inString;
        }

        // Track depth (only outside strings)
        if (!inString)
        {
            if (c == '(') parenDepth++;
            if (c == ')') parenDepth--;
        }

        prevChar = c;
        pos++;
    }

    return parenDepth == 0 ? pos : -1;  // Return position after closing ')' or -1
}
```

**Key Features:**
- **Depth tracking:** `parenDepth` counter
- **String awareness:** `inString` flag prevents matching `(` or `)` inside strings
- **Escape handling:** `prevChar != '\\'` detects escaped quotes
- **Error signaling:** Returns `-1` on unbalanced delimiters

**Similar pattern for braces:** `FindBalancedBraceEnd()` (Lines 247-289) uses `braceDepth` instead of `parenDepth`

---

### Stage 2: Json1sParser - Structure Parsing

**File:** `Platform/Foundations/Visor Foundation/Parsing/Json1sParser.cs` (388 lines)

**Purpose:** Parse VISOR V-call body into structured HydraEnvelope

**Example Input:**
```
Prolog: "mcp("
Body: ".headers priority=10; .meta schema=\"foo\"; { \"method\": \"tools/list\" }"
```

**Output:**
```csharp
HydraEnvelope {
    Id = "generated-uuid",
    Type = call,
    Qos = HydraQos { Priority = 10 },
    Schema = "foo",
    Payload = { "method": "tools/list" }
}
```

#### Parse Entry Point (Lines 26-71)

```csharp
public List<HydraEnvelope> Parse(string prolog, string body)
{
    // Validate prolog
    if (!prolog.StartsWith("mcp(", StringComparison.OrdinalIgnoreCase))
    {
        Logger.LogWarning($"[VISOR-PARSE] Unsupported prolog: {prolog}");
        return new List<HydraEnvelope>();  // Return empty, don't crash
    }

    // Split body into multiple V-calls (semicolon-separated)
    var vCallBodies = SplitVCalls(body);

    var envelopes = new List<HydraEnvelope>();

    foreach (var vCallBody in vCallBodies)
    {
        try
        {
            var envelope = ParseSingleVCall(vCallBody);
            if (envelope != null)
                envelopes.Add(envelope);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VISOR-PARSE] Failed to parse V-call");
            // Skip bad V-call, continue with others
        }
    }

    return envelopes;
}
```

**Error Recovery:** Individual V-call failures don't stop processing

#### Depth-Aware V-Call Splitting (Lines 73-150)

**State Machine Pattern: Context-Aware Splitting**

```csharp
private List<string> SplitVCalls(string body)
{
    var calls = new List<string>();
    int braceDepth = 0;
    bool inString = false;
    bool startedWithDot = false;
    int callStart = 0;

    for (int i = 0; i < body.Length; i++)
    {
        char c = body[i];
        char prevChar = i > 0 ? body[i - 1] : '\0';

        // Track string context
        if (c == '"' && prevChar != '\\')
        {
            inString = !inString;
        }

        // Track brace depth (only outside strings)
        if (!inString)
        {
            if (c == '{') braceDepth++;
            if (c == '}') braceDepth--;

            // Split on semicolon only at depth 0
            if (c == ';' && braceDepth == 0)
            {
                var call = body.Substring(callStart, i - callStart).Trim();
                if (!string.IsNullOrWhiteSpace(call))
                    calls.Add(call);

                callStart = i + 1;
            }
        }
    }

    // Add final call (no trailing semicolon)
    var lastCall = body.Substring(callStart).Trim();
    if (!string.IsNullOrWhiteSpace(lastCall))
        calls.Add(lastCall);

    return calls;
}
```

**Why Not Simple `body.Split(';')`?**
- Semicolons inside strings shouldn't split: `{ "text": "a;b" }`
- Semicolons inside nested objects shouldn't split: `{ "obj": { "x": 1; "y": 2 } }`

#### Dotted Section Extraction (Lines 260-386)

**State Machine Pattern: Sequential Section Parser**

**Parses syntax like:**
```
.headers priority=10; .meta schema="foo"; { "payload": "here" }
```

**Into:**
```csharp
Sections: {
    "headers": "priority=10",
    "meta": "schema=\"foo\""
}
Payload: "{ \"payload\": \"here\" }"
```

**Implementation:**
```csharp
private (Dictionary<string, string>, string) ExtractDottedSectionsAndPayload(string body)
{
    var sections = new Dictionary<string, string>();
    int pos = 0;

    while (pos < body.Length)
    {
        // State 1: Skip Whitespace
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos >= body.Length) break;

        // State 2: Check for Dot (section marker)
        if (body[pos] != '.')
            break;  // Rest is payload

        pos++;  // Skip '.'

        // State 3: Extract Section Name
        int nameStart = pos;
        while (pos < body.Length && (char.IsLetterOrDigit(body[pos]) || body[pos] == '_'))
            pos++;

        string sectionName = body.Substring(nameStart, pos - nameStart);

        // State 4: Expect '='
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos >= body.Length || body[pos] != '=')
            break;  // Malformed

        pos++;  // Skip '='

        // State 5: Extract JSON Value (balanced braces)
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos >= body.Length || body[pos] != '{')
            break;

        int valueEnd = FindBalancedBraceEnd(body, pos);
        if (valueEnd == -1)
            break;

        string jsonValue = body.Substring(pos, valueEnd - pos);
        sections[sectionName] = jsonValue;

        pos = valueEnd;

        // State 6: Expect Terminator (semicolon)
        while (pos < body.Length && char.IsWhiteSpace(body[pos]))
            pos++;

        if (pos < body.Length && body[pos] == ';')
            pos++;  // Skip semicolon
    }

    // Remaining body is payload
    string payload = body.Substring(pos).Trim();

    return (sections, payload);
}
```

**Error Recovery:** Break on invalid transitions, return partial sections

#### Single V-Call Parser (Lines 152-253)

**Orchestrates substeps:**

```csharp
private HydraEnvelope? ParseSingleVCall(string vCallBody)
{
    // 1. Extract dotted sections (.headers, .meta) + payload
    var (sections, payloadStr) = ExtractDottedSectionsAndPayload(vCallBody);

    // 2. Parse JSON payload
    JsonElement payload = JsonDocument.Parse(payloadStr, JsonOptions).RootElement;

    // 3. Extract metadata from sections (with error isolation)
    int? priority = null;
    try
    {
        if (sections.TryGetValue("headers", out var headersJson))
        {
            var headers = JsonDocument.Parse(headersJson).RootElement;
            if (headers.TryGetProperty("priority", out var p))
                priority = p.GetInt32();
        }
    }
    catch (Exception ex)
    {
        Logger.LogWarning($"[VISOR-PARSE] Failed to parse .headers section: {ex.Message}");
        // Continue without priority
    }

    string? schema = null;
    string? traceparent = null;
    string? contentType = null;
    try
    {
        if (sections.TryGetValue("meta", out var metaJson))
        {
            var meta = JsonDocument.Parse(metaJson).RootElement;
            if (meta.TryGetProperty("schema", out var s))
                schema = s.GetString();
            if (meta.TryGetProperty("traceparent", out var t))
                traceparent = t.GetString();
            if (meta.TryGetProperty("content_type", out var ct))
                contentType = ct.GetString();
        }
    }
    catch (Exception ex)
    {
        Logger.LogWarning($"[VISOR-PARSE] Failed to parse .meta section: {ex.Message}");
        // Continue without metadata
    }

    // 4. Create envelope
    return new HydraEnvelope(
        Id: Guid.NewGuid().ToString(),
        Type: HydraEnvelopeType.call,
        Ts: DateTimeOffset.UtcNow,
        Src: "",  // Filled by Node layer
        Dst: "",  // Filled by Node layer
        Op: "mcp",
        Subject: null,
        Corr: null,
        Trace: traceparent != null ? new HydraTrace(traceparent) : null,
        Auth: null,
        Caps: null,
        Qos: priority != null ? new HydraQos(priority, null, null, null) : null,
        Flow: null,
        Schema: schema,
        ContentType: contentType,
        Payload: payload,
        Attachments: null,
        Error: null
    );
}
```

**Error Recovery Strategy: Section Isolation**
- `.headers` parse failure → continue without QoS
- `.meta` parse failure → continue without trace/schema
- Malformed section doesn't block envelope creation

---

### Stage 3: VisorParsePipeline - Orchestration

**File:** `Platform/Foundations/Visor Foundation/Parsing/VisorParsePipeline.cs` (84 lines)

**Purpose:** Thin façade coordinating VBlockPreParser → Json1sParser

```csharp
public static class VisorParsePipeline
{
    public static List<HydraEnvelope> ExtractAndParse(string message)
    {
        try
        {
            // Stage 1: Extract VISOR fenced blocks
            var vBlocks = VBlockPreParser.Extract(message);

            if (vBlocks.Count == 0)
            {
                Logger.LogDebug("[VISOR-PARSE] No VISOR blocks found");
                return new List<HydraEnvelope>();
            }

            Logger.LogInformation($"[VISOR-PARSE] ✓ Extracted {vBlocks.Count} VISOR block(s)");

            // Stage 2: Parse each block
            var allEnvelopes = new List<HydraEnvelope>();

            foreach (var vBlock in vBlocks)
            {
                try
                {
                    var envelopes = Json1sParser.Parse(vBlock.Prolog, vBlock.Body);
                    allEnvelopes.AddRange(envelopes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"[VISOR-PARSE] ✗ Failed to parse V-block");
                    // Continue with other blocks
                }
            }

            Logger.LogInformation($"[VISOR-PARSE] ✓ Parsed {allEnvelopes.Count} envelope(s)");

            return allEnvelopes;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VISOR-PARSE] ✗ Pipeline catastrophic failure");
            return new List<HydraEnvelope>();  // Return empty, don't crash
        }
    }
}
```

**Error Recovery: Block-Level Isolation**
- One bad block doesn't stop processing of others
- Catastrophic failure returns empty list (graceful degradation)

---

## Pattern Structure

### State Machine Patterns

#### Pattern 1: Sequential Character Parser
```
Position pointer: pos (0-based index)
Loop: while (pos < input.Length)
  - Check current character
  - Advance position
  - Break on invalid transition
```

**Used in:** `ExtractVCalls()`, `ExtractDottedSectionsAndPayload()`

#### Pattern 2: Depth Tracking with Context
```
Depth counter: depth (starts at 1 or 0)
Context flag: inString (boolean)
Loop: while (depth > 0)
  - Track string context (handle escaped quotes)
  - Increment depth on open delimiter (outside strings)
  - Decrement depth on close delimiter (outside strings)
  - Return position when depth == 0
  - Return -1 if unbalanced
```

**Used in:** `FindPrologEnd()`, `FindBalancedBraceEnd()`, `SplitVCalls()`

#### Pattern 3: Multi-Stage Pipeline
```
Stage 1: Coarse extraction (fenced blocks)
Stage 2: Medium extraction (V-calls from blocks)
Stage 3: Fine extraction (JSON from payload)
Each stage produces structured output for next stage
```

**Used in:** Full VisorParsePipeline flow

---

## Key Design Decisions

### 1. Immutable Outputs
**Decision:** All parsing methods return `IReadOnlyList<T>` or immutable records

**Rationale:**
- Prevents accidental modification of parse results
- Safer for concurrent access
- Clear ownership (callers can't mutate)

**Example:**
```csharp
public static IReadOnlyList<VBlock> ExtractVCalls(string body)
{
    var results = new List<VBlock>();
    // ... parsing logic
    return results.AsReadOnly();  // Immutable wrapper
}
```

### 2. Error Recovery: Partial Results
**Decision:** Return partial results on parse failures

**Rationale:**
- One malformed block shouldn't break entire conversation
- Partial data better than no data
- User can retry specific blocks

**Example:**
```csharp
try {
    // Parse V-call
} catch (Exception ex) {
    Logger.LogError(ex, "Parse failed");
    // Continue with next V-call, return what we have so far
}
```

### 3. Section Isolation for Metadata
**Decision:** Wrap each section parse in separate try-catch

**Rationale:**
- Malformed `.headers` shouldn't block envelope creation
- Core payload more important than optional metadata
- Progressive parsing (extract what you can)

**Example:**
```csharp
try {
    priority = ParseHeaders(sections["headers"]);
} catch {
    // Ignore headers, continue without priority
}

try {
    schema = ParseMeta(sections["meta"]);
} catch {
    // Ignore meta, continue without schema
}

return CreateEnvelope(priority: priority, schema: schema);  // May be null
```

### 4. String-Aware Delimiter Matching
**Decision:** Track `inString` flag during delimiter matching

**Rationale:**
- Delimiters inside strings shouldn't be matched: `{ "text": "a)b" }`
- Escaped quotes handled: `{ "text": "a\"b" }`
- Prevents false positives

**Implementation:**
```csharp
if (c == '"' && prevChar != '\\')
{
    inString = !inString;
}

if (!inString)  // Only match delimiters outside strings
{
    if (c == '(') depth++;
    if (c == ')') depth--;
}
```

### 5. Lenient JSON Parsing
**Decision:** Use `JsonDocumentOptions` with trailing comma support

**Rationale:**
- Human-written JSON often has trailing commas
- Comments in JSON useful for debugging
- Parsing shouldn't be overly strict

**Implementation:**
```csharp
private static readonly JsonDocumentOptions JsonOptions = new()
{
    AllowTrailingCommas = true,
    CommentHandling = JsonCommentHandling.Skip
};
```

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define Immutable Parse Result

```csharp
public record ParsedBlock(string Header, string Body);
```

### Step 2: Implement Balanced Delimiter Tracker

```csharp
public class DelimiterMatcher
{
    public static int FindBalancedEnd(string input, int start, char open, char close)
    {
        int depth = 1;  // Start at 1 (we've already seen opening delimiter)
        bool inString = false;
        char prevChar = '\0';

        for (int pos = start + 1; pos < input.Length; pos++)
        {
            char c = input[pos];

            // Track string context
            if (c == '"' && prevChar != '\\')
            {
                inString = !inString;
            }

            // Track depth (only outside strings)
            if (!inString)
            {
                if (c == open) depth++;
                if (c == close) depth--;

                if (depth == 0)
                    return pos + 1;  // Return position after closing delimiter
            }

            prevChar = c;
        }

        return -1;  // Unbalanced
    }
}
```

### Step 3: Implement Sequential Character Parser

```csharp
public class SequentialParser
{
    public static List<ParsedBlock> Parse(string input)
    {
        var results = new List<ParsedBlock>();
        int pos = 0;

        while (pos < input.Length)
        {
            // Step 1: Skip whitespace
            while (pos < input.Length && char.IsWhiteSpace(input[pos]))
                pos++;

            if (pos >= input.Length) break;

            // Step 2: Extract header
            int headerEnd = input.IndexOf(':', pos);
            if (headerEnd == -1) break;

            string header = input.Substring(pos, headerEnd - pos).Trim();
            pos = headerEnd + 1;

            // Step 3: Extract body (balanced braces)
            while (pos < input.Length && char.IsWhiteSpace(input[pos]))
                pos++;

            if (pos >= input.Length || input[pos] != '{')
                break;

            int bodyEnd = DelimiterMatcher.FindBalancedEnd(input, pos, '{', '}');
            if (bodyEnd == -1) break;

            string body = input.Substring(pos, bodyEnd - pos);

            results.Add(new ParsedBlock(header, body));

            pos = bodyEnd;
        }

        return results;
    }
}
```

### Step 4: Implement Multi-Stage Pipeline

```csharp
public class ParsingPipeline
{
    public static List<Envelope> ExtractAndParse(string message)
    {
        try
        {
            // Stage 1: Extract fenced blocks
            var blocks = ExtractFencedBlocks(message);

            // Stage 2: Parse each block
            var envelopes = new List<Envelope>();

            foreach (var block in blocks)
            {
                try
                {
                    var envelope = ParseBlock(block);
                    if (envelope != null)
                        envelopes.Add(envelope);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to parse block: {ex.Message}");
                    // Continue with other blocks
                }
            }

            return envelopes;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Pipeline failure: {ex.Message}");
            return new List<Envelope>();  // Graceful degradation
        }
    }

    private static List<ParsedBlock> ExtractFencedBlocks(string message)
    {
        var regex = new Regex(@"```(\w+)\s+(.*?)```", RegexOptions.Singleline);
        var matches = regex.Matches(message);

        return matches
            .Select(m => new ParsedBlock(m.Groups[1].Value, m.Groups[2].Value))
            .ToList();
    }

    private static Envelope? ParseBlock(ParsedBlock block)
    {
        // Parse block.Body using SequentialParser
        // Extract metadata with error isolation
        // Return structured envelope
    }
}
```

### Step 5: Add Error Isolation

```csharp
public class RobustParser
{
    public Envelope Parse(string input)
    {
        // Extract core payload (required)
        var payload = ExtractPayload(input);  // Throws on failure

        // Extract optional metadata (isolated errors)
        string? author = null;
        try
        {
            author = ExtractAuthor(input);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to extract author: {ex.Message}");
            // Continue without author
        }

        DateTimeOffset? timestamp = null;
        try
        {
            timestamp = ExtractTimestamp(input);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to extract timestamp: {ex.Message}");
            // Continue without timestamp
        }

        return new Envelope(payload, author, timestamp);
    }
}
```

---

## Tradeoffs & Constraints

### Advantages
✅ Robust parsing with graceful degradation
✅ Handles nested structures correctly
✅ String-aware (no false positives)
✅ Section isolation (partial failures ok)
✅ Immutable outputs (safe concurrency)

### Limitations
⚠️ Character-by-character parsing slower than simple regex
⚠️ Depth tracking adds complexity
⚠️ Error recovery may hide bugs (silent failures)
⚠️ Extensive logging needed for debugging

### When NOT to Use This Pattern
❌ Simple flat formats (CSV, simple JSON) - use built-in parsers
❌ Performance-critical parsing (consider parser generators like ANTLR)
❌ Well-defined grammar (use parser combinators or expression trees)
❌ Schema validation required (use JSON Schema validators)

---

## Related Patterns

- **Chain of Responsibility:** Pipeline stages process in sequence
- **State Machine:** Sequential character parsing with state tracking
- **Composite:** Nested structures (V-calls contain sections contain JSON)
- **Facade:** VisorParsePipeline hides complexity of multi-stage parsing

---

## Gen2 Evolution Notes

**Current (Gen1):** Hand-written state machines, character-by-character parsing

**Future (Gen2):**
- Parser generators (ANTLR, FParsec) for complex grammars
- Source-generated parsers for performance
- Streaming parsers for large messages
- Binary formats (protobuf) where applicable

**Migration Strategy:**
- Keep immutable output types (`VBlock`, `HydraEnvelope`)
- Replace implementation with generated parsers
- Maintain error recovery semantics

---

**Last Updated:** 2025-11-10
**Pattern Stability:** Medium - Implementation may change, but error recovery principles stable
**Code References:** VBlockPreParser.cs:1-291, Json1sParser.cs:1-388, VisorParsePipeline.cs:1-84
