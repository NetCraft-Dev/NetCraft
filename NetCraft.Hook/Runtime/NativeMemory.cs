using System.Runtime.InteropServices;

namespace NetCraft.Hook.Runtime;

internal static class NativeMemory
{
    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("libc")]
    private static extern int mprotect(IntPtr addr, UIntPtr len, int prot);

    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const int PROT_READ = 0x1;
    private const int PROT_WRITE = 0x2;
    private const int PROT_EXEC = 0x4;

    public static bool MakeWritable(IntPtr address, int size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return VirtualProtect(address, (UIntPtr)size, PAGE_EXECUTE_READWRITE, out _);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            var pageSize = Environment.SystemPageSize;
            var pageStart = (long)address & ~(pageSize - 1);
            var pageEnd = ((long)address + size + pageSize - 1) & ~(pageSize - 1);
            var pageCount = (UIntPtr)(pageEnd - pageStart);
            return mprotect((IntPtr)pageStart, pageCount, PROT_READ | PROT_WRITE | PROT_EXEC) == 0;
        }

        return false;
    }
}

internal static class PlatformInfo
{
    public static bool IsX64 => IntPtr.Size == 8 &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);

    public static bool IsArm64 => IntPtr.Size == 8 &&
        (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.Arm);

    public static int JumpSize => IsArm64 ? 16 : 14;

    public static int PatchSize => IsArm64 ? 16 : 32;
}
