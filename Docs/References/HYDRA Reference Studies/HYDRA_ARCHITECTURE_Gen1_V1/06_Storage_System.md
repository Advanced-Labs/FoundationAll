# 6. Storage System (RavenDB)

## 6.1 HydraStoreSystemV1

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Storage Foundation/HydraStoreSystemV1.cs` (92 lines)
**Status:** ✅ Working
**Boot Priority:** 90 (starts early)
**Auto-Start:** Yes

### IHydraStoreSystem Interface

```csharp
public interface IHydraStoreSystem
{
    IHydraSecurityStore Security { get; }
    // Future: IHydraKVStore KV { get; }
    // Future: IHydraEventStore Events { get; }
}
```

### Self-Provisioning

**EnsureSetupAsync (Lines 34-45):**
```csharp
public async Task StartHostAsync(CancellationToken cancellationToken)
{
    Logger.LogInformation("[HydraStoreSystemV1] Starting storage system...", "Storage");
    
    var driver = _serviceProvider.GetRequiredService<IHydraStoreDriver>();
    await driver.EnsureSetupAsync();
    
    Logger.LogInformation("[HydraStoreSystemV1] Storage system ready", "Storage");
}
```

**RavenDB Setup (RavenHydraSecurityStoreDriver.cs Lines 24-50):**
1. Create database if not exists
2. Create indexes: `Accounts_ByUserName`, `HydraSessions_ById`, `HydraConnections_ByConnectionId`, `HydraConnections_ByAccountId`
3. Wait for index compilation

### Registered Stores

**Current:** Security store only
**Future:** KV store, Event store

## 6.2 OpenIddict Integration

### RavenDB-Backed OpenIddict Stores

**Configuration (HydraSecurityService.ConfigureServices.cs Lines 20-43):**
```csharp
services.AddOpenIddict()
    .AddCore(options => {
        options.UseRavenDb()
            .UseApplicationStore<OpenIddictRavenDbApplicationStore>()
            .UseAuthorizationStore<OpenIddictRavenDbAuthorizationStore>()
            .UseScopeStore<OpenIddictRavenDbScopeStore>()
            .UseTokenStore<OpenIddictRavenDbTokenStore>();
    });
```

### Stored Entities

| Entity | Collection | Purpose |
|--------|-----------|---------|
| OpenIddictApplication | Applications | OAuth clients |
| OpenIddictAuthorization | Authorizations | Authorization grants |
| OpenIddictScope | Scopes | OAuth scopes |
| OpenIddictToken | Tokens | Access/refresh tokens |
| Account | Accounts | User accounts |
| HydraConnection | HydraConnections | Gateway connections |
| Session | HydraSessions | User sessions |

## 6.3 RavenDB Configuration

**Document Store Setup (HydraCompositionRegistry.cs Lines 22-38):**
```csharp
For<IDocumentStore>().Use(ctx =>
{
    var config = ctx.GetInstance<IConfiguration>();
    var urls = config.GetSection("RavenDB:Urls").Get<string[]>() ?? new[] { "http://localhost:8080" };
    var database = config.GetValue<string>("RavenDB:Database") ?? "HydraStore";

    var store = new DocumentStore
    {
        Urls = urls,
        Database = database
    };

    store.Initialize();
    return store;
}).Singleton();
```

**Default Configuration:**
- URLs: `http://localhost:8080`
- Database: `HydraStore`
- Can override via `appsettings.json`

**Scoped Session (Lines 41-42):**
```csharp
For<IAsyncDocumentSession>().Use(ctx =>
    ctx.GetInstance<IDocumentStore>().OpenAsyncSession()
).Scoped();
```

