using System.Text.Json;
using TouchOffResearch;

int passed = 0;
void Test(string name, Action action)
{
    action();
    Console.WriteLine("PASS: " + name);
    passed++;
}
void Check(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

foreach (var original in new[] { new GateValue(false, null), new GateValue(true, 1) })
{
    Test($"exact configure/reopen/restore: {original}", () =>
    {
        var f = new Fixture(original);
        Check(f.Lifecycle.ConfigureOff() == "Configured_PendingManualRestartAndPhysicalTest");
        Check(f.Backup.Data?.Original == original && f.Registry.Value == Fixture.Off);
        Check(f.Backup.Saves == 1 && f.Backup.Reads == 1);
        // New lifecycle instance models restart; state comes from the adapters.
        Check(f.NewLifecycle().Restore() == "OriginalRestored_PendingManualRestartAndPhysicalTest");
        Check(f.Registry.Value == original && f.Backup.Data is null && f.Backup.Archived?.Original == original);
    });
}
Test("already zero never writes or creates original", () =>
{
    var f = new Fixture(Fixture.Off);
    Check(f.Lifecycle.ConfigureOff() == "TouchGateAlreadyZero_NotProofOfTouchDisabled");
    Check(f.Registry.Writes == 0 && f.Backup.Saves == 0);
});
Test("repeat configure while zero preserves original", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); var original = f.Backup.Data;
    f.Lifecycle.ConfigureOff();
    Check(f.Backup.Data == original && f.Backup.Saves == 1 && f.Registry.Writes == 1);
});
foreach (var (raw, kind) in new (object?, bool)[] { ("1", false), (1L, false), (2, true), (-1, true), (null, true) })
    Test($"reject raw type/value {raw ?? "null"} DWORD={kind}", () =>
    {
        var f = new Fixture(); f.Registry.BeforeRead = _ => GateValue.FromRaw(raw, kind);
        Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
        Check(f.Registry.Writes == 0 && f.Backup.Saves == 0);
    });
Test("DWORD 0/1 parsed without conversion from strings", () =>
    Check(GateValue.FromRaw(0, true) == Fixture.Off && GateValue.FromRaw(1, true) == new GateValue(true, 1)));
Test("reject malformed absent state", () =>
{
    var f = new Fixture(new(false, 1)); Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Registry.Writes == 0 && f.Backup.Saves == 0);
});
Test("read access denied before backup", () =>
{
    var f = new Fixture(); f.Registry.BeforeRead = _ => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Backup.Saves == 0 && f.Registry.Writes == 0);
});
foreach (bool accessDenied in new[] { false, true })
    Test("backup save failure leaves registry unchanged: accessDenied=" + accessDenied, () =>
    {
        var f = new Fixture(); f.Backup.OnSave = _ => throw (accessDenied ? new UnauthorizedAccessException() : new IOException());
        Throws<Exception>(() => f.Lifecycle.ConfigureOff());
        Check(f.Registry.Writes == 0 && f.Registry.Value == new GateValue(true, 1));
    });
Test("backup read denial prevents registry write", () =>
{
    var f = new Fixture(); f.Backup.OnRead = () => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Registry.Writes == 0 && f.Backup.Data is not null);
});
Test("backup valid but wrong original readback prevents write", () =>
{
    var f = new Fixture(); f.Backup.OnSave = b => f.Backup.Data = b with { Original = new(false, null) };
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff()); Check(f.Registry.Writes == 0);
});
Test("backup timestamp altered also fails full readback", () =>
{
    var f = new Fixture(); f.Backup.OnSave = b => f.Backup.Data = b with { CreatedUtc = b.CreatedUtc.AddSeconds(1) };
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff()); Check(f.Registry.Writes == 0);
});
Test("pending original never overwritten even if current equals original", () =>
{
    var f = new Fixture(); f.Backup.Data = f.OriginalBackup;
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Backup.Saves == 0 && f.Registry.Writes == 0 && f.Backup.Data == f.OriginalBackup);
});
foreach (var invalid in new[] {
    new GateBackup(2, "test-scope", DateTimeOffset.UnixEpoch, new(true, 1)),
    new GateBackup(1, "other-scope", DateTimeOffset.UnixEpoch, new(true, 1)),
    new GateBackup(1, "test-scope", DateTimeOffset.UnixEpoch, new(true, 2)),
    new GateBackup(1, "test-scope", DateTimeOffset.UnixEpoch, new(false, 0)),
    new GateBackup(1, "test-scope", DateTimeOffset.UnixEpoch, null!) })
    Test("invalid backup refuses configure and restore: " + JsonSerializer.Serialize(invalid), () =>
    {
        var f = new Fixture(Fixture.Off); f.Backup.Data = invalid;
        Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
        Throws<InvalidOperationException>(() => f.Lifecycle.Restore());
        Check(f.Registry.Writes == 0 && f.Backup.Archived is null);
    });
