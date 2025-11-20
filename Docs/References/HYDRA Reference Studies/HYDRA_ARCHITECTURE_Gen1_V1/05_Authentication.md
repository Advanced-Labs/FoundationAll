# 5. Authentication & Authorization

## 5.1 Three-Layer Auth Model (Planned)

### Layer 1: Human Identity (OpenIddict)
**Purpose:** User authentication, session management
**Technology:** OpenIddict + ASP.NET Core Identity
**Current Status:** ✅ Operational

### Layer 2: Workload Identity (SPIFFE/SPIRE)
**Purpose:** Service-to-service authentication, mTLS
**Technology:** SPIFFE/SPIRE with X.509 SVIDs
**Current Status:** 🚧 Planned (dev CA as fallback)

### Layer 3: Capability Tokens (Macaroons)
**Purpose:** Fine-grained authorization, delegation, attenuation
**Technology:** Macaroons with caveats
**Current Status:** 🚧 Planned

## 5.2 Current State (OpenIddict Only)

### HydraSecurityService

**File:** `/src/Hydra/Hydra.Server/Platform/Core Services/HydraSecurityService.cs` (169 lines + partials)
**Status:** ✅ Working
**Boot Priority:** 100 (starts last)
**Auto-Start:** Yes

**HTTP Endpoints:**
- Authorization Server: `http://localhost:5081` (HTTP)
- Authorization Server: `https://localhost:5444` (HTTPS, dev cert)

**OAuth Endpoints:**
- `/connect/authorize` - Authorization endpoint
- `/connect/token` - Token endpoint
- `/account/login` - Login page
- `/.well-known/openid-configuration` - OIDC discovery
- `/.well-known/oauth-authorization-server` - OAuth AS metadata
- `/.well-known/oauth-protected-resource` - Protected Resource Metadata

### OpenIddict Configuration

**File:** `/src/Hydra/Hydra.Server/Platform/Domains/Security/Authentication/HydraSecurityService.ConfigureServices.cs` (132 lines)

**Features Enabled:**
- Authorization code flow with PKCE required (Lines 54-56)
- Refresh token flow (Line 55)
- Anonymous clients accepted (Line 57)
- **Scope validation disabled** (Lines 58-61)
- Development encryption/signing certificates (Lines 64-65)

**Custom RavenDB Stores (Lines 20-43):**
```csharp
.AddCore(options => {
    options.UseRavenDb()
        .UseApplicationStore<OpenIddictRavenDbApplicationStore>()
        .UseAuthorizationStore<OpenIddictRavenDbAuthorizationStore>()
        .UseScopeStore<OpenIddictRavenDbScopeStore>()
        .UseTokenStore<OpenIddictRavenDbTokenStore>();
})
```

### Scope Stripping Implementation

**Primary: Middleware (Lines 34-150 of OAuth.cs):**
```csharp
app.Use(async (context, next) => {
    if (context.Request.Method == "POST" && context.Request.Path == "/connect/token") {
        // Read request body
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        
        // Remove scope parameter from form-urlencoded or JSON
        var filteredBody = RemoveScopeParameter(body, context.Request.ContentType);
        
        // Replace request body
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(filteredBody));
        context.Request.Body.Seek(0, SeekOrigin.Begin);
        context.Request.ContentLength = filteredBody.Length;
    }
    
    await next();
});
```

**Secondary: OpenIddict Event Handler (Lines 75-89 of ConfigureServices.cs):**
```csharp
.AddServer(options => {
    options.AddEventHandler<ExtractTokenRequestContext>(builder => {
        builder.UseInlineHandler(context => {
            if (context.Request.GrantType == GrantTypes.AuthorizationCode) {
                context.Request.Scope = null; // Strip scope
                _logger.LogDebug("[OAuth] Stripped scope from authorization code token request");
            }
            return default;
        });
    });
})
```

### Audience Fallback Logic

**Default Resource Resolution (Lines 538-542 of OAuth.cs):**
```csharp
private async Task<string?> ResolveDefaultResource()
{
    var resources = await _resourceRegistry.ListAsync();
    return resources.FirstOrDefault()?.Url ?? "http://localhost:5080/mcp";
}
```

**Used during authorization (Line 248):**
```csharp
var resource = requestResource ?? await ResolveDefaultResource();
```

**MCP Gateway Validation (HydraMcpSdkHost.cs Lines 129-139):**
```csharp
.AddValidation(options => {
    options.SetIssuer("http://localhost:5081/");
    options.AddAudiences("http://localhost:5080/mcp", "http://127.0.0.1:5080/mcp");
    options.UseSystemNetHttp();
    options.UseAspNetCore();
})
```

