# v0.1 私人端程式審查與修正（2026-09-29）

沿用 `src/TouchOffResearch` 原型、WinForms UI、ASUS 子程序查詢及既有操作流程。

| 問題 | 修正 | 證據 |
|---|---|---|
| 備份讀回僅驗證 schema/scope，未驗證保存的原值完全相符 | 比對完整 GateBackup；失敗不寫 registry | RegistryChecks：原值／時間遭改、讀回拒絕、JSON 損壞 |
| 備份與 HKCU 靜態耦合，無法安全測試失敗路徑 | 抽出 IGateRegistry、IGateBackupStore 與 TouchGateLifecycle；UI 保持原 facade | 44 項案例，測試專案不引用 Windows registry adapter |
| 有待處理備份時可能再次寫入；跨 session 無共同鎖 | ConfigureOff 遇到 pending backup 不再開始寫入；以同帳號資料夾檔案鎖序列化 | pending backup、鎖定衝突、兩個真實 FileGateBackupStore 實例測試 |
| 已是原值仍不必要地改写；還原失敗需保留原始資料 | 還原已相同時僅驗證／封存；拒絕存取、讀回與封存失敗保留備份 | absent/1、失敗後重開、刪值拒絕、外部變更、封存失敗案例 |
| 部分 interop 與 UI 初始化缺少檢查 | 驗證 TokenElevation bytesReturned 與值、保留 Win32Error；IOCTL output 明示 [Out]；Register/Unregister/Close 回傳檢查；DPI/文字初始化移至最早窗口之前 | 私人 Windows token、48-byte x64 TOUCHINPUT、實際啟動／結束；其餘 native 失敗分支為靜態審查 |

150% DPI 小視窗發現同意文字截斷，改為獨立可換行 Label 配合預設未勾選 CheckBox，支援點文字勾選與鍵盤操作。介面保留第二次確認，Cancel 為預設選項。WindowsChecks 只切換同意框，從未點擊寫入按鈕。

ASUS 維持 `DSTS` 與 `0x00050031`／`0x00050032` allowlist；無新增硬體寫入。子程序等待 stdout 也受同一 5 秒期限約束。沒有重試、提權、服務、顯示配置變更或連網 API。

## 保留限制

- 登錄 read/check/write 不是跨外部程序的原子 CAS。會拒絕觀察到的衝突，但無法辨識同值外部寫入、ABA 變動或最後檢查之後的競爭。檔案鎖只協調本工具。
- 備份採 flush-to-disk、no-overwrite rename 與完整讀回；未做實體斷電／檔案系統損毀測試，也不是對同帳號惡意修改的安全邊界。
- fake registry 不能證明實際公司 ACL、policy 或 Windows 採用 TouchGate；本輪禁止對私人 Wisp 作修改，因此真實寫入／還原仍未測。
- 沒有執行 elevated token 拒絕分支、ASUS 實體驅動／超時測試、100%／200% DPI 或企業 application control 測試；不得說全部通過。
- 未執行網路封包追蹤；「未新增連網 API」來自 source review，不能稱為動態封包驗證。

## Windows API 審查依據

- [SetCompatibleTextRenderingDefault](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcompatibletextrenderingdefault?view=windowsdesktop-10.0)：在第一個視窗前設定。
- [GetTouchInputInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-gettouchinputinfo)：檢查 BOOL、GetLastError 與每筆結構大小。

既有 `SOURCE_SHA256SUMS.txt` 與 `STRUCTURE_CHECK_RESULTS.txt` 保留為原始研究包記錄，不代表本次修改後內容。