Test("policy block before any backup/write", () =>
{
    var f = new Fixture(); f.Policy = () => throw new InvalidOperationException();
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
    Throws<InvalidOperationException>(() => f.Lifecycle.Restore());
    Check(f.Registry.Writes == 0 && f.Backup.Saves == 0);
});
Test("policy read denied stops without alternate key", () =>
{
    var f = new Fixture(); f.Policy = () => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.ConfigureOff()); Check(f.Registry.Writes == 0);
});
Test("policy changes during backup prevents write", () =>
{
    var f = new Fixture(); int calls = 0;
    f.Policy = () => { if (++calls == 2) throw new UnauthorizedAccessException(); };
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Registry.Writes == 0 && f.Backup.Data is not null);
});
Test("external change after backup prevents configure", () =>
{
    var f = new Fixture(); f.Backup.OnSave = _ => f.Registry.Value = new(false, null);
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Registry.Writes == 0 && f.Registry.Value == new GateValue(false, null));
});
Test("write denied retains original and restore avoids redundant write", () =>
{
    var f = new Fixture(); f.Registry.OnWrite = _ => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.ConfigureOff());
    Check(f.Backup.Data?.Original == new GateValue(true, 1));
    f.Lifecycle.Restore(); Check(f.Registry.Writes == 1 && f.Backup.Archived is not null);
});
Test("flush failure after write retains backup for recovery", () =>
{
    var f = new Fixture(); f.Registry.OnWrite = v => { f.Registry.Value = v; throw new IOException(); };
    Throws<IOException>(() => f.Lifecycle.ConfigureOff()); Check(f.Backup.Data is not null);
    f.Registry.OnWrite = null; f.NewLifecycle().Restore(); Check(f.Registry.Value == new GateValue(true, 1));
});
Test("configure readback mismatch retains original", () =>
{
    var f = new Fixture(); f.Registry.OnWrite = _ => { };
    Throws<InvalidOperationException>(() => f.Lifecycle.ConfigureOff()); Check(f.Backup.Data is not null);
});
Test("no backup restore changes nothing", () =>
{
    var f = new Fixture(); Check(f.Lifecycle.Restore() == "NoOriginalBackup_NoChange");
    Check(f.Registry.Writes == 0);
});
Test("restore access denied retains backup", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff();
    f.Registry.OnWrite = _ => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.Restore());
    Check(f.Backup.Data is not null && f.Backup.Archived is null && f.Registry.Value == Fixture.Off);
});
Test("absent restore deletion denied retains backup", () =>
{
    var f = new Fixture(new(false, null)); f.Lifecycle.ConfigureOff();
    f.Registry.OnWrite = _ => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.Restore()); Check(f.Backup.Data?.Original == new GateValue(false, null));
});
Test("restore readback mismatch retains backup", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); f.Registry.OnWrite = _ => { };
    Throws<InvalidOperationException>(() => f.Lifecycle.Restore()); Check(f.Backup.Data is not null);
});
Test("external conflicting value not overwritten on restore", () =>
{
    var f = new Fixture(new(false, null)); f.Lifecycle.ConfigureOff(); f.Registry.Value = new(true, 1);
    Throws<InvalidOperationException>(() => f.Lifecycle.Restore()); Check(f.Registry.Writes == 1 && f.Backup.Data is not null);
});
Test("external deletion not overwritten on restore", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); f.Registry.Value = new(false, null);
    Throws<InvalidOperationException>(() => f.Lifecycle.Restore()); Check(f.Registry.Writes == 1);
});
Test("external change between restore reads prevents write", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); int reads = 0;
    f.Registry.BeforeRead = _ => { if (++reads == 2) f.Registry.Value = new(false, null); };
    Throws<InvalidOperationException>(() => f.Lifecycle.Restore()); Check(f.Registry.Writes == 1);
});
Test("external already-original value only archives; does not write", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); f.Registry.Value = new(true, 1);
    f.Lifecycle.Restore(); Check(f.Registry.Writes == 1 && f.Backup.Archived is not null);
});
Test("policy denial on restore retains original", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); f.Policy = () => throw new UnauthorizedAccessException();
    Throws<UnauthorizedAccessException>(() => f.Lifecycle.Restore()); Check(f.Backup.Data is not null && f.Registry.Writes == 1);
});
Test("archive failure retains original; retry does not rewrite", () =>
{
    var f = new Fixture(); f.Lifecycle.ConfigureOff(); f.Backup.OnArchive = () => throw new IOException();
    Throws<IOException>(() => f.Lifecycle.Restore()); Check(f.Backup.Data is not null && f.Registry.Writes == 2);
    f.Backup.OnArchive = null; f.Lifecycle.Restore(); Check(f.Registry.Writes == 2 && f.Backup.Archived is not null);
});
Test("operation contention fails before accessing registry", () =>
{
    var f = new Fixture(); f.Backup.Locked = true;
    Throws<IOException>(() => f.Lifecycle.ConfigureOff());
    Throws<IOException>(() => f.Lifecycle.Restore()); Check(f.Registry.Reads == 0 && f.Registry.Writes == 0);
});

