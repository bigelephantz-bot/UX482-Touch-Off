using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using TouchOffResearch;

internal static class WindowsChecks
{
    private static int passed;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAILED: " + label);
        Console.WriteLine("PASS: " + label); passed++;
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int tokenClass,
        out uint info, uint size, out uint returned);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("release EXE and evidence directory required");
            string exe = Path.GetFullPath(args[0]); string evidence = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(evidence);
            Check(!NativeProbes.IsElevated(), "test process has non-elevated token; never requests UAC");
            using (var identity = WindowsIdentity.GetCurrent())
            {
                if (!GetTokenInformation(identity.Token, 18, out uint type, 4, out uint returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                Check(returned == 4 && type is 1 or 3, "token type and returned length valid");
                Console.WriteLine("ACCOUNT_TOKEN: " + (type == 3 ? "LimitedAdministrator_NotStandardAccount" : "DefaultNonElevated"));
            }
            TouchOffResearch.Program.InitializeWindows();
            Check(Application.HighDpiMode == HighDpiMode.PerMonitorV2, "Windows initialization uses PerMonitorV2 before HWND");
            Check(TouchTestForm.TouchInputSize == 48, "TOUCHINPUT x64 size is 48 bytes");
            var before = ReadWispSnapshot();
            using (var form = new MainForm())
            {
                form.Show(); Application.DoEvents();
                var controls = Descendants(form).ToArray();
                var consent = controls.OfType<CheckBox>().Single();
                var disclosure = controls.OfType<Label>().Single(x => x.Name == "ConsentDisclosure");
                var off = controls.OfType<Button>().Single(x => x.Text == "試驗 TouchGate 關閉");
                var restore = controls.OfType<Button>().Single(x => x.Text == "還原原始 TouchGate 設定");
                Check(!consent.Checked && !off.Enabled, "TouchGate consent default unchecked and write button disabled");
                consent.Checked = true; Check(off.Enabled, "explicit consent enables experiment button only");
                consent.Checked = false; Check(!off.Enabled, "revoking consent disables experiment button");
                var reportBox = controls.OfType<TextBox>().Single();
                using var report = JsonDocument.Parse(reportBox.Text);
                Check(report.RootElement.GetProperty("physicalTouchDisabled").GetString() == "NOT_VERIFIED_BY_SOFTWARE",
                    "live diagnostics never claim physical touch disabled");
                Check(report.RootElement.GetProperty("elevated").ValueKind == JsonValueKind.False,
                    "diagnostic report records ordinary permissions");
                Check(!reportBox.Text.Contains("ScopeHash", StringComparison.OrdinalIgnoreCase) &&
                    !reportBox.Text.Contains("S-1-5-", StringComparison.OrdinalIgnoreCase), "report excludes backup scope identifiers");
                File.WriteAllText(Path.Combine(evidence, "private-diagnostics.json"), reportBox.Text);
                Console.WriteLine("ACTUAL_DPI: " + form.DeviceDpi);
                foreach (var size in new[] { new Size(890, 620), new Size(700, 490) })
                {
                    form.ClientSize = size; Application.DoEvents(); form.PerformLayout();
                    Check(IsInside(form, consent) && IsInside(form, off) && IsInside(form, restore),
                        "consent/experiment/restore visible at client " + size);
                    Check(IsInside(form, disclosure) && disclosure.Height >=
                        disclosure.GetPreferredSize(new Size(disclosure.MaximumSize.Width, 0)).Height,
                        "wrapped consent height sufficient at client " + size);
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(evidence, $"main-{size.Width}x{size.Height}.png"));
                }
                form.Close();
            }
            using (var touch = new TouchTestForm())
            {
                touch.Show(); Application.DoEvents(); Check(touch.IsHandleCreated, "blank input window creates native handle");
                touch.Close(); // Exercise register/unregister without injecting any input.
            }
            Check(ReadWispSnapshot() == before, "real Wisp value snapshot unchanged after read-only UI checks");

            using (var process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false }))
            {
                if (process is null) throw new Exception("Cannot launch release");
                try
                {
                    Check(process.WaitForInputIdle(10000), "application reaches GUI input idle");
                    var timeout = Stopwatch.StartNew();
                    do
                    {
                        process.Refresh();
                        if (process.HasExited || process.MainWindowTitle.Length != 0) break;
                        Thread.Sleep(100);
                    } while (timeout.Elapsed < TimeSpan.FromSeconds(10));
                    Console.WriteLine("CHILD_WINDOW: " + (process.HasExited ? "Exited" : process.MainWindowTitle));
                    Check(!process.HasExited && process.MainWindowTitle.Contains("非已驗證解法"), "actual release EXE displays research main window");
                    Check(process.CloseMainWindow(), "release accepts normal window close");
                    Check(process.WaitForExit(10000) && process.ExitCode == 0, "release exits normally with code 0");
                }
                finally { if (!process.HasExited) process.Kill(); } // Our test child only.
            }
            Check(ReadWispSnapshot() == before, "real Wisp unchanged after actual release launch/exit");
            Console.WriteLine($"{passed} Windows checks passed. No real registry writes; no ASUS calls; no physical touch claims.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static string ReadWispSnapshot()
    {
        // Read-only comparison permits arbitrary existing value kinds without coercion.
        using var hive = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser,
            Microsoft.Win32.RegistryView.Registry64);
        using var key = hive.OpenSubKey(@"Software\Microsoft\Wisp\Touch", false);
        if (key is null || !key.GetValueNames().Contains("TouchGate", StringComparer.OrdinalIgnoreCase)) return "Absent";
        return key.GetValueKind("TouchGate") + ":" + JsonSerializer.Serialize(key.GetValue("TouchGate", null,
            Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames));
    }
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static bool IsInside(Form form, Control control) => control.Visible &&
        form.RectangleToScreen(form.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle));
}
