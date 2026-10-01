# 貼給私人電腦 Codex 的第一輪指令

請先閱讀 AGENTS.md、README_zh-TW.md、RESEARCH.md、TEST_PLAN.md、VALIDATION.md。

這不是從零開發，也不是完整 ScreenXpert 替代品。請基於 src 既有原型進行 code review、編譯與修正。

目標：公司 ASUS UX482 的副螢幕已透過 Windows 關閉顯示，但黑屏後仍會誤觸。
公司電腦不能安裝 Codex 或用管理員權限；開發、SDK 與編譯只在這台私人電腦完成。

本輪交付要求：
1. 使用 Windows x64 一般權限，manifest 維持 asInvoker / uiAccess=false。
2. 保留 GUI 的診斷、ASUS 唯讀查詢、空白輸入測試區、匯出報告。
3. TouchGate 預設不修改。只有明確接受全部螢幕手指觸控都可能停用後，才做可備份還原試驗。
4. 不安裝驅動／服務，不送未知 IOCTL，不改系統政策或 ACL，不自動重開機。
5. 先執行 tests/ProtocolChecks。補上可注入 fake-registry 的備份、還原、權限拒絕與外部修改測試；不要為測試修改私人電腦真正的 Wisp 設定。
6. 先確保「原值不存在、原值1、原值0、非DWORD、備份存在、備份失敗、寫入失敗、還原衝突」都有測試。
7. 在私人 Windows 標準帳號驗證 GUI、互通呼叫及非提權啟動，再 publish 成完整 self-contained ZIP。
8. 回覆真實產物路徑、SHA-256、實際跑過的測試與尚未驗證項目。

注意：這包原始碼尚未完成 .NET 編譯；不要假定已可執行。
ASUS 0x00050031 不得標示為觸控開關，亦不得假設寫入 1一定恢復。
不要在本輪加入硬體寫入。先用公司端報告確認 ATKACPI 是否可存取及狀態是否有效。
不要把編譯成功、設定寫入成功或 DSTS 成功當成 UX482 觸控停止。
