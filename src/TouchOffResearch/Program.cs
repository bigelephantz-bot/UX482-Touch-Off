using System.Text.Json;

namespace TouchOffResearch;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool worker = args.Length == 1 && args[0] == "--asus-probe-worker";
        try
        {
            if (!worker && OperatingSystem.IsWindows()) InitializeWindows();
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || NativeProbes.IsElevated())
            {
                if (!worker) MessageBox.Show("此研究工具只供 Windows x64 一般帳號測試。\n請勿以系統管理員身分執行。",
                    "TouchOff Research");
                return 2;
            }
            if (worker)
            {
                Console.WriteLine(JsonSerializer.Serialize(NativeProbes.QueryAsusInWorker()));
                return 0;
            }
            if (args.Length != 0) return 3;
            using var mutex = new Mutex(true, @"Local\UX482.TouchOffResearch.v01", out bool created);
            if (!created)
            {
                MessageBox.Show("同一工作階段已有研究工具執行中。", "TouchOff Research");
                return 4;
            }
            try
            {
                Application.Run(new MainForm());
            }
            finally { mutex.ReleaseMutex(); }
            return 0;
        }
        catch (Exception ex)
        {
            if (!worker)
                MessageBox.Show($"工具停止：{ex.GetType().Name}（0x{ex.HResult:X8}）。\n未要求提權；請將錯誤碼交回私人端分析。",
                    "TouchOff Research");
            return 1;
        }
    }

    internal static void InitializeWindows()
    {
        // Before any dialog or HWND, including the refusal/error paths.
        if (!Application.SetHighDpiMode(HighDpiMode.PerMonitorV2))
            throw new InvalidOperationException("Cannot initialize PerMonitorV2 DPI mode.");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }
}
