# Test plan

## Evidence levels

1. Source/static review.
2. Build + protocol unit tests on private computer.
3. Private Windows standard-account execution and fake registry lifecycle tests.
4. UX482 standard-account diagnostics.
5. UX482 explicit opt-in change, manual restart, physical comparison and successful restore.

Never label a lower level as a higher one. See VALIDATION.md for executed checks; this plan itself is not evidence.

## Before company delivery: private computer

- Build net10.0-windows win-x64 self-contained.
- Run the 11 protocol assertions in tests/ProtocolChecks.
- Launch under a standard Windows account, not an elevated terminal.
- An elevated launch must refuse execution; it must not attempt to relaunch itself.
- Non-ASUS PC: optional ASUS probe should report OpenFailed cleanly without hardware writes.
- Inspect UI at 100%, 150%, 200% DPI and small laptop resolution; no clipped consent/restore buttons.
- Add injected registry storage tests BEFORE recommending the write test: absent value, 1, 0, wrong type, unexpected value, backup exists, save failure, write failure, restore failure, scope mismatch, external modification and policy access denial.
- A failed backup must leave real TouchGate unchanged.
- Run `dotnet run --project tests/RegistryChecks -c Release`: injected registry lifecycle plus isolated production JSON/file-store checks; no real Wisp writes.
- Run `dotnet run --project tests/WindowsChecks -c Release -- <release-exe> <evidence-directory>`: non-elevated token, default consent, read-only diagnostics, actual EXE launch/close, and before/after Wisp snapshot.
- A successful restore must preserve absent-versus-value semantics.
- Do not run mutation tests against the private computer's real Wisp settings; use a fake store or separately scoped test key after refactoring.
- Confirm no network connections are initiated by the program.
- Confirm full release directory, manifest, dependencies and SHA-256 are correct.

## UX482 first pass: no changes

| ID | Action | Record |
|---|---|---|
| D01 | Launch by double-click, normal account | Starts / UAC / blocked / missing component |
| D02 | Refresh diagnostics | Windows build, anonymous active screens, current TouchGate and policy result |
| D03 | Open blank input test window | Touch registration result; baseline lower-screen taps cause observable input or not |
| D04 | ASUS read-only query, once | Open status, Win32 error, raw DSTS/bytes; no assumption about touch |
| D05 | Preview/export minimal report | No user name, document, device serial or company content |

No need to repeat the already-failed dedicated ScreenPad button or Windows display-off experiment.
Input-test baseline must reproduce the problem. If touches do not land on the test area, a later zero count is not valid evidence of success.

## Optional account-wide TouchGate experiment

Only when the user explicitly accepts main-screen and other touchscreen finger input may also stop.
Do not use this as the single-device path.

| ID | Action | Success criterion |
|---|---|---|
| T01 | Back up, then set TouchGate=0 | Correct original saved; readback0; status says pending, not disabled |
| T02 | Save work and manually restart | No automatic restart or logout |
| T03 | Log in, keep lower display off, tap all areas | Lower touches no longer generate unwanted actions |
| T04 | Drag, long-press, two-finger and multi-finger tests | No unwanted pointer/click/scroll/gesture in normal working context |
| T05 | Test physical mouse and right-side touchpad | Remain functional; failure requires rollback |
| T06 | Test upper touchscreen if present | Record actual effect, do not silently assume preserved |
| T07 | Test after sleep/wake and lock/unlock | Record results independently; don't claim login/secure desktop coverage |
| T08 | Close the tool and continue working | Behavior does not rely on the tool staying open, if this setting is honored |
| T09 | Restore original via tool, manually restart | Original behavior returns; exact registry state restored |
| T10 | After any company policy/driver/OS refresh | Revalidate; never repeatedly enforce against changed policy |

If a stylus is used, test it separately. Finger suppression is not proof of pen suppression.
Do not open confidential applications to collect screenshots or crash dumps. The tool's own blank area and a blank document are sufficient for baseline tests.

## Minimal user feedback template

Version:
Windows build:
Launch under standard account:
ASUS open/result (if tested):
Original TouchGate: absent / 0 / 1 / unknown
Opt-in TouchGate trial: not run / ran
Manual restart completed:
Lower-screen accidental touch: still present / absent / cannot judge
Upper-screen touch (if present): works / stopped / not tested
Right touchpad and mouse:
Sleep/wake result:
Restore result:

Return only company-approved, inspected minimal information. Do not return the original-setting backup.
