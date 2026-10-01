# Sources — checked 2026-09-29

Primary documentation / implementation sources. Mutable source files should be pinned to a reviewed commit before any hardware-write development.

- [S1] HP employee support reply: historical current-user TouchGate suggestion, 2019. Not UX482 validation and not a universal guarantee.
  https://h30434.www3.hp.com/t5/Notebook-Hardware-and-Upgrade-Questions/Touchscreen/td-p/7142249
- [S2] Microsoft TouchGate: values 0/1; configuration pass is offlineServicing, not a documented runtime HKCU API guarantee.
  https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/unattend/microsoft-windows-tabletpc-platform-input-core-touchgate
- [S3] Microsoft registry security and access rights.
  https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-key-security-and-access-rights
- [S4] G-Helper AsusACPI.cs: ATKACPI name, IOCTL, DSTS packet and ScreenPad IDs. Consult upstream licensing for any future source reuse; this kit does not bundle G-Helper.
  https://raw.githubusercontent.com/seerge/g-helper/main/app/AsusACPI.cs
- [S5] Linux ASUS WMI header: SCREENPAD_POWER/LIGHT and asymmetric re-enable caveat; DSTS masks.
  https://raw.githubusercontent.com/torvalds/linux/master/include/linux/platform_data/x86/asus-wmi.h
- [S6] Linux ASUS WMI implementation: Screenpad backlight code.
  https://raw.githubusercontent.com/torvalds/linux/master/drivers/platform/x86/asus-wmi.c
- [S7] Microsoft RegisterPointerInputTarget: global pointer redirect requires UIAccess.
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerpointerinputtarget
- [S8] Microsoft HID architecture: touchscreen and precision touchpad are different exclusive collections.
  https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-architecture
- [S9] Microsoft required touchscreen HID collections/features. Not a universal TouchOff command.
  https://learn.microsoft.com/en-us/windows-hardware/design/component-guidelines/touchscreen-required-hid-top-level-collections
- [S10] Microsoft Disable-PnpDevice: requires Administrator account.
  https://learn.microsoft.com/en-us/powershell/module/pnpdevice/disable-pnpdevice?view=windowsserver2025-ps
- [S11] Microsoft GetPointerDevices: information query, not disabling input.
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getpointerdevices
- [S12] Microsoft POINTER_DEVICE_INFO: mapped display is not necessarily the physical attachment. This is important when identifying ScreenPad after display disconnect.
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-pointer_device_info
- [S13] Microsoft ADMX_TouchInput: existing user/computer TurnOffTouchInput policy location. This prototype only detects the value; it never writes Policies keys.
  https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-touchinput
- [S14] Microsoft .NET application deployment: self-contained folder publishing.
  https://learn.microsoft.com/en-us/dotnet/core/deploying/
- [S15] Microsoft .NET 10 downloads. Use the supported stable SDK on the PRIVATE computer only.
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- [S16] OpenAI Codex AGENTS.md guidance; the official developer URL currently redirects to ChatGPT Learn.
  https://developers.openai.com/codex/guides/agents-md/
- [S17] Microsoft RegistryKey.SetValue.
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.setvalue?view=net-10.0

Additional implementation API references to verify during Windows code review:
- RegisterTouchWindow / GetTouchInputInfo / CloseTouchInputHandle / GetTokenInformation.
- These only serve our window's input test and non-elevation check. No global input interception is implemented.

- [S18] Microsoft RegisterTouchWindow: registers only a window owned by the calling thread.
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registertouchwindow
- [S19] Microsoft GetTouchInputInfo and TOUCHINPUT layout/flags.
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-gettouchinputinfo
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-touchinput
