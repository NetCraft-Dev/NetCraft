using System.Runtime.InteropServices;
using System.Text;

namespace NetCraft.Interop;

//InteropRuntime interop runtime utilities
//Provides UTF-8/UTF-16 encoding conversion and platform info queries
//Platform-specific P/Invoke is forbidden, all APIs use the .NET cross-platform standard library
public static class InteropRuntime
{
    //StringToUtf8NativeAlloc allocates a UTF-8 byte string with NativeMemory
    //The caller must free the returned byte buffer with NativeMemory.Free
    public static unsafe byte* StringToUtf8NativeAlloc(string s)
    {
        var byteCount = Encoding.UTF8.GetByteCount(s);
        var ptr = (byte*)NativeMemory.Alloc((nuint)(byteCount + 1), 1);
        fixed (char* src = s)
        {
            Encoding.UTF8.GetBytes(src, s.Length, ptr, byteCount);
        }
        ptr[byteCount] = 0;
        return ptr;
    }

    //Utf8PtrToString converts a UTF-8 byte buffer allocated with NativeMemory to a .NET string
    public static unsafe string Utf8PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        int len = 0;
        while (ptr[len] != 0) len++;
        return Encoding.UTF8.GetString(ptr, len);
    }

    //StringToUtf8Managed converts to UTF-8 in a managed byte[] without allocating unmanaged memory
    public static byte[] StringToUtf8Managed(string s)
        => Encoding.UTF8.GetBytes(s);

    //Utf8ManagedToString converts a managed byte[] to a .NET string
    public static string Utf8ManagedToString(byte[] bytes)
        => Encoding.UTF8.GetString(bytes);

    //StringToUtf16NativeAlloc allocates a UTF-16 string with NativeMemory
    public static unsafe char* StringToUtf16NativeAlloc(string s)
    {
        var charCount = s.Length;
        var ptr = (char*)NativeMemory.Alloc((nuint)(charCount + 1) * 2, 2);
        fixed (char* src = s)
        {
            Buffer.MemoryCopy(src, ptr, (nuint)(charCount + 1) * 2, (nuint)charCount * 2);
        }
        ptr[charCount] = '\0';
        return ptr;
    }

    //Utf16PtrToString converts a UTF-16 char* to a .NET string
    public static unsafe string Utf16PtrToString(char* ptr)
    {
        if (ptr == null) return string.Empty;
        int len = 0;
        while (ptr[len] != '\0') len++;
        return new string(ptr, 0, len);
    }

    //IsWindows whether running on Windows
    public static bool IsWindows => OperatingSystem.IsWindows();

    //IsLinux whether running on Linux
    public static bool IsLinux => OperatingSystem.IsLinux();

    //IsMacOS whether running on macOS
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    //PlatformSuffix platform-specific dynamic library suffix (dll/so/dylib)
    public static string PlatformSuffix
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "dll";
            if (OperatingSystem.IsMacOS()) return "dylib";
            return "so";
        }
    }

    //PlatformPrefix platform-specific dynamic library prefix (lib on Linux/macOS, none on Windows)
    public static string PlatformPrefix
    {
        get
        {
            if (OperatingSystem.IsWindows()) return string.Empty;
            return "lib";
        }
    }

    //PlatformLibraryName composes a dynamic library name following platform rules
    public static string PlatformLibraryName(string baseName)
        => $"{PlatformPrefix}{baseName}.{PlatformSuffix}";
}
