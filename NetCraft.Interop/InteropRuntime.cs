using System.Runtime.InteropServices;
using System.Text;

namespace NetCraft.Interop;

//InteropRuntime 互操作运行时工具
//提供 UTF-8/UTF-16 编码转换与平台信息查询
//禁止平台特定 P/Invoke 所有 API 均用 .NET 跨平台标准库
public static class InteropRuntime
{
    //StringToUtf8NativeAlloc 用 NativeMemory 分配 UTF-8 字节串
    //返回的字节流需调用方用 NativeMemory.Free 释放
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

    //Utf8PtrToString 从 NativeMemory 分配的 UTF-8 字节流转 .NET string
    public static unsafe string Utf8PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        int len = 0;
        while (ptr[len] != 0) len++;
        return Encoding.UTF8.GetString(ptr, len);
    }

    //StringToUtf8Managed 转 UTF-8 到托管 byte[] 不分配非托管内存
    public static byte[] StringToUtf8Managed(string s)
        => Encoding.UTF8.GetBytes(s);

    //Utf8ManagedToString 从托管 byte[] 转 .NET string
    public static string Utf8ManagedToString(byte[] bytes)
        => Encoding.UTF8.GetString(bytes);

    //StringToUtf16NativeAlloc 用 NativeMemory 分配 UTF-16 字符串
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

    //Utf16PtrToString 从 UTF-16 char* 转 .NET string
    public static unsafe string Utf16PtrToString(char* ptr)
    {
        if (ptr == null) return string.Empty;
        int len = 0;
        while (ptr[len] != '\0') len++;
        return new string(ptr, 0, len);
    }

    //IsWindows 是否运行在 Windows 平台
    public static bool IsWindows => OperatingSystem.IsWindows();

    //IsLinux 是否运行在 Linux 平台
    public static bool IsLinux => OperatingSystem.IsLinux();

    //IsMacOS 是否运行在 macOS 平台
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    //PlatformSuffix 平台特定动态库后缀（dll/so/dylib）
    public static string PlatformSuffix
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "dll";
            if (OperatingSystem.IsMacOS()) return "dylib";
            return "so";
        }
    }

    //PlatformPrefix 平台特定动态库前缀（Linux/macOS 为 lib Windows 无前缀）
    public static string PlatformPrefix
    {
        get
        {
            if (OperatingSystem.IsWindows()) return string.Empty;
            return "lib";
        }
    }

    //PlatformLibraryName 按平台规则合成动态库名称
    public static string PlatformLibraryName(string baseName)
        => $"{PlatformPrefix}{baseName}.{PlatformSuffix}";
}
