using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace TouchOffResearch;

internal static class NativeProbes
{
    [StructLayout(LayoutKind.Sequential)] private struct TokenElevation { public uint Elevated; }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int informationClass,
        out TokenElevation information, uint informationLength, out uint returnLength);

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        uint size = (uint)Marshal.SizeOf<TokenElevation>();
        if (!GetTokenInformation(identity.Token, 20, out var elevation, size, out uint returned))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot verify non-elevated token.");
        if (returned != size || elevation.Elevated > 1)
            throw new InvalidOperationException("Invalid token elevation response; execution refused.");
        return elevation.Elevated != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerDevices(ref uint deviceCount, IntPtr devices);

    // Count only. Avoid collecting product strings, serials or device paths.
    public static object PointerCount()
    {
        uint count = 0;
        bool ok = GetPointerDevices(ref count, IntPtr.Zero);
        int error = ok ? 0 : Marshal.GetLastWin32Error();
        return new { Succeeded = ok, Count = ok ? (uint?)count : null, Win32Error = error,
            Note = "Includes supported pointer types; not a count of physical touchscreens." };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint sharing,
        IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        [In] byte[] input, uint inputLength, [Out] byte[] output, uint outputLength,
        out uint bytesReturned, IntPtr overlapped);

    public static AsusProbeResult QueryAsusInWorker()
    {
        // The reference driver's handle is opened read/write, but ONLY DSTS query
        // requests are issued. This is not a Windows read-only handle and is not
        // an ASUS-supported compatibility guarantee. Denied access is final.
        using var device = CreateFileW(@"\\.\ATKACPI", 0xC0000000, 3,
            IntPtr.Zero, 3, 0x80, IntPtr.Zero);
        if (device.IsInvalid)
            return new("OpenFailed", Marshal.GetLastWin32Error(), []);
        var queries = new List<AsusQueryResult>();
        foreach (uint id in new[] { AsusProtocol.ScreenPadPower, AsusProtocol.ScreenPadBrightness })
        {
            var input = AsusProtocol.BuildRead(id);
            var output = new byte[16];
            bool ok = DeviceIoControl(device, AsusProtocol.Ioctl, input, (uint)input.Length,
                output, (uint)output.Length, out uint returned, IntPtr.Zero);
            int error = ok ? 0 : Marshal.GetLastWin32Error();
            queries.Add(AsusProtocol.Decode(id, ok, error, output, returned));
            if (!ok) break; // No escalating retries or alternate interfaces.
        }
        return new("QueryCompleted_NotTouchDisabled", 0, queries.ToArray());
    }

    public static async Task<AsusProbeResult> QueryAsusAsync()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("No executable path.");
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        info.ArgumentList.Add("--asus-probe-worker");
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Cannot start query worker.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            if (process.ExitCode != 0)
                return new("WorkerFailed", process.ExitCode, []);
            string json = await output.WaitAsync(cancellation.Token);
            return JsonSerializer.Deserialize<AsusProbeResult>(json)
                ?? new("InvalidWorkerOutput", 0, []);
        }
        catch (OperationCanceledException)
        {
            // Only our own child is targeted. No services or unrelated processes.
            try { if (!process.HasExited) process.Kill(entireProcessTree: false); }
            catch { /* Some driver failures are not cancellable. Do not retry. */ }
            return new("WorkerTimedOut_DoNotRetry", 0, []);
        }
    }
}
