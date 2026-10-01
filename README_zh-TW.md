# UX482 TouchOff Research v0.1

建立日期：2026-09-29。交付類型：**研究記錄 + C# 原型原始碼 + Codex 任務**。
**2026-09-29 私人端已完成編譯、11 項協定斷言、44 項 fake-registry／檔案案例，以及一般帳號 Windows 啟動檢查。後續 UX482 使用者回報重啟與睡眠喚醒後不再誤觸，鍵盤、右側觸控板及滑鼠正常；精確還原與其他觸控影響仍待確認。**
完整 portable 成品位於 `artifacts/TouchOffResearch-v0.1.0-win-x64.zip`；驗證範圍見 `VALIDATION.md`。
GitHub 使用者請從此儲存庫的 Releases 下載完整 ZIP 與 SHA-256 檔；`artifacts` 是私人端建置目錄，不納入 Git。

## 目的

公司 ASUS ZenBook Duo 14 UX482 的 ScreenPad Plus 已經透過 Windows 停用顯示，
但下方觸控仍會干擾操作；專用 ScreenPad 關閉鍵也無效。
公司電腦不能安裝 Codex 或取得管理員權限。開發與編譯只在私人電腦完成。

本次不開發視窗管理、亮度面板或 ScreenXpert 替代品。只研究停止誤觸。

## 先理解兩條不同路線

| 路線 | 本版內容 | 範圍與限制 |
|---|---|---|
| ASUS 裝置介面 | 僅 DSTS 狀態查詢，不寫硬體 | 可提供是否能開啟 ATKACPI、Win32 錯誤碼與兩個 ScreenPad 原始狀態；**沒有證明能關觸控**。 |
| HKCU TouchGate | 可選、可備份還原的設定試驗 | **不是只停用副螢幕**，可能影響目前帳號的主螢幕及外接觸控螢幕。**不是控制器斷電**。 |

TouchGate 的 HKCU 設定方式出自 HP 員工的歷史技術支援建議；Microsoft 的正式 TouchGate 文件是離線部署設定，
不能把它延伸成對所有 Windows 11 build／UX482 的即時 API 保證。見 RESEARCH.md、SOURCES.md。

主螢幕觸控必須保留時：只執行診斷及 ASUS 唯讀查詢，**不要勾選整體觸控試驗**。
本版不修改 Windows 已有的副螢幕停用設定。

## 私人電腦：交給 Codex

1. 將本包解壓到私人電腦的專案資料夾。
2. 讓 Codex 閱讀 `AGENTS.md`、`RESEARCH.md`、`CODEX_START.md`。
3. 本包已包含實作原始碼，不必從空白專案重新建立。
4. 要求 Codex 先檢查原始碼、完成建置與測試，再發佈 portable ZIP。
5. 已完成的私人端檢查不能取代公司 UX482 的核准、相容性與觸控實測。

私人 Windows 電腦安裝受支援的 .NET 10 SDK 後，直接執行：

```powershell
dotnet run --project tests/ProtocolChecks/ProtocolChecks.csproj -c Release
dotnet run --project tests/RegistryChecks/RegistryChecks.csproj -c Release
dotnet publish src/TouchOffResearch/TouchOffResearch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/TouchOffResearch-v0.1.0-win-x64
```

也可在私人端依現有政策執行 `scripts/Publish-Private.ps1`。
腳本會建立 release 目錄、SHA-256 清單及 ZIP。不使用 ExecutionPolicy Bypass。
發佈腳本也會執行 `tests/WindowsChecks`，開啟工具自己的 GUI 後結束，並將檢查結果保留在私人端 `artifacts/validation`。
測試不寫真實 Wisp、不呼叫 ASUS 裝置。公司端不需也不應執行測試或建置腳本。
SDK／執行環境／相依元件僅在私人端準備。公司端傳入**整個發佈資料夾**，不可只複製 EXE。

Self-contained 會隨成品包含 .NET runtime，但仍須符合 Windows 支援範圍與原生系統相依條件。
原型為 Windows x64；私人電腦若不是 Windows，需要可實際驗證 WinForms 的私人 Windows 環境。

## 公司電腦：第一輪

前提是公司允許執行與測試這類自製程式；沒有 UAC 提示不代表已取得公司許可。

