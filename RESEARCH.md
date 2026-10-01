# 研究結論與決策：2026-09-29

## 已知的實機事實（使用者回報）

- 機型 ASUS ZenBook Duo 14 UX482。
- 公司標準帳號，不能安裝 Codex／取得管理員權限。
- 專用 ScreenPad 關閉鍵未關閉顯示與觸控。
- Windows 停用副螢幕顯示後已黑屏，但仍產生誤觸。

因此黑屏、視窗隱藏與亮度歸零不是本案驗收目標；缺少的是停止觸控輸入。

## 研究發現 1：有帳號層級試驗方向，但不是單裝置

HP 員工在 2019 年支援回覆中建議以 HKCU\Software\Microsoft\Wisp\Touch\TouchGate 的 DWORD 0 關閉觸控，之後重新啟動。
這是一項歷史廠商支援建議，不是 UX482 實測，也不是普遍成功的證據。該討論後續仍有使用者操作及其他裝置問題。 [S1]

Microsoft 正式 TouchGate 文件指出 0/1 的關閉／開啟語義，但適用的配置階段是 offlineServicing。
它**沒有**承諾本原型的 HKCU 執行時寫入對所有目前 Windows build 有效。 [S2]

HKCU 的修改受該鍵 ACL 限制。程式透過一般 registry API 可以嘗試目前帳號有權限的設定，不表示繞過 OS 權限或公司政策。 [S3]

工程決策：以可恢復、明示影響整體手指觸控的 opt-in 試驗優先驗證。
成功標準是重新登入後不再誤觸，而非值變成0。
此路線不切斷觸控控制器電源，不保證手寫筆、登入畫面、安全桌面、其他帳號或喚醒手勢一併停止。

## 研究發現 2：不能再把 ScreenPadToggle 說成已找到觸控開關

G-Helper 公開程式定義：
- ScreenPadToggle = 0x00050031
- ScreenPadBrightness = 0x00050032
- ATKACPI IOCTL = 0x0022240C
- DSTS 讀取方法 = 0x53545344

其狀態查詢封包是方法ID、參數長度、裝置ID、第二參數的 little-endian DWORD 序列。[S4]
本版獨立實作最小查詢，不複製整套 G-Helper，也不納入風扇／功率／電池／GPU 控制。

Linux 主線標頭將相同 0x00050031 定義為 SCREENPAD_POWER，程式實作在 Screenpad backlight 區段。
標頭另提醒 power 控制可能只用於關閉；亮度寫入才重新開啟。
所以「寫0關閉、寫入 1必定還原」不是安全假設。 [S5][S6]

這些資料只能支持「有副螢幕／背光控制通道」，不能支持「觸控digitizer必定同步停止」。
因此 v0.1 **只讀 DSTS**，實機證據充分以前不建立硬體開／關按鈕。

## 研究發現 3：全域輸入攔截不是等價的免管理員替代

Microsoft 的 RegisterPointerInputTarget 可以全域重導向某種類型的 pointer input，但要求 UIAccess；沒有該權限回傳拒絕存取。 [S7]

Microsoft 將觸控螢幕 HID collection 列為 exclusive；觸控標準的強制 feature report 並沒有提供可對所有螢幕任意寫入的通用 TouchOff 命令。 [S8][S9]
這不否定某個廠商可能提供另外的可存取介面，但不能亂猜 HID report、把 Right Touchpad 的控制當成 ScreenPad Touchscreen。

工程決策：不採用全域滑鼠鉤子、透明遮罩、BlockInput、任意 HID 寫入或修改 UIAccess。
攔截 mouse promotion 也不等於阻止所有原生觸控、長按、多指與系統手勢。

## 研究發現 4：PnP 停用仍不是本案免權限主線

Microsoft 的 Disable-PnpDevice 明定停用裝置需要 Administrator account。包成 EXE 不會改變要求。 [S10]
因此本版不呼叫 PnP disable/enable，不安裝 filter driver 或 service。

## 唯讀診斷的判讀

| 結果 | 能推導什麼 | 不能推導什麼 |
|---|---|---|
| ATKACPI 開啟失敗，error 2/3 | 此裝置別名／路徑無法開啟 | 不能單憑此認定所有 ASUS 驅動不存在 |
| error 5 | 當下 token／ACL／政策不允許該存取 | 不是要求繞過或提權 |
| IOCTL 成功但 bytesReturned <4 | 沒有有效DWORD狀態 | 不能當0=Off |
| raw=FFFFFFFE/FFFFFFFF | 不支援或無效回覆的候選 | 不能做「減65536」後推算成合法值 |
| presence bit 未設 | 這個介面查詢不能支持功能存在 | 不宜繼續猜測不同硬體 ID |
| presence bit 已設 | 候選功能狀態可讀 | 不等於可以寫，也不等於會停觸控 |
| TouchGate寫0成功 | 設定已寫入並讀回 | 不等於Windows實際採用或控制器斷電 |

## 下一階段只在證據足夠時進行

1. 公司端回傳最小報告；不要求安裝任何開發工具。
2. TouchGate 試驗若停止誤觸、主螢幕觸控也可放棄，可將此判為「帳號層級防誤觸」方案；不是 ScreenPad-only，也不是硬體電源關閉。
3. 主螢幕觸控必須保留，則跳過 TouchGate，深入確認 UX482 現有 ASUS 元件是否公開獨立的觸控控制方法。
4. 後續硬體寫入前，先取得讀回、精確恢復與實機對照方法。不得只憑 G-Helper 常數增加寫入。
5. 找不到目前權限可用的控制方法時，記錄具體失敗點。這時 IT 針對單個 touch device 的一次性處理才是替代方向；不能用無效遮罩冒充完成。

## 主要未解問題

初始研究於 2026-09-29 尚未取得這台 UX482 的 ATKACPI、ASUS 狀態與實機效果。後續使用者已提供一般權限診斷與 DSTS 報告照片，並回報重啟與睡眠喚醒後不再誤觸、鍵盤／右側觸控板／滑鼠正常；完整證據及限制見 VALIDATION.md。

尚未取得實機精確還原、其他觸控影響或單獨 ScreenPad Touch Off 的證據，不能交付「已證實免管理員、只關副螢幕觸控」的結論。
