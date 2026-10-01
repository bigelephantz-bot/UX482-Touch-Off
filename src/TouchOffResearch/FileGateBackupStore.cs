using System.Text.Json;

namespace TouchOffResearch;

internal sealed class FileGateBackupStore(string directory) : IGateBackupStore
{
    private string BackupPath => Path.Combine(directory, "TouchGate.original.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IDisposable AcquireOperation()
    {
        Directory.CreateDirectory(directory);
        // Same account, including separate Windows sessions: fail busy rather than retry.
        return new FileStream(Path.Combine(directory, "TouchGate.operation.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public bool Exists
    {
        get
        {
            try { _ = File.GetAttributes(BackupPath); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            // Unlike File.Exists, an access error must not look like an absent backup.
        }
    }

    public GateBackup Read() => JsonSerializer.Deserialize<GateBackup>(File.ReadAllText(BackupPath))
        ?? throw new InvalidOperationException("Invalid backup JSON.");

    public void SaveNew(GateBackup backup)
    {
        Directory.CreateDirectory(directory);
        string temporary = BackupPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, backup, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, BackupPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Archive(GateBackup expected)
    {
        if (Read() != expected)
            throw new InvalidOperationException("Backup changed externally; retained for inspection.");
        string archive = Path.Combine(directory, "TouchGate.restored." +
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N") + ".json");
        File.Move(BackupPath, archive, overwrite: false);
    }
}
