namespace TouchOffResearch;

public record GateValue(bool Exists, int? Value)
{
    internal static GateValue FromRaw(object? raw, bool isDword)
    {
        if (!isDword || raw is not int value || value is not (0 or 1))
            throw new InvalidOperationException("Unexpected TouchGate type/value; will not overwrite it.");
        return new(true, value);
    }
}
internal record GateBackup(int Schema, string ScopeHash, DateTimeOffset CreatedUtc,
    GateValue Original);

// Pure lifecycle; production adapters are the only code allowed to touch HKCU.
internal interface IGateRegistry
{
    GateValue Read();
    void Write(GateValue value);
}

internal interface IGateBackupStore
{
    IDisposable AcquireOperation();
    bool Exists { get; }
    GateBackup Read();
    void SaveNew(GateBackup backup);
    void Archive(GateBackup expected);
}

internal sealed class TouchGateLifecycle(IGateRegistry registry, IGateBackupStore backups,
    Action ensurePolicyAllows, string scopeHash)
{
    private static readonly GateValue Off = new(true, 0);

    private static GateValue ValidateValue(GateValue value)
    {
        if ((!value.Exists && value.Value is not null) ||
            (value.Exists && value.Value is not (0 or 1)))
            throw new InvalidOperationException("Unexpected TouchGate value; no overwrite allowed.");
        return value;
    }

    private GateBackup LoadBackup()
    {
        var backup = backups.Read();
        if (backup.Schema != 1 || backup.ScopeHash != scopeHash || backup.Original is null)
            throw new InvalidOperationException("Backup scope/schema does not match.");
        ValidateValue(backup.Original);
        return backup;
    }

    public string ConfigureOff()
    {
        ensurePolicyAllows();
        using var operation = backups.AcquireOperation();
        var before = ValidateValue(registry.Read());
        GateBackup? pending = backups.Exists ? LoadBackup() : null;
        if (before == Off) return "TouchGateAlreadyZero_NotProofOfTouchDisabled";
        if (pending is not null)
        {
            // A pending backup represents an unresolved attempt, not permission to retry.
            throw new InvalidOperationException("Pending original backup exists. Restore first; no new write performed.");
        }
        var saved = new GateBackup(1, scopeHash, DateTimeOffset.UtcNow, before);
        backups.SaveNew(saved); // Must flush durably and never replace an existing backup.
        if (LoadBackup() != saved)
            throw new InvalidOperationException("Backup readback mismatch. No registry write performed.");
        ensurePolicyAllows();
        if (ValidateValue(registry.Read()) != before)
            throw new InvalidOperationException("Concurrent setting change. No write performed.");
        registry.Write(Off);
        if (ValidateValue(registry.Read()) != Off)
            throw new InvalidOperationException("Readback mismatch; original backup retained.");
        return "Configured_PendingManualRestartAndPhysicalTest";
    }

    public string Restore()
    {
        ensurePolicyAllows();
        using var operation = backups.AcquireOperation();
        if (!backups.Exists) return "NoOriginalBackup_NoChange";
        var backup = LoadBackup();
        var before = ValidateValue(registry.Read());
        if (before != Off && before != backup.Original)
            throw new InvalidOperationException("Setting changed externally; will not overwrite.");
        ensurePolicyAllows();
        if (ValidateValue(registry.Read()) != before)
            throw new InvalidOperationException("Concurrent setting change. No write performed.");
        // A failed write or an external restore can already leave the original intact.
        if (before != backup.Original) registry.Write(backup.Original);
        if (ValidateValue(registry.Read()) != backup.Original)
            throw new InvalidOperationException("Restore readback mismatch; backup retained.");
        backups.Archive(backup);
        return "OriginalRestored_PendingManualRestartAndPhysicalTest";
    }
}
