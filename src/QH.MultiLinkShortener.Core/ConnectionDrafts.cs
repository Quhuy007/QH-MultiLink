namespace QH.MultiLinkShortener.Core;

public sealed record ConnectionDraft(string Key = "", string Domain = "");

/// <summary>Unsaved credentials belong to a service, never to the shared input control.</summary>
public sealed class ConnectionDrafts(ISecretStore secrets, IEnumerable<ProviderInfo> connections)
{
    private readonly Dictionary<string, ProviderInfo> _connections = connections.ToDictionary(x => x.SecretName!);
    private readonly Dictionary<string, ConnectionDraft> _drafts = [];

    public ConnectionDraft Get(string name) => _drafts.GetValueOrDefault(name) ?? new(Domain: SavedDomain(name));
    public void Set(string name, ConnectionDraft draft)
    {
        if (!_connections.ContainsKey(name)) throw new ArgumentException("Unknown connection.");
        if (draft.Key.Length == 0 && draft.Domain == SavedDomain(name)) _drafts.Remove(name);
        else _drafts[name] = draft;
    }
    public bool HasChanges(string name) => _drafts.ContainsKey(name);
    public bool HasSavedKey(string name) => !string.IsNullOrEmpty(secrets.Read(name));
    private string SavedDomain(string name) => _connections[name].RequiresDomain ? ServiceCredential.Decode(secrets.Read(name))?.Domain ?? "" : "";

    public bool Save(string name)
    {
        if (!_drafts.TryGetValue(name, out var draft)) return false;
        var value = Validate(name, draft);
        secrets.Write(name, value);
        _drafts.Remove(name);
        return true;
    }
    public int SaveAll()
    {
        // Validate all drafts first, so a typo does not silently save only some services.
        var pending = _drafts.Select(x => (Name: x.Key, Value: Validate(x.Key, x.Value))).ToArray();
        foreach (var item in pending)
        {
            secrets.Write(item.Name, item.Value);
            _drafts.Remove(item.Name);
        }
        return pending.Length;
    }
    public void Remove(string name)
    {
        secrets.Write(name, null);
        _drafts.Remove(name);
    }
    private string Validate(string name, ConnectionDraft draft)
    {
        var info = _connections[name];
        var key = draft.Key.Trim();
        if (key.Length == 0 && info.RequiresDomain) key = ServiceCredential.Decode(secrets.Read(name))?.ApiKey ?? "";
        if (!ServiceCredential.ValidKey(key) || (name == "spoo" && !key.StartsWith("spoo_", StringComparison.Ordinal)))
            throw new ArgumentException($"{info.Name}: key không hợp lệ hoặc còn trống; không chứa khoảng trắng. Key spoo.me cần bắt đầu bằng spoo_.");
        if (!info.RequiresDomain) return key;
        var domain = draft.Domain.Trim().ToLowerInvariant();
        if (!ServiceCredential.ValidDomain(domain))
            throw new ArgumentException($"{info.Name}: nhập domain của tài khoản, không kèm https:// hoặc đường dẫn.");
        return new ServiceCredential(key, domain).Encode();
    }
}
