# 驗證狀態 — 更新至 2026-09-30

## 證據等級

| 狀態 | 本輪結果 | 範圍 |
|---|---|---|
| NotBuilt | 已解除 | 原始包只有 source；本轮已在私人 Windows 完成建置 |
| BuildPassed | 通過 | .NET SDK 10.0.401、net10.0-windows、win-x64、自含完整資料夾；warning-as-error |
| PrivateWindowsTested | 通過限定項目 | Windows 10.0.26200.0、x64、DefaultNonElevated 一般帳號 token；診斷、GUI、正常啟動與結束；備份還原只以 fake registry 測試 |
| UX482Tested | 部分完成（使用者照片與回報） | 公司 UX482 一般權限診斷、兩個 ASUS DSTS 查詢有照片證據；使用者回報重啟後不再誤觸，鍵盤、右側觸控板及滑鼠全部正常，睡眠喚醒後仍正常。其他觸控影響與實機精確還原尚未確認 |

**編譯、登錄設定或 ASUS 查詢成功都不等於觸控已停止。**

## 公司 UX482 回報 — 2026-09-30

- 使用者提供三張報告照片：版本 0.1.0、Windows 10.0.19045.0、process64Bit=true、elevated=false；一個作用中主顯示器為 1920×1080。
- 照片中的 TouchGate 均為存在且值 0，originalBackupPresent=true，policyBlocks=[]。名為 before 的報告也已呈現此狀態，不能用它重建首次寫入前的原值或時間。
- ASUS 0x00050031／0x00050032 查詢皆 IoctlSucceeded=true、Win32Error=0、BytesReturned=16，原始值分別為 0x00010001／0x0001FF52。這只證明兩個狀態可讀，不證明硬體觸控控制能力。
- configured 照片的結果為 TouchGateAlreadyZero_NotProofOfTouchDisabled；該次按鈕操作沒有重新寫入。2026-09-30 使用者另行回報「重啟後不再誤觸」。此為實際使用情境的回報，非僅由 registry 或查詢結果推定；仍不足以確定首次設定的完整因果過程、觸控控制器斷電或單獨 ScreenPad 停用。
- 在要求確認鍵盤打字、右側觸控板及滑鼠點擊／捲動後，使用者回覆「全部正常」。記錄為這三類必要輸入的使用者回報；不擴大解讀為其他觸控或完整驗收通過。
- 在要求關閉工具、睡眠再喚醒並測試下方黑屏區後，使用者回覆「喚醒後仍正常」。記錄為本次睡眠／喚醒情境下無誤觸的使用者回報；未獨立量測，未延伸為所有電源狀態或安全桌面的保證。
- 尚未取得主螢幕／外接觸控、長按／多指、獨立鎖定解鎖及真實備份精確還原的結果。現有原始備份應保留在公司電腦，不傳出備份或其 scope identifiers。

這次僅更新工作區驗證紀錄；2026-09-29 發佈 ZIP、內含文件與既有 SHA-256 清單保持原樣。

## 實際執行的檢查

1. `tests/ProtocolChecks`：11／11 斷言通過。兩個 DSTS 封包、禁止 ID、拒絕存取、空／超長回覆及狀態解析；不存取 registry 或硬體。
2. `tests/RegistryChecks`：44／44 案例通過。涵蓋原始缺值／DWORD 1／DWORD 0、型態與数值拒絕、精確還原、pending backup、scope/schema、備份寫入／讀回失敗、registry 拒絕存取、寫入／還原讀回不符、外部變更、政策拒絕、封存失敗與鎖定衝突。另以隔離暫存目錄執行正式 JSON/file adapter 的讀寫、no-overwrite、跨實例鎖與損壞 JSON 檢查。全部使用 fake registry。
3. `tests/WindowsChecks`：21／21 檢查通過。實際 token 非提升、TokenElevationType=1、PerMonitorV2 初始化、x64 TOUCHINPUT=48 bytes、預設同意框與按鈕、診斷未宣稱停觸控、不含備份識別、兩種視窗尺寸、空白測試區開關，以及發佈 EXE 實際啟動／正常退出。
4. 私人環境實際 DPI=144（150%），檢查 890×620 及 700×490 client size，並檢視工具自身 DrawToBitmap 圖片。修正小視窗同意文字截斷。沒有改 Windows 顯示配置。
5. 真實 HKCU TouchGate 僅讀取，UI 測試及 EXE 啟閉前後快照相同（原本缺值，仍然缺值）。沒有建立真實 TouchGate 備份、沒有 ASUS 呼叫、沒有重啟或登出。

## 產物與重現

- 完整成品：`artifacts/TouchOffResearch-v0.1.0-win-x64/`
- 可攜 ZIP：`artifacts/TouchOffResearch-v0.1.0-win-x64.zip`
- ZIP SHA-256：ZIP 同路徑加 `.sha256.txt`；成品逐檔清單：資料夾內 `SHA256SUMS.txt`。
- 私人端證據：`artifacts/validation/protocol-results.txt`、`registry-results.txt`、`windows-results.txt`、`publish-results.txt`、`private-diagnostics.json`、`main-*.png`。私人診斷及圖片不放入公司發佈 ZIP。
- 本輪 `powershell.exe -NoProfile -File scripts/Publish-Private.ps1` 遭私人端腳本執行原則拒絕，未修改政策。採 README 既有的直接 `dotnet run`／`dotnet publish` 指令完成測試與建置，再用標準檔案複製、雜湊與 ZIP 操作封裝；沒有使用 ExecutionPolicy Bypass、解鎖腳本或載入被阻擋的腳本內容。
- 重現建置指令：`dotnet publish src/TouchOffResearch/TouchOffResearch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -warnaserror -o <new-release-directory>`。完整腳本只供現有政策允許的私人環境；公司端只解壓完整成品、雙擊 EXE，無需 Codex、SDK、Python 或額外 .NET runtime。

## 尚未驗證

- **UX482 部分實測已回報：重啟後不再誤觸，鍵盤、右側觸控板及滑鼠正常，睡眠喚醒後仍正常。** 一般權限診斷與 ASUS DSTS 有照片證據；首次真實 TouchGate 寫入過程、精確還原、獨立鎖定解鎖、其他觸控與手寫筆尚未確認。不能將部分回報標為完整 UX482 驗收通過。
- 100%／200% 實際 DPI、跨螢幕 DPI 切換、實際 elevated token 的拒絕分支、ASUS worker 超時／無法終止情況：未測；不為測試要求 UAC。
- 未做網路封包動態追蹤。Source review 未發現連網 API，不能改寫為封包測試通過。
- 未做突然斷電、磁碟故障、同值外部變更或最後一次檢查後的競爭測試。登錄操作不能對外部程序提供原子 compare-and-swap 保證。
- 公司執行核准、白名單、簽章與資安掃描未完成。成品未簽章；遇公司政策阻擋應停止，不改政策或使用繞過方式。

原始 `STRUCTURE_CHECK_RESULTS.txt` 的 21 項 Python 結構檢查與 `SOURCE_SHA256SUMS.txt` 是先前研究包記錄；本輪沒有把它們當作新程式的執行測試證據。
