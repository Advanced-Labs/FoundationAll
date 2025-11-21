namespace Orion.DynamicKvGrains;

/// <summary>
/// State for the KV grain - persisted in RavenDB
/// </summary>
public class KvState
{
    public string? Value { get; set; }
}
