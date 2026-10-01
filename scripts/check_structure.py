"""Private-side structural checks ONLY. Not a C# compiler or hardware test."""
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
checks = []
def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)

manifest = ET.parse(root / 'src/TouchOffResearch/app.manifest')
level = manifest.find('.//{urn:schemas-microsoft-com:asm.v3}requestedExecutionLevel')
check(level is not None and level.attrib.get('level') == 'asInvoker', 'manifest asInvoker')
check(level.attrib.get('uiAccess') == 'false', 'manifest no UIAccess')
project = ET.parse(root / 'src/TouchOffResearch/TouchOffResearch.csproj')
check(project.findtext('.//TargetFramework') == 'net10.0-windows', 'Windows target')
check(project.findtext('.//PublishSingleFile') == 'false', 'folder deployment')
check(project.findtext('.//PublishTrimmed') == 'false', 'no trimming')
source = '\n'.join(p.read_text() for p in (root/'src').rglob('*.cs'))
for forbidden in ['SetWindowsHookEx(', 'BlockInput(', 'Disable-PnpDevice',
                  'CM_Disable_DevNode(', 'HttpClient(', 'WebClient(', 'SendInput(']:
    check(forbidden not in source, 'no direct call: ' + forbidden)
protocol = (root/'src/TouchOffResearch/AsusProtocol.cs').read_text()
check('DstsMethod = 0x53545344' in protocol, 'DSTS allowlisted method')
check('id != ScreenPadPower && id != ScreenPadBrightness' in protocol, 'two-ID query allowlist')
for value, expected in [(0x50031, '44535453080000003100050000000000'),
                        (0x50032, '44535453080000003200050000000000')]:
    actual = struct.pack('<IIII', 0x53545344, 8, value, 0).hex().upper()
    check(actual == expected, 'independent packet vector ' + hex(value))
    check(expected in (root/'tests/ProtocolChecks/Program.cs').read_text(), 'C# test contains expected vector')
store = '\n'.join((root/'src/TouchOffResearch'/name).read_text(encoding='utf-8-sig') for name in
    ['TouchGateStore.cs', 'FileGateBackupStore.cs', 'TouchGateLifecycle.cs'])
check('File.Move(temporary, BackupPath, overwrite: false)' in store, 'backup no overwrite')
check('key?.DeleteValue(ValueName' in store, 'restore absent value supported')
check('Configured_PendingManualRestartAndPhysicalTest' in store, 'configuration is not physical success')
print('STRUCTURAL / INDEPENDENT VECTOR CHECKS ONLY')
for label in checks:
    print('PASS:', label)
print(f'{len(checks)} checks; C# compilation, .NET test execution and UX482 hardware are NOT verified.')
