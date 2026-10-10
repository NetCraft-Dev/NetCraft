using System.Runtime.InteropServices;

namespace NetCraft.Hook;

//NativeInjector 调原生注入层的导出函数
//原生库是被 CLR 的 profiler 机制加载的 这里按同一路径再取一次拿到的是同一个实例
internal static class NativeInjector
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RequestRewriteSignature(
        [MarshalAs(UnmanagedType.LPStr)] string moduleSuffix,
        [MarshalAs(UnmanagedType.LPStr)] string typeName,
        [MarshalAs(UnmanagedType.LPStr)] string methodName,
        byte[] body,
        uint size);

    private static RequestRewriteSignature? _requestRewrite;

    //IsAvailable 原生注入层是否已经挂到进程上
    //光看环境变量不够 库文件可能不在那个路径上 真调用时才发现就是启动崩溃 这里先探一次
    //探过之后入口被缓存 后续调用不会重复加载
    public static bool IsAvailable
    {
        get
        {
            try
            {
                _requestRewrite ??= Resolve();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    //RequestRewrite 登记一条运行时改写 返回 0 表示登记成功
    public static int RequestRewrite(string moduleSuffix, string typeName, string methodName, byte[] body)
    {
        _requestRewrite ??= Resolve();
        return _requestRewrite(moduleSuffix, typeName, methodName, body, (uint)body.Length);
    }

    private static RequestRewriteSignature Resolve()
    {
        var path = Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH");
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException(
                "CORECLR_PROFILER_PATH is not set, the native injection layer is not attached, runtime injection is unavailable");

        var library = NativeLibrary.Load(path);
        var export = NativeLibrary.GetExport(library, "ncn_request_rewrite");
        return Marshal.GetDelegateForFunctionPointer<RequestRewriteSignature>(export);
    }
}
