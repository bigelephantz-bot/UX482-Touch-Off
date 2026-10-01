using System.Buffers.Binary;
using TouchOffResearch;

int assertions = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    assertions++;
}
byte[] power = AsusProtocol.BuildRead(AsusProtocol.ScreenPadPower);
Check(Convert.ToHexString(power) == "44535453080000003100050000000000", "power request bytes");
Check(Convert.ToHexString(AsusProtocol.BuildRead(AsusProtocol.ScreenPadBrightness)) ==
    "44535453080000003200050000000000", "brightness request bytes");
bool refused = false;
try { AsusProtocol.BuildRead(0x00100011); } catch (ArgumentOutOfRangeException) { refused = true; }
Check(refused, "touchpad ID is rejected");
var buffer = new byte[16];
Check(AsusProtocol.Decode(AsusProtocol.ScreenPadPower, false, 5, buffer, 0).Win32Error == 5,
    "access denied retained");
Check(AsusProtocol.Decode(AsusProtocol.ScreenPadPower, true, 0, buffer, 0).Interpretation ==
    "InvalidOutputLength", "empty successful ioctl is not a valid response");
Check(AsusProtocol.Decode(AsusProtocol.ScreenPadPower, true, 0, buffer, 32).Interpretation ==
    "InvalidOutputLength", "oversized response rejected");
foreach (var (value, expected) in new (uint, string)[] {
    (0xFFFFFFFE, "UnsupportedMethod"), (0xFFFFFFFF, "InvalidDeviceResponse"),
    (0, "PresenceBitNotSet"), (0x00010002, "PowerStateUnknown"),
    (0x00010001, "PresenceBitSet_NotProofOfTouchControl") })
{
    BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
    Check(AsusProtocol.Decode(AsusProtocol.ScreenPadPower, true, 0, buffer, 4).Interpretation == expected,
        "decode " + value.ToString("X8"));
}
Console.WriteLine($"{assertions} assertions passed. No registry or hardware was accessed.");
