using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetCraft.Hook.Runtime;

//JitCompiler 定位当前进程的 RyuJIT 编译器对象
//coreclr 导出 getJit 返回 ICorJitCompiler* 该对象虚表第 0 项即 compileMethod
//只有拿到它才能在方法被 JIT 之前接管 也才能收到"某方法刚被编译"的事件
//这是"JIT 前 hook"与"检测目标是否已被编译"共用的唯一可靠事实来源
public static class JitCompiler
{
    private delegate IntPtr GetJitDelegate();

    //ModuleBase coreclr 模块的加载基址
    public static IntPtr ModuleBase { get; private set; }

    //GetJitAddress getJit 导出函数的地址
    public static IntPtr GetJitAddress { get; private set; }

    //Compiler ICorJitCompiler 对象地址
    public static IntPtr Compiler { get; private set; }

    //VTable 编译器对象的虚表地址
    public static IntPtr VTable { get; private set; }

    //CompileMethod compileMethod 的原函数地址 取自 vtable[0]
    public static IntPtr CompileMethod { get; private set; }

    //Located 是否已成功定位
    public static bool Located { get; private set; }

    //Locate 定位编译器对象 返回结果描述 失败时也保证不抛
    public static string Locate()
    {
        if (Located)
            return "located";

        var module = FindModule("coreclr");
        if (module is null)
            return "coreclr module not found";
        ModuleBase = module.BaseAddress;

        IntPtr handle;
        try
        {
            handle = NativeLibrary.Load(module.FileName);
        }
        catch (Exception ex)
        {
            return $"loading coreclr failed: {ex.Message}";
        }

        if (!NativeLibrary.TryGetExport(handle, "getJit", out var getJit))
            return "coreclr does not export getJit";
        GetJitAddress = getJit;

        IntPtr compiler;
        try
        {
            var getJitFn = Marshal.GetDelegateForFunctionPointer<GetJitDelegate>(getJit);
            compiler = getJitFn();
        }
        catch (Exception ex)
        {
            return $"calling getJit failed: {ex.Message}";
        }

        if (compiler == IntPtr.Zero)
            return "getJit returned a null pointer";
        Compiler = compiler;

        VTable = Marshal.ReadIntPtr(compiler);
        if (VTable == IntPtr.Zero)
            return "the compiler object has no vtable";

        CompileMethod = Marshal.ReadIntPtr(VTable);
        if (CompileMethod == IntPtr.Zero)
            return "vtable entry 0 is null";

        Located = true;
        return "ok";
    }

    //BelongsToCoreClr 判断地址是否落在 coreclr 模块内
    //用来校验取到的虚表项确实是运行时自己的代码 而不是别处内存
    public static bool BelongsToCoreClr(IntPtr address)
    {
        if (ModuleBase == IntPtr.Zero || address == IntPtr.Zero)
            return false;

        var size = (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>();
        if (VirtualQuery(address, out var info, size) == UIntPtr.Zero)
            return false;

        return info.AllocationBase == ModuleBase;
    }

    //Describe 汇总定位结果 供探针打印
    public static string Describe()
    {
        if (!Located)
            return $"not located: {Locate()}";

        return string.Join(Environment.NewLine,
            $"coreclr base                 : 0x{ModuleBase.ToInt64():X}",
            $"getJit export                : 0x{GetJitAddress.ToInt64():X}",
            $"ICorJitCompiler              : 0x{Compiler.ToInt64():X}",
            $"vtable                       : 0x{VTable.ToInt64():X}",
            $"compileMethod                : 0x{CompileMethod.ToInt64():X}",
            $"compileMethod in coreclr     : {BelongsToCoreClr(CompileMethod)}",
            $"vtable in coreclr            : {BelongsToCoreClr(VTable)}");
    }

    private static ProcessModule? FindModule(string name)
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.ModuleName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return module;
        }
        return null;
    }

    //FindExports 列出 coreclr 导出表里名字含关键字的条目
    //getJit 在新版运行时可能已被裁剪 需要先看清还剩哪些入口
    public static unsafe List<string> FindExports(string keyword)
    {
        var result = new List<string>();
        if (ModuleBase == IntPtr.Zero)
            return result;

        var basePtr = (byte*)ModuleBase;
        if (basePtr[0] != (byte)'M' || basePtr[1] != (byte)'Z')
            return result;

        var peOffset = *(int*)(basePtr + 0x3C);
        var pe = basePtr + peOffset;
        if (*(uint*)pe != 0x00004550)
            return result;

        var optionalHeader = pe + 24;
        var magic = *(ushort*)optionalHeader;
        var dataDirectoryOffset = magic == 0x20B ? 112 : 96;
        var exportDir = optionalHeader + dataDirectoryOffset;
        var exportRva = *(int*)exportDir;
        if (exportRva == 0)
            return result;

        var exportTable = basePtr + exportRva;
        var numberOfNames = *(int*)(exportTable + 24);
        var addressOfNames = *(int*)(exportTable + 32);

        var names = (int*)(basePtr + addressOfNames);
        for (var i = 0; i < numberOfNames; i++)
        {
            var entry = basePtr + names[i];
            if (entry == basePtr)
                continue;
            var name = new string((sbyte*)entry);
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                result.Add(name);
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public uint Alignment1;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint Alignment2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryBasicInformation buffer, UIntPtr length);
}