1. 用一般帳號雙擊 `TouchOffResearch.exe`。**不要用「以系統管理員身分執行」**。
2. 按「重新診斷」。這不會寫 TouchGate、改顯示配置或呼叫 ASUS 驅動。
3. 按「開啟空白輸入測試區」。在改設定前確認下方螢幕的誤觸確實能在測試區重現。
4. 按「ASUS 唯讀查詢」，同意一次查詢後查看結果。這只發出兩個 DSTS 查詢。
5. 匯出最小報告，先自行檢視，依公司允許的方式帶回私人端。

ASUS 查詢會以 read/write 權限嘗試開啟現有裝置 handle，**但程式只發出讀取狀態的 DSTS 請求**。
因此是「不發送變更命令」，不是聲稱取得 Windows read-only handle。
拒絕存取後不調整 ACL、不提權、不換驅動。子程序查詢超時後不自動重試。

## 可選：接受主螢幕觸控也停用時才做 TouchGate 試驗

1. 確認鍵盤、右側觸控板或滑鼠可正常操作。
2. 勾選明確提示影響全部手指觸控的核取方塊。
3. 按「試驗 TouchGate 關閉」，閱讀第二次確認。
4. 程式先保存原值，再設定 **HKCU\Software\Microsoft\Wisp\Touch\TouchGate = DWORD 0**。
5. 關閉工具，儲存工作，**手動重新啟動 Windows**。
6. 重新登入後，照 TEST_PLAN.md 驗證下方觸控、主螢幕觸控、右側觸控板及滑鼠。

成功寫入的狀態只會標記 `Configured_PendingManualRestartAndPhysicalTest`，不會寫「觸控已關閉」。
程式不自動重開機、不登出、不建立常駐或開機啟動。

**設定可能持續存在：關閉工具或刪除 EXE，不會自動恢復 TouchGate。**
不能把目前帳號的觸控輸入設定當成開機前、登入畫面、UAC 安全桌面或所有帳號的保證。
指尖輸入受控不等於主動式手寫筆也停用；有使用手寫筆時需另測。

## 還原

重新開啟同一台電腦、同一帳號的工具，按「還原原始 TouchGate 設定」。
然後儲存工作、手動重新啟動並驗證。

原本沒有 TouchGate：只刪除本工具新增的值。
原本為 DWORD 1：恢復 DWORD 1。
原本為 DWORD 0：不再改寫，不據此宣稱有效。
非預期型態／數值：停止，不覆蓋。

備份保存在 `%LOCALAPPDATA%\UX482TouchResearch\TouchGate.original.json`。
不要刪除或自行修改備份；不要把備份當成診斷報告傳走。
還原成功會保留一份本地 `.restored...json` 存檔，新的試驗再重新保存當下原值。
若已有待處理備份，工具不會再寫入新的試驗設定；應先執行還原。原值已相同時，還原只驗證並封存備份。
操作期間使用本機檔案鎖避免本工具的多個程序同時修改。外部程式若寫回相同數值，或在最後檢查與寫入之間改值，不能保證偵測；不要與其他設定工具同時操作。
可能留下空的 Touch 登錄機碼，不會刪除其他值或整個父機碼。

若現有公司政策、ACL 或第三方設定變動導致拒絕還原，程式會停止；不要改權限或覆蓋政策。
備份遺失／毀損時不要直接猜測原值為 1；將局部資訊交 IT 處理。

## 範圍與隱私

本版沒有網路請求、雲端 API、遙測、螢幕截圖、記憶體 dump、鍵盤全域鉤子或全域輸入監聽。
空白輸入測試區僅處理該視窗收到的事件，沒有讀取其他程式的輸入。
報告包含 Windows 版本、匿名的作用中螢幕幾何、pointer 裝置數量、TouchGate 值、局部政策狀態、ASUS 狀態與錯誤碼。
不包含文件名稱、視窗標題、程序清單、IP、帳號、主機名或 SID。
備份的帳號／電腦範圍以本地 hash 綁定；原始 SID 及主機名只用於該本地計算，從不輸出。
此種去識別化不等於允許外傳，仍依公司規範處理。

已列出的政策檢查不是完整的企業政策偵測器。沒有偵測到政策也不是核准。

## 本包狀態

請閱讀 `VALIDATION.md` 與 `CODE_REVIEW.md`。狀態為 **BuildPassed／PrivateWindowsTested（限定已列項目）／UX482Tested：部分完成（使用者回報，精確還原及其他觸控影響待驗證）**。
本工具是可驗證原型，不是 ASUS 官方軟體，也不是保證成功的 ScreenPad-only 觸控開關。