## 6.4 Security Store Driver

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Storage Foundation/RavenHydraSecurityStoreDriver.cs` (181 lines)

### Account Operations

**FindByUserNameAsync (Lines 52-58):**
```csharp
public async Task<Account?> FindByUserNameAsync(string userName)
{
    using var session = _documentStore.OpenAsyncSession();
    return await session.Query<Account, Accounts_ByUserName>()
        .FirstOrDefaultAsync(a => a.UserName == userName);
}
```

**CreateAccountAsync (Lines 60-65):**
```csharp
public async Task CreateAccountAsync(Account account)
{
    using var session = _documentStore.OpenAsyncSession();
    await session.StoreAsync(account);
    await session.SaveChangesAsync();
}
```

### Connection Operations

**SaveConnectionAsync (Lines 67-73):**
```csharp
public async Task SaveConnectionAsync(HydraConnection connection)
{
    using var session = _documentStore.OpenAsyncSession();
    await session.StoreAsync(connection, connection.ConnectionId);
    await session.SaveChangesAsync();
}
```

**FindConnectionByIdAsync (Lines 75-88):**
```csharp
public async Task<HydraConnection?> FindConnectionByIdAsync(string connectionId)
{
    using var session = _documentStore.OpenAsyncSession();
    return await session.Query<HydraConnection, HydraConnections_ByConnectionId>()
        .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
}
```

**FindConnectionByAccountIdAsync (Lines 90-104):**
```csharp
public async Task<List<HydraConnection>> FindConnectionByAccountIdAsync(string accountId)
{
    using var session = _documentStore.OpenAsyncSession();
    return await session.Query<HydraConnection, HydraConnections_ByAccountId>()
        .Where(c => c.AccountId == accountId)
        .OrderByDescending(c => c.LastSeenUtc)
        .ToListAsync();
}
```

### Session Operations

**CreateSessionAsync (Lines 114-120):**
```csharp
public async Task CreateSessionAsync(Session session)
{
    using var session = _documentStore.OpenAsyncSession();
    await session.StoreAsync(session, session.SessionId);
    await session.SaveChangesAsync();
}
```

**FindSessionByIdAsync (Lines 106-112):**
```csharp
public async Task<Session?> FindSessionByIdAsync(string sessionId)
{
    using var session = _documentStore.OpenAsyncSession();
    return await session.Query<Session, HydraSessions_ById>()
        .FirstOrDefaultAsync(s => s.SessionId == sessionId);
}
```

## 6.5 RavenDB Indexes

### Accounts_ByUserName (Lines 143-150)
```csharp
public class Accounts_ByUserName : AbstractIndexCreationTask<Account>
{
    public Accounts_ByUserName()
    {
        Map = accounts => from account in accounts
                          select new { account.UserName };
    }
}
```

### HydraSessions_ById (Lines 153-160)
```csharp
public class HydraSessions_ById : AbstractIndexCreationTask<Session>
{
    public HydraSessions_ById()
    {
        Map = sessions => from session in sessions
                          select new { session.SessionId };
    }
}
```

### HydraConnections_ByConnectionId (Lines 163-170)
```csharp
public class HydraConnections_ByConnectionId : AbstractIndexCreationTask<HydraConnection>
{
    public HydraConnections_ByConnectionId()
    {
        Map = connections => from connection in connections
                              select new { connection.ConnectionId };
    }
}
```

### HydraConnections_ByAccountId (Lines 173-180)
```csharp
public class HydraConnections_ByAccountId : AbstractIndexCreationTask<HydraConnection>
{
    public HydraConnections_ByAccountId()
    {
        Map = connections => from connection in connections
                              select new {
                                  connection.AccountId,
                                  connection.LastSeenUtc
                              };
    }
}
```

## 6.6 Future Stores (Planned)

### IHydraKVStore
**Purpose:** Key-value storage for service state
**Use Cases:**
- Service configuration
- Feature flags
- User preferences
- Temporary caching

### IHydraEventStore
**Purpose:** Event sourcing (Gen2)
**Technology:** EventStoreDB or RavenDB streams
**Use Cases:**
- Envelope history
- State reconstruction
- Audit trail
- Event replay

## 6.7 Data Model

### Document Structure

**Accounts:**
```json
{
  "Id": "accounts/1-A",
  "UserName": "hydra",
  "DisplayName": "Hydra Test User",
  "PasswordHash": "$2a$11$...",
  "CreatedUtc": "2025-11-10T00:00:00Z"
}
```

**HydraConnections:**
```json
{
  "ConnectionId": "conn-a1b2c3d4",
  "AccountId": "accounts/1-A",
  "ActiveSessionId": "sess-xyz",
  "SessionIds": ["sess-xyz", "sess-abc"],
  "CreatedUtc": "2025-11-10T12:00:00Z",
  "LastSeenUtc": "2025-11-10T14:30:00Z",
  "UpdatedUtc": "2025-11-10T14:30:00Z"
}
```

**Sessions:**
```json
{
  "SessionId": "sess-xyz",
  "Username": "hydra",
  "ProjectName": "MyProject",
  "ClientName": "ChatGPT",
  "AgentName": "GPT-4",
  "CreatedUtc": "2025-11-10T12:00:00Z",
  "LastActivityUtc": "2025-11-10T14:30:00Z",
  "Status": "Open",
  "PropertiesJson": "{\"key\":\"value\"}"
}
```
