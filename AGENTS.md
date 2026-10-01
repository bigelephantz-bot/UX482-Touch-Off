# Project scope

Read README_zh-TW.md, RESEARCH.md, TEST_PLAN.md and VALIDATION.md first.
Respond to this user in Traditional Chinese. The user is a server hardware R&D manager.

The user's company UX482 ScreenPad Plus is black after Windows display disconnect but still produces unwanted touch input.
Its dedicated ScreenPad key did not turn off display or touch. Do not ask the user to repeat either test.
No Codex, SDK, administrator credentials or development dependencies may be installed on the company computer.
All development/building happens on the private computer; only an approved portable release goes to the company PC.

# Constraints

- Do not expand into window managers, ScreenXpert clone, fan/power/GPU control, remote management or agents on the company PC.
- Preserve the existing display configuration in v0.1.
- Keep asInvoker and uiAccess=false. The prototype deliberately refuses an elevated token.
- Do not invoke UAC, install services/drivers, alter device ACLs or enterprise policy, or bypass application/script controls.
- Check all native return values AND relevant error codes AND bytes returned.
- ASUS queries are ONLY DSTS for 0x00050031 and 0x00050032. No DEVS, INIT, unknown IDs, brute force, firmware writes or HID report guessing.
- A successful query is NOT proof that touch can be disabled.
- ScreenPad power ID 0x00050031 is a backlight/screen control candidate, NOT a verified touch switch.
- Do not assume setting power to 1 reverses an off command; public kernel comments describe brightness writes re-enabling the screen.
- TouchGate is an OPTIONAL account-wide experiment, not a ScreenPad-only switch; explicit user consent must stay default-off.
- Only write HKCU\\Software\\Microsoft\\Wisp\\Touch\\TouchGate. Do not replace it with HKLM or a Policies key.
- Back up the precise original state before first write. Preserve absent vs DWORD 0/1. Refuse other kinds/values.
- Save original durably and read it back before registry modification. Never overwrite a pending original backup.
- Restore only our setting if no conflicting external change is detected. Refuse policy/ACL conflicts rather than trying alternatives.
- No automatic restart, logoff, auto-start, scheduled task, persistent service or network traffic.
- The optional ASUS worker has a timeout; do not retry timed-out or denied access automatically.
- Logs must stay local, minimal, previewable and manually exportable; do not export backup scope identifiers.

# First work

1. Review and compile the supplied implementation instead of generating a different architecture.
2. Fix compile errors, warnings relevant to safety/interop, x64 marshalling and Windows initialization.
3. Run the pure .NET protocol tests; add fake-registry unit tests for backup/restore/error paths before recommending the TouchGate write experiment.
4. Validate on a PRIVATE Windows standard account: launch without UAC, diagnostics, UI labels, backup/restore and non-admin behavior.
5. Build a complete net10.0-windows win-x64 self-contained folder. Do not ship just the EXE or force a single-file bundle.
6. Emit exact artifact paths and SHA-256 checksums. Keep source/build validation separate from UX482 hardware evidence.

# Reporting

Always distinguish NotBuilt / BuildPassed / PrivateWindowsTested / UX482Tested.
Never mark physical touch disabled based solely on registry value, driver status, return=1 or zero events in a window.
If a company policy blocks execution, stop and report it. This is not an instruction to defeat the policy.
