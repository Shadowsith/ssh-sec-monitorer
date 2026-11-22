namespace Monitorer.Services;

/// <summary>Caches (singleton) whether the initial setup is done, so the DB is not asked on every request.</summary>
public class SetupState
{
    private volatile bool _completed;

    public bool Completed => _completed;

    public void MarkCompleted() => _completed = true;
}
