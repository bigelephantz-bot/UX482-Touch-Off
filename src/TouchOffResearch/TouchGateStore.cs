using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace TouchOffResearch;

internal static class PolicyGuard
{
    // Limited detection only; never evidence of enterprise approval.
    public static string[] ReadBlocks()
    {
        var blocks = new List<string>();
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        foreach (var (name, hive) in new[] { ("HKCU", user), ("HKLM", machine) })
        {
            using var touch = hive.OpenSubKey(@"SOFTWARE\Policies\Microsoft\TabletPC", false);
            if (touch?.GetValueNames().Contains("TurnOffTouchInput", StringComparer.OrdinalIgnoreCase) == true)
                blocks.Add(name + ":TouchInputPolicyPresent");
            using var registry = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", false);
            var flag = registry?.GetValue("DisableRegistryTools");
            if (flag is not null && !Equals(flag, 0))
                blocks.Add(name + ":RegistryToolsPolicyPresent");
        }
        return blocks.ToArray();
    }

    public static void EnsureCanExperiment()
    {
        if (ReadBlocks().Length != 0)
            throw new InvalidOperationException("Policy value present. Stop and use IT-approved handling.");
    }
}

internal sealed class WindowsGateRegistry : IGateRegistry
{
    private const string KeyPath = @"Software\Microsoft\Wisp\Touch";
    private const string ValueName = "TouchGate";
    private static RegistryKey UserHive() => RegistryKey.OpenBaseKey(
        RegistryHive.CurrentUser, RegistryView.Registry64);

    public GateValue Read()
    {
        using var hive = UserHive();
        using var key = hive.OpenSubKey(KeyPath, false);
        if (key is null || !key.GetValueNames().Contains(ValueName, StringComparer.OrdinalIgnoreCase))
            return new(false, null);
        bool isDword = key.GetValueKind(ValueName) == RegistryValueKind.DWord;
        object? raw = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return GateValue.FromRaw(raw, isDword);
    }

    public void Write(GateValue value)
    {
        using var hive = UserHive();
        if (value.Exists)
        {
            if (value.Value is not (0 or 1)) throw new InvalidOperationException("Invalid DWORD.");
            using var key = hive.CreateSubKey(KeyPath, writable: true)
                ?? throw new UnauthorizedAccessException("Cannot open current-user setting.");
            key.SetValue(ValueName, value.Value.Value, RegistryValueKind.DWord);
            key.Flush();
        }
        else
        {
            if (value.Value is not null) throw new InvalidOperationException("Invalid absent value.");
            using var key = hive.OpenSubKey(KeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            key?.Flush(); // Never delete the parent key or other values.
        }
    }
}

internal static class TouchGateStore
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UX482TouchResearch");
    public static GateValue Read() => new WindowsGateRegistry().Read();
    public static bool HasBackup() => new FileGateBackupStore(DataDirectory).Exists;

    private static string ScopeHash()
    {
        using var identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("No user scope.");
        // Local-only binding. Never export the hash, raw SID or machine name.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            sid + "|" + Environment.MachineName)));
    }

    private static TouchGateLifecycle Lifecycle() => new(new WindowsGateRegistry(),
        new FileGateBackupStore(DataDirectory), PolicyGuard.EnsureCanExperiment, ScopeHash());
    public static string ConfigureOff() => Lifecycle().ConfigureOff();
    public static string Restore() => Lifecycle().Restore();
}
