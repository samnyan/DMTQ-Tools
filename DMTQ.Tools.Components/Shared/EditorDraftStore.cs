namespace DMTQ_Tools.Components.Shared;

/// <summary>Retains active editor drafts while a failed route is replaced by the global error boundary.</summary>
public sealed class EditorDraftStore
{
    private readonly Dictionary<string, object> _drafts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Set<T>(string key, T draft) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
            _drafts[key] = draft;
    }

    public bool TryGet<T>(string key, out T? draft) where T : class
    {
        lock (_gate)
        {
            if (_drafts.TryGetValue(key, out var value) && value is T typed)
            {
                draft = typed;
                return true;
            }
        }

        draft = null;
        return false;
    }

    public void Remove(string key)
    {
        lock (_gate)
            _drafts.Remove(key);
    }
}
