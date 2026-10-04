using System.Reflection;
using System.Runtime.InteropServices;

namespace NetCraft.Interop;

//NativeLibraryAccessor 跨平台动态库加载封装
//对应原版 .NET NativeLibrary 静态类封装提供统一加载/卸载/委托获取接口
//禁止平台特定 P/Invoke 用 .NET 跨平台 NativeLibrary API
public static class NativeLibraryAccessor
{
    //Load 按名称加载动态库返回 IntPtr 句柄
    //assembly 参数用于解析同目录依赖搜索路径
    public static IntPtr Load(string name, Assembly assembly)
        => NativeLibrary.Load(name, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories);

    //LoadByName 按平台特定名称加载（如 libfoo.so/libfoo.dylib/foo.dll）
    //让 .NET 自动追加平台前后缀
    public static IntPtr LoadByName(string nameWithoutExtension, Assembly assembly)
        => NativeLibrary.Load(nameWithoutExtension, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories);

    //TryLoad 失败返回 false 句柄为 IntPtr.Zero
    public static bool TryLoad(string name, Assembly assembly, out IntPtr handle)
    {
        return NativeLibrary.TryLoad(name, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories, out handle);
    }

    //GetDelegate 获取导出函数委托
    public static T GetDelegate<T>(IntPtr handle) where T : Delegate
    {
        var name = typeof(T).Name;
        var ptr = NativeLibrary.GetExport(handle, name);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"导出函数 {name} 未找到");
        }
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    //TryGetDelegate 失败返回 false
    public static bool TryGetDelegate<T>(IntPtr handle, out T? @delegate) where T : Delegate
    {
        var ptr = NativeLibrary.GetExport(handle, typeof(T).Name);
        if (ptr == IntPtr.Zero)
        {
            @delegate = null;
            return false;
        }
        @delegate = Marshal.GetDelegateForFunctionPointer<T>(ptr);
        return true;
    }

    //Free 释放动态库句柄
    public static void Free(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeLibrary.Free(handle);
        }
    }
}
