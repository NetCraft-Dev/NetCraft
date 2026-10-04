using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NetCraft.ModLoader;

//KernelTypeIndex 类型到程序集的索引 用来把注入目标定位到具体程序集
//不能靠命名空间前缀猜程序集 两者并不一一对应
//例如 NetCraft.Game.Server.DedicatedServer 落在 NetCraft.Server.dll 里
//唯一可靠的办法是读元数据表的 TypeDef 记录 全程不加载程序集
//不加载这点对模组注入模组是必需的 目标一旦被提前加载就再没有改写的机会了
internal static class KernelTypeIndex
{
    //Build 扫给定程序集建索引 readBytes 按程序集名取原始字节 取不到就跳过
    public static Dictionary<string, string> Build(IEnumerable<string> assemblyNames, Func<string, byte[]?> readBytes)
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assemblyNames)
        {
            var bytes = readBytes(name);
            if (bytes is null)
                continue;

            Add(index, name, bytes);
        }
        return index;
    }

    //Add 把单个程序集的类型并进索引
    //同名类型先到先得 内核先扫所以内核优先 模组之间同名也是先扫到的那个赢
    public static void Add(Dictionary<string, string> index, string assemblyName, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            return;

        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            index.TryAdd(GetFullName(reader, type), assemblyName);
        }
    }

    //GetFullName 拼类型全名 嵌套类型逐层向外拼成 a.b/c 形式
    private static string GetFullName(MetadataReader reader, TypeDefinition type)
    {
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
            return GetFullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
}
