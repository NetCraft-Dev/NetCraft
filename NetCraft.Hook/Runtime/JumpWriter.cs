using System.Runtime.InteropServices;

namespace NetCraft.Hook.Runtime;

internal static class JumpWriter
{
    public static byte[] BuildJump(IntPtr from, IntPtr to)
    {
        if (PlatformInfo.IsArm64)
            return BuildArm64Jump(to);
        return BuildX64Jump(to);
    }

    private static byte[] BuildX64Jump(IntPtr to)
    {
        var bytes = new byte[14];
        bytes[0] = 0xFF;
        bytes[1] = 0x25;
        bytes[2] = 0x00;
        bytes[3] = 0x00;
        bytes[4] = 0x00;
        bytes[5] = 0x00;
        BitConverter.TryWriteBytes(bytes.AsSpan(6, 8), to.ToInt64());
        return bytes;
    }

    private static byte[] BuildArm64Jump(IntPtr to)
    {
        var bytes = new byte[16];
        var targetAddr = to.ToInt64();

        BitConverter.TryWriteBytes(bytes, targetAddr);
        bytes[8] = 0x5E;
        bytes[9] = 0x00;
        bytes[10] = 0x9F;
        bytes[11] = 0xD6;
        bytes[12] = 0x00;
        bytes[13] = 0x00;
        bytes[14] = 0x00;
        bytes[15] = 0x00;
        return bytes;
    }

    public static byte[] BuildTrampolineJump(IntPtr from, IntPtr to)
    {
        return BuildJump(from, to);
    }
}
