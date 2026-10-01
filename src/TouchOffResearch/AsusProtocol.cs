using System.Buffers.Binary;

namespace TouchOffResearch;

// Original narrow implementation of the public-source ATKACPI query ABI.
// Reference implementations and caveats are identified in SOURCES.md.
// This class intentionally exposes ONLY DSTS reads of two allowlisted IDs.
public static class AsusProtocol
{
    public const uint Ioctl = 0x0022240C;
    public const uint DstsMethod = 0x53545344;
    public const uint ScreenPadPower = 0x00050031;
    public const uint ScreenPadBrightness = 0x00050032;

    public static byte[] BuildRead(uint id)
    {
        if (id != ScreenPadPower && id != ScreenPadBrightness)
            throw new ArgumentOutOfRangeException(nameof(id), "Only two ScreenPad query IDs are allowed.");
        var buffer = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), DstsMethod);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), id);
        // Second argument is zero; do not add INIT, DEVS, arbitrary IDs, or fuzzing.
        return buffer;
    }

    public static AsusQueryResult Decode(uint id, bool ioSuccess, int win32Error,
        byte[] buffer, uint bytesReturned)
    {
        if (!ioSuccess)
            return new(id, false, win32Error, bytesReturned, null, "IoctlFailed");
        if (bytesReturned < 4 || bytesReturned > (uint)buffer.Length)
            return new(id, true, 0, bytesReturned, null, "InvalidOutputLength");
        uint raw = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4));
        string interpretation = raw switch
        {
            0xFFFFFFFE => "UnsupportedMethod",
            0xFFFFFFFF => "InvalidDeviceResponse",
            _ when (raw & 0x00010000) == 0 => "PresenceBitNotSet",
            _ when id == ScreenPadPower && (raw & 0x00000002) != 0 => "PowerStateUnknown",
            _ => "PresenceBitSet_NotProofOfTouchControl"
        };
        return new(id, true, 0, bytesReturned, $"0x{raw:X8}", interpretation);
    }
}

public record AsusQueryResult(uint DeviceId, bool IoctlSucceeded, int Win32Error,
    uint BytesReturned, string? RawHex, string Interpretation);

public record AsusProbeResult(string Status, int Win32Error, AsusQueryResult[] Queries);