### Test Credentials

**Hard-coded for development (Lines 29-30 of OAuth.cs):**
```csharp
private const string TestUsername = "hydra";
private const string TestPassword = "test123";
```

## 5.3 OAuth Invariants (MUST NOT BREAK)

### 1. Single Scope: `hydra.mcp` Only

**Enforcement:** Scope stripping middleware removes all scope parameters
**Reason:** Prevents scope proliferation, simplifies token validation
**Impact:** All tokens have implicit access to all resources (dev mode)

### 2. Token-Exchange Scope Stripping

**Enforcement:** Both middleware and OpenIddict event handler
**Behavior:** Authorization code token requests have scope removed
**Reason:** Workaround for OpenIddict validation issues

### 3. Audience Fallback for MCP Tokens

**Enforcement:** `ResolveDefaultResource()` returns MCP URL when no aud specified
**Behavior:** Tokens without explicit audience get `http://localhost:5080/mcp`
**Reason:** Ensures MCP gateway can always validate tokens

## 5.4 Session Management

### HydraConnection

**File:** `/src/Hydra/Hydra.Server/Platform/Domains/Networking/Connections/HydraConnection.cs` (58 lines)

**Properties:**
- `ConnectionId` - Unique GUID
- `AccountId` - User account (null for guests)
- `ActiveSessionId` - Current bound session
- `SessionIds` - Historical session set
- `CreatedUtc`, `LastSeenUtc`, `UpdatedUtc` - Timestamps

**Methods:**
- `Touch()` - Update last seen time
- `AttachToSession(sessionId)` - Bind connection to session

### HydraSession

**Runtime Wrapper:** `/src/Hydra/Hydra.Server/Platform/Domains/Networking/Sessions/HydraSession.cs` (95 lines)
**Persistent Model:** `/src/Hydra/Hydra.Server/Platform/Domains/Networking/Sessions/Session.cs` (107 lines)

**HydraSession Properties:**
- `Session` - Underlying persistent model
- `CancellationToken` - Session-scoped cancellation
- Session context storage (thread-safe dictionary)

**Session (Persistent) Properties:**
- `SessionId`, `Username`, `ProjectName`, `ClientName`, `AgentName`
- `CreatedUtc`, `LastActivityUtc`, `Status`
- `PropertiesJson` - Extensible key-value storage

## 5.5 Storage Integration

### Account Model

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Storage Foundation/Account.cs` (11 lines)

```csharp
public class Account
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string DisplayName { get; set; }
    public string PasswordHash { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
```

### RavenDB Security Store

**Driver:** `/src/Hydra/Hydra.Server/Platform/Foundations/Storage Foundation/RavenHydraSecurityStoreDriver.cs` (181 lines)

**Methods:**
- `FindByUserNameAsync()` - Query accounts by username
- `CreateAccountAsync()` - Store new account
- `SaveConnectionAsync()`, `FindConnectionByIdAsync()`, `FindConnectionByAccountIdAsync()`
- `CreateSessionAsync()`, `UpdateSessionAsync()`, `FindSessionByIdAsync()`, `ListSessionsAsync()`

**Indexes:**
- `Accounts_ByUserName` - Account lookups
- `HydraSessions_ById` - Session queries
- `HydraConnections_ByConnectionId` - Connection ID lookups
- `HydraConnections_ByAccountId` - Account + LastSeenUtc composite

## 5.6 Future: SPIFFE/SPIRE (Planned)

### Design Goals
- Automatic workload identity provisioning
- mTLS between all HYDRA services
- X.509 SVID rotation every 1 hour
- Service mesh integration ready

### Integration Points
- Replace dev certificates with SPIFFE SVIDs
- Add SPIFFE middleware to all gateways
- Verify peer SVIDs in service calls
- Fallback to dev CA for local development

## 5.7 Future: Macaroons (Planned)

### Design Goals
- Capability-based authorization
- Delegation without server round-trips
- Attenuation (reduce permissions)
- Time-bound, resource-bound caveats

### Example Use Cases
- Grant temporary MCP tool access
- Delegate subset of permissions to AI agent
- Revoke capabilities without token revocation
- Audit trail of delegations

### Integration Points
- Embed macaroons in `HydraAuth.Proof`
- Verify caveats in ServiceRouter
- Add caveat validation middleware
- Store discharge macaroons in RavenDB
