using System.Runtime.CompilerServices;

namespace NetCraft;

//模块初始化器。
//.NET 独家：在程序集加载时自动执行（早于任何类型首次访问）。
//用于在主库被引用类型前，先注册内嵌资源解析回调，避免"鸡生蛋"问题：
//  - GetType(NetCraftKernel) 需要 NetCraft.Config（被引用）
//  - NetCraft.Config 解析需要 Resolving 回调
//  - Resolving 回调原在 Initialize() 中注册，但 GetType 在 Initialize 之前
internal static class ModuleInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        EmbeddedAssemblyLoader.Initialize();
    }
}