// Production JSON/file adapter, isolated temp directory, with fake registry only.
string temp = Path.Combine(Path.GetTempPath(), "TouchOffRegistryChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    Test("real file adapter durable JSON roundtrip and exact absent restore", () =>
    {
        var registry = new FakeRegistry(new(false, null)); var store = new FileGateBackupStore(temp);
        new TouchGateLifecycle(registry, store, () => { }, "test-scope").ConfigureOff();
        Check(store.Read().Original == new GateValue(false, null));
        new TouchGateLifecycle(registry, new FileGateBackupStore(temp), () => { }, "test-scope").Restore();
        Check(!store.Exists && registry.Value == new GateValue(false, null));
        Check(Directory.GetFiles(temp, "TouchGate.restored.*.json").Length == 1);
    });
    Test("file adapter rejects concurrent instances", () =>
    {
        var a = new FileGateBackupStore(temp); var b = new FileGateBackupStore(temp);
        using var lease = a.AcquireOperation(); Throws<IOException>(() => b.AcquireOperation());
    });
    Test("file adapter no overwrite and corrupt JSON fails closed", () =>
    {
        var store = new FileGateBackupStore(temp);
        var original = new GateBackup(1, "test-scope", DateTimeOffset.UtcNow, new(true, 1));
        store.SaveNew(original);
        Throws<IOException>(() => store.SaveNew(original with { Original = Fixture.Off }));
        Check(store.Read() == original && Directory.GetFiles(temp, "*.tmp").Length == 0);
        File.WriteAllText(Path.Combine(temp, "TouchGate.original.json"), "{corrupt");
        var registry = new FakeRegistry(Fixture.Off);
        Throws<JsonException>(() => new TouchGateLifecycle(registry, store, () => { }, "test-scope").Restore());
        Check(registry.Writes == 0 && store.Exists);
    });
}
finally
{
    string resolved = Path.GetFullPath(temp);
    string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFileName(resolved).StartsWith("TouchOffRegistryChecks-", StringComparison.Ordinal))
        throw new InvalidOperationException("Unexpected test cleanup path.");
    Directory.Delete(resolved, recursive: true);
}
Console.WriteLine($"{passed} cases passed. Fake registry only; real Wisp was never accessed.");

sealed class Fixture
{
    public static readonly GateValue Off = new(true, 0);
    public FakeRegistry Registry { get; }
    public FakeBackups Backup { get; } = new();
    public Action Policy = () => { };
    public TouchGateLifecycle Lifecycle { get; }
    public GateBackup OriginalBackup { get; }
    public Fixture(GateValue? original = null)
    {
        Registry = new(original ?? new(true, 1));
        OriginalBackup = new(1, "test-scope", DateTimeOffset.UnixEpoch, Registry.Value);
        Lifecycle = NewLifecycle();
    }
    public TouchGateLifecycle NewLifecycle() => new(Registry, Backup, () => Policy(), "test-scope");
}
sealed class FakeRegistry(GateValue original) : IGateRegistry
{
    public GateValue Value = original;
    public int Reads, Writes;
    public Action<int>? BeforeRead;
    public Action<GateValue>? OnWrite;
    public GateValue Read() { BeforeRead?.Invoke(++Reads); return Value; }
    public void Write(GateValue value) { Writes++; if (OnWrite is not null) OnWrite(value); else Value = value; }
}
sealed class FakeBackups : IGateBackupStore
{
    public GateBackup? Data, Archived;
    public int Saves, Reads;
    public bool Locked;
    public Action<GateBackup>? OnSave;
    public Action? OnRead, OnArchive;
    public bool Exists => Data is not null;
    public IDisposable AcquireOperation()
    {
        if (Locked) throw new IOException("Busy");
        Locked = true; return new Lease(() => Locked = false);
    }
    public GateBackup Read() { Reads++; OnRead?.Invoke(); return Data ?? throw new FileNotFoundException(); }
    public void SaveNew(GateBackup backup)
    {
        if (Data is not null) throw new IOException("Already exists");
        Saves++; Data = backup; OnSave?.Invoke(backup);
    }
    public void Archive(GateBackup expected)
    {
        OnArchive?.Invoke(); if (Data != expected) throw new IOException("Changed");
        Archived = Data; Data = null;
    }
    private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
}
