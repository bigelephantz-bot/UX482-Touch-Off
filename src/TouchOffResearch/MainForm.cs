using System.Text;
using System.Text.Json;

namespace TouchOffResearch;

internal sealed class MainForm : Form
{
    private readonly TextBox reportBox = new() { Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
    private const string ConsentText = "我已取得測試許可，且接受主螢幕／其他螢幕的手指觸控也可能停用。";
    private readonly CheckBox acceptAll = new() { AutoSize = true,
        AccessibleName = ConsentText, Anchor = AnchorStyles.Top | AnchorStyles.Left };
    private readonly Button off = new() { Text = "試驗 TouchGate 關閉", AutoSize = true, Enabled = false };
    private readonly Button asus = new() { Text = "ASUS 唯讀查詢", AutoSize = true };
    private readonly Dictionary<string, object?> report = new();
    private readonly List<object> operations = [];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft JhengHei UI", 10F);
        Text = "UX482 TouchOff Research v0.1 — 非已驗證解法";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(890, 620);
        MinimumSize = new Size(700, 490);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1,
            AutoScroll = true,
            Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var explanation = new Label { AutoSize = true,
            Text = "診斷不會關閉觸控。TouchGate 是目前帳號的整體手指觸控試驗，並非只關閉副螢幕；" +
                "設定寫入成功不等於誤觸已消失。此版不改顯示模式、不寫 ASUS 硬體控制命令。" };
        layout.Controls.Add(explanation, 0, 0);
        var consentLabel = new Label { Name = "ConsentDisclosure", AutoSize = true, Text = ConsentText };
        var consentRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        consentRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        consentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        consentRow.Controls.Add(acceptAll, 0, 0);
        consentRow.Controls.Add(consentLabel, 1, 0);
        consentLabel.Click += (_, _) => acceptAll.Checked = !acceptAll.Checked;
        layout.SizeChanged += (_, _) =>
        {
            int width = Math.Max(100, layout.ClientSize.Width - layout.Padding.Horizontal -
                SystemInformation.VerticalScrollBarWidth - 8);
            explanation.MaximumSize = new Size(width, 0);
            consentLabel.MaximumSize = new Size(Math.Max(100, width - acceptAll.Width - 12), 0);
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        AddButton(buttons, "重新診斷", RefreshDiagnostics);
        buttons.Controls.Add(asus);
        asus.Click += async (_, _) => await ProbeAsus();
        AddButton(buttons, "開啟空白輸入測試區", () => new TouchTestForm().Show(this));
        AddButton(buttons, "匯出最小報告", Export);
        layout.Controls.Add(buttons, 0, 1);
        layout.Controls.Add(reportBox, 0, 2);
        layout.Controls.Add(consentRow, 0, 3);
        acceptAll.CheckedChanged += (_, _) => off.Enabled = acceptAll.Checked;
        var changes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        changes.Controls.Add(off);
        off.Click += (_, _) => ChangeGate(false);
        AddButton(changes, "還原原始 TouchGate 設定", () => ChangeGate(true));
        AddButton(changes, "結束", Close);
        layout.Controls.Add(changes, 0, 4);
        Controls.Add(layout);
        Shown += (_, _) => RefreshDiagnostics();
    }

    private void AddButton(FlowLayoutPanel panel, string label, Action action)
    {
        var button = new Button { Text = label, AutoSize = true };
        button.Click += (_, _) => { try { action(); } catch (Exception ex) { Fail(label, ex); } };
        panel.Controls.Add(button);
    }

    private void RefreshDiagnostics()
    {
        report["version"] = "0.1.0";
        report["windowsVersion"] = Environment.OSVersion.Version.ToString();
        report["process64Bit"] = Environment.Is64BitProcess;
        report["elevated"] = false; // Entry point rejects elevated execution.
        report["activeDisplays"] = Screen.AllScreens.Select((s, i) => new {
            Alias = $"Display-{i + 1}", s.Primary,
            Bounds = new { s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height }
        }).ToArray();
        TryRead("pointerCount", NativeProbes.PointerCount);
        TryRead("touchGate", () => TouchGateStore.Read());
        TryRead("policyBlocks", () => PolicyGuard.ReadBlocks());
        TryRead("originalBackupPresent", () => TouchGateStore.HasBackup());
        report["physicalTouchDisabled"] = "NOT_VERIFIED_BY_SOFTWARE";
        report["scope"] = "TouchGate is account-wide, not ScreenPad-only; no power-off guarantee.";
        report["operations"] = operations;
        Render();
    }

    private void TryRead(string key, Func<object?> read)
    {
        try { report[key] = read(); }
        catch (Exception ex) { report[key] = ErrorRecord(ex); }
    }

    private async Task ProbeAsus()
    {
        if (MessageBox.Show(this,
            "此動作會呼叫既有 ATKACPI 驅動，僅查詢 ScreenPad 的兩個狀態。\n" +
            "它不會安裝驅動，也不會送出開關／亮度寫入命令。\n" +
            "查詢成功不證明能停用觸控。是否執行一次？", "ASUS 研究查詢",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        asus.Enabled = false;
        try { report["asusProbe"] = await NativeProbes.QueryAsusAsync(); }
        catch (Exception ex) { report["asusProbe"] = ErrorRecord(ex); }
        // Deliberately one query per application run; no automated retries.
        Render();
    }

    private void ChangeGate(bool restore)
    {
        if (!restore && !acceptAll.Checked) return;
        string warning = restore
            ? "只還原本工具備份的 TouchGate 原值。\n還原後請自行儲存工作並重新啟動 Windows。\n是否繼續？"
            : "此試驗可能停用這個帳號的全部手指觸控，包括主螢幕；不是只關閉 ScreenPad。\n" +
              "請確認鍵盤、右側觸控板或滑鼠可用。\n程式會先備份原值，再寫入 TouchGate=0。\n" +
              "它不會自動重開機；請自行儲存工作並重開機後測試。\n是否繼續？";
        if (MessageBox.Show(this, warning, restore ? "還原確認" : "整體觸控試驗確認",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        try
        {
            string outcome = restore ? TouchGateStore.Restore() : TouchGateStore.ConfigureOff();
            operations.Add(new { Action = restore ? "Restore" : "ConfigureOff", Outcome = outcome });
            RefreshDiagnostics();
            MessageBox.Show(this, outcome + "\n\n這只是設定結果；請自行儲存工作、重開機並實測。\n" +
                "單純關閉或刪除 EXE 不會自動還原 TouchGate。", "設定結果");
        }
        catch (Exception ex) { Fail(restore ? "Restore" : "ConfigureOff", ex); }
    }

    private static object ErrorRecord(Exception ex) => new {
        Type = ex.GetType().Name, HResult = $"0x{ex.HResult:X8}",
        Win32Error = ex is System.ComponentModel.Win32Exception native ? (int?)native.NativeErrorCode : null
    };
    private void Fail(string action, Exception ex)
    {
        operations.Add(new { Action = action, Error = ErrorRecord(ex) });
        Render();
        MessageBox.Show(this, $"{action} 停止：{ex.Message}\n\n不要求提權，不更改權限或公司政策。",
            "未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    private void Render()
    {
        report["operations"] = operations;
        reportBox.Text = JsonSerializer.Serialize(report, JsonOptions);
    }
    private void Export()
    {
        Render();
        using var dialog = new SaveFileDialog { FileName = "UX482-TouchResearch-report.json",
            Filter = "JSON files (*.json)|*.json", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, reportBox.Text, new UTF8Encoding(false));
        MessageBox.Show(this, "報告已儲存。請先檢視內容，並依公司核准方式傳回私人電腦。\n" +
            "原始設定備份不包含在報告內。", "報告");
    }
}
