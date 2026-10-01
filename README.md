# UX482 TouchOff Research

ASUS UX482 ScreenPad Plus 防誤觸研究原型，使用 C#／.NET 10 WinForms。完整操作方式見 [繁體中文使用文件](README_zh-TW.md)。

## 功能與範圍

- 一般帳號診斷目前 TouchGate、有限政策狀態及匿名顯示器幾何。
- ASUS 僅查詢兩個 allowlisted DSTS 狀態：`0x00050031`、`0x00050032`；不發送硬體寫入。
- TouchGate 是預設未啟用、須明確同意的帳號層級手指觸控試驗。可能影響主螢幕及外接觸控螢幕。
- 修改前保存並讀回精確原值；保留缺值與 DWORD 0／1 差異，拒絕待處理備份、觀察到的外部衝突及拒絕存取。
- 發佈 Windows x64 self-contained 完整資料夾，使用 `asInvoker`、`uiAccess=false`。不安裝服務、驅動或開機常駐元件。

公司電腦只使用核准的完整 portable 成品，不需要 Codex、SDK、Python 或額外 .NET runtime。遇到公司政策阻擋應停止；不變更政策或要求提權。

## 驗證狀態

| 等級 | 已取得的證據 |
|---|---|
| BuildPassed | 私人 Windows 的 .NET 10 編譯與 win-x64 self-contained 發佈 |
| PrivateWindowsTested | ProtocolChecks 11／11、RegistryChecks 44／44、WindowsChecks 21／21；測試未修改真實 Wisp |
| UX482Tested（部分） | 使用者提供一般權限診斷及 ASUS DSTS 報告照片，回報重啟後不再誤觸、鍵盤／右側觸控板／滑鼠正常、睡眠喚醒後仍正常 |

**實機精確還原、其他觸控影響及完整驗收仍待確認。**使用者回報限於測試的電腦、帳號與情境；不能推導為 ScreenPad-only 開關、觸控控制器斷電或通用相容性保證。最新紀錄見 [VALIDATION.md](VALIDATION.md)。

## 下載與公司端使用

從此儲存庫的 Releases 下載 `TouchOffResearch-v0.1.0-win-x64.zip` 及 SHA-256 檔，完整解壓縮後，以一般帳號開啟 `TouchOffResearch.exe`。保留整個資料夾，不可只複製 EXE。

先執行「重新診斷」，保留 TouchGate 同意框未勾選。要進行可選試驗與手動重啟／還原，請依 [README_zh-TW.md](README_zh-TW.md) 與 [TEST_PLAN.md](TEST_PLAN.md) 操作。

備份保留在原電腦、原帳號的 `%LOCALAPPDATA%\UX482TouchResearch`。關閉或刪除程式不會還原設定。不要刪除、修改或匯出原始備份。

## 私人端開發

在私人 Windows 電腦使用 .NET 10 SDK；公司端不執行以下指令。

```powershell
dotnet run --project tests/ProtocolChecks/ProtocolChecks.csproj -c Release
dotnet run --project tests/RegistryChecks/RegistryChecks.csproj -c Release
dotnet publish src/TouchOffResearch/TouchOffResearch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -warnaserror -o artifacts/TouchOffResearch-v0.1.0-win-x64
dotnet run --project tests/WindowsChecks/WindowsChecks.csproj -c Release -- artifacts/TouchOffResearch-v0.1.0-win-x64/TouchOffResearch.exe artifacts/validation
```

`RegistryChecks` 使用 fake registry 及隔離暫存檔案；`WindowsChecks` 開啟工具自己的 GUI、讀取診斷並正常關閉，不按 TouchGate 寫入按鈕，不呼叫 ASUS。

可在現有執行原則允許的私人環境使用 `scripts/Publish-Private.ps1`，不繞過執行原則。發佈目錄已存在時，請選擇新的輸出目錄，避免混入舊成品。

## 文件

- [研究與來源](RESEARCH.md)、[SOURCES.md](SOURCES.md)
- [程式審查](CODE_REVIEW.md)、[測試計畫](TEST_PLAN.md)
- [最新驗證紀錄](VALIDATION.md)

`SOURCE_SHA256SUMS.txt` 與 `STRUCTURE_CHECK_RESULTS.txt` 是原始研究包的歷史記錄。Release 的逐檔 SHA-256 位於 ZIP 內的 `SHA256SUMS.txt`；2026-09-29 portable ZIP 內文件是當時的驗證快照，後續實機回報以本儲存庫的 `VALIDATION.md` 為準。
