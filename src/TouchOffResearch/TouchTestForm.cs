using System.Runtime.InteropServices;

namespace TouchOffResearch;

internal sealed class TouchTestForm : Form
{
    private const int WmTouch = 0x0240;
    private int touchDowns, mouseDowns, touchMessages, inputReadErrors, lastInputError;
    private bool touchRegistered;
    private int registrationError;
    internal static int TouchInputSize => Marshal.SizeOf<TouchInput>();
    [StructLayout(LayoutKind.Sequential)]
    private struct TouchInput
    {
        public int X, Y;
        public IntPtr Source;
        public uint Id, Flags, Mask, Time;
        public UIntPtr ExtraInfo;
        public uint ContactWidth, ContactHeight;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterTouchWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterTouchWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTouchInputInfo(IntPtr input, uint count,
        [Out] TouchInput[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseTouchInputHandle(IntPtr input);

    public TouchTestForm()
    {
        Text = "空白輸入測試區 — Esc 結束";
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        DoubleBuffered = true;
        Font = new Font("Microsoft JhengHei UI", 16F);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        touchRegistered = RegisterTouchWindow(Handle, 0);
        registrationError = touchRegistered ? 0 : Marshal.GetLastWin32Error();
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (touchRegistered && !UnregisterTouchWindow(Handle))
        {
            inputReadErrors++;
            lastInputError = Marshal.GetLastWin32Error();
        }
        touchRegistered = false;
        base.OnHandleDestroyed(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        mouseDowns++;
        Invalidate();
        base.OnMouseDown(e);
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmTouch)
        {
            touchMessages++;
            try
            {
                uint count = unchecked((uint)message.WParam.ToInt64()) & 0xFFFF;
                if (count > 0 && count <= 256)
                {
                    var inputs = new TouchInput[checked((int)count)];
                    if (GetTouchInputInfo(message.LParam, count, inputs, Marshal.SizeOf<TouchInput>()))
                        touchDowns += inputs.Count(x => (x.Flags & 0x0002) != 0);
                    else
                    {
                        inputReadErrors++;
                        lastInputError = Marshal.GetLastWin32Error();
                    }
                }
                else
                {
                    inputReadErrors++;
                    lastInputError = 13; // ERROR_INVALID_DATA, not a successful zero-event sample.
                }
            }
            finally
            {
                if (!CloseTouchInputHandle(message.LParam))
                {
                    inputReadErrors++;
                    lastInputError = Marshal.GetLastWin32Error();
                }
            }
            message.Result = IntPtr.Zero;
            Invalidate();
            return;
        }
        base.WndProc(ref message);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        string text = $"觸控按下事件：{touchDowns}\n滑鼠按下事件：{mouseDowns}\n" +
            $"觸控訊息總數：{touchMessages}；讀取／關閉錯誤：{inputReadErrors}（{lastInputError}）\n" +
            $"本視窗觸控註冊：{(touchRegistered ? "成功" : "失敗，不能據此判定停用")}（{registrationError}）\n\n" +
            "請將手指點在已黑屏的副螢幕，觀察此處是否出現事件。\n" +
            "再測拖曳、長按與多指。Esc 關閉。\n\n" +
            "此區僅接收自己視窗的輸入，並未進行全域阻擋或監聽。\n" +
            "零事件不等於全系統停用；須與試驗前基準及其他情境交叉確認。";
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(30, 30,
            Math.Max(1, ClientSize.Width - 60), Math.Max(1, ClientSize.Height - 60)),
            SystemColors.WindowText, TextFormatFlags.WordBreak);
    }
}
