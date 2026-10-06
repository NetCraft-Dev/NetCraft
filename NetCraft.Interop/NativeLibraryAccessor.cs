using System.Reflection;
using System.Runtime.InteropServices;

namespace NetCraft.Interop;

//NativeLibraryAccessor cross-platform dynamic library loading wrapper
//Wraps the vanilla .NET NativeLibrary static class to provide unified load/unload/get-delegate interfaces
//Platform-specific P/Invoke is forbidden, use the .NET cross-platform NativeLibrary API
public static class NativeLibraryAccessor
{
    //Load loads a dynamic library by name and returns an IntPtr handle
    //The assembly argument is used to resolve dependencies in the same directory
    public static IntPtr Load(string name, Assembly assembly)
        => NativeLibrary.Load(name, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories);

    //LoadByName loads by platform-specific name (such as libfoo.so/libfoo.dylib/foo.dll)
    //Lets .NET append the platform prefix/suffix automatically
    public static IntPtr LoadByName(string nameWithoutExtension, Assembly assembly)
        => NativeLibrary.Load(nameWithoutExtension, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories);

    //TryLoad returns false on failure and leaves the handle as IntPtr.Zero
    public static bool TryLoad(string name, Assembly assembly, out IntPtr handle)
    {
        return NativeLibrary.TryLoad(name, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.UserDirectories, out handle);
    }

    //GetDelegate gets a delegate for an exported function
    public static T GetDelegate<T>(IntPtr handle) where T : Delegate
    {
        var name = typeof(T).Name;
        var ptr = NativeLibrary.GetExport(handle, name);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Export function {name} not found");
        }
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    //TryGetDelegate returns false on failure
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

    //Free releases a dynamic library handle
    public static void Free(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeLibrary.Free(handle);
        }
    }
}
