using System.Text;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.Block;

//BlockTable 内嵌的原版方块注册表 顺序与状态属性都来自原版 26.2
//表由 tools 从原版 sources 推导生成 顺序即原版 Blocks.java 声明顺序
//注册顺序与每个方块的状态数一起决定 BlockState 全局 id 原版客户端按同一套 id 解码
internal static class BlockTable
{
    private const string ResourceSuffix = ".blocks.txt";

    //Load 逐行读出方块 属性定义 默认状态覆盖 放置类别 是否遮挡光线 推动反应与是否红石导体
    //每行 注册名<TAB>属性定义[<TAB>扩展段] 扩展段由 default= place= occlude= push= conductor= 用 ; 拼成
    public static IEnumerable<(Identifier Id, PropertyBase[] Properties,
        IReadOnlyDictionary<string, string>? Defaults, string? Placement, bool CanOcclude,
        NetCraft.Registry.Enums.PushReaction? PushReaction, bool? RedstoneConductor)> Load()
    {
        var assembly = typeof(BlockTable).Assembly;
        //资源名前缀跟着 RootNamespace 走 这里按后缀找 免得改命名空间时又要同步
        var name = Array.Find(assembly.GetManifestResourceNames(),
            n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (name is null)
            throw new InvalidOperationException(
                $"缺少内嵌方块表 *{ResourceSuffix} 现有 {string.Join(",", assembly.GetManifestResourceNames())}");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var fields = line.Split('\t');
            var id = Identifier.WithDefaultNamespace(fields[0]);
            //无属性的方块也会占一个空列 免得扩展段被当成属性段
            var properties = fields.Length > 1 && fields[1].Length > 0
                ? ParseProperties(id, fields[1])
                : Array.Empty<PropertyBase>();
            var extras = fields.Length > 2 ? ParseExtras(fields[2]) : default;
            yield return (id, properties, extras.Defaults, extras.Placement,
                extras.Occlude != false, extras.PushReaction, extras.RedstoneConductor);
        }
    }

    //ParseExtras 解析扩展段 形如 default=名:值,名:值;place=类别;occlude=false;push=destroy;conductor=false
    private static (IReadOnlyDictionary<string, string>? Defaults, string? Placement, bool? Occlude,
        NetCraft.Registry.Enums.PushReaction? PushReaction, bool? RedstoneConductor) ParseExtras(string text)
    {
        Dictionary<string, string>? defaults = null;
        string? placement = null;
        bool? occlude = null;
        NetCraft.Registry.Enums.PushReaction? pushReaction = null;
        bool? redstoneConductor = null;
        foreach (var item in text.Split(';'))
        {
            var kv = item.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0])
            {
                case "place":
                    placement = kv[1];
                    break;
                case "occlude":
                    occlude = kv[1] != "false";
                    break;
                case "push":
                    //表里写的是原版枚举成员名小写
                    pushReaction = Enum.Parse<NetCraft.Registry.Enums.PushReaction>(kv[1]);
                    break;
                case "conductor":
                    //只写与原版默认判定不同的那些 原版默认是按碰撞形状满不满格算
                    redstoneConductor = kv[1] != "false";
                    break;
                case "default":
                    defaults = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var part in kv[1].Split(','))
                    {
                        var fields = part.Split(':');
                        if (fields.Length == 2) defaults[fields[0]] = fields[1];
                    }
                    break;
            }
        }
        return (defaults, placement, occlude, pushReaction, redstoneConductor);
    }

    //ParseProperties 解析一行属性定义 形如 名:bool|名:int:min:max|名:enum:枚举名:值1,值2
    private static PropertyBase[] ParseProperties(Identifier block, string text)
    {
        var parts = text.Split('|');
        var result = new PropertyBase[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = ParseProperty(block, parts[i]);
        return result;
    }

    private static PropertyBase ParseProperty(Identifier block, string text)
    {
        var fields = text.Split(':');
        return fields[1] switch
        {
            "bool" => new BooleanProperty(fields[0]),
            "int" => new IntegerProperty(fields[0], int.Parse(fields[2]), int.Parse(fields[3])),
            "enum" => BuildEnumProperty(block, fields[0], fields[2], fields[3].Split(',')),
            _ => throw new InvalidOperationException($"{block} 不认识的属性类型 {text}")
        };
    }

    //BuildEnumProperty 按枚举名与值名列表建枚举属性
    //值名是原版序列化名 枚举类型由推导脚本生成 值列表可能只是枚举的一个子集
    private static PropertyBase BuildEnumProperty(Identifier block, string name, string enumName, string[] valueNames)
    {
        var enumType = BlockEnums.Resolve(enumName)
            ?? throw new InvalidOperationException($"{block} 的属性 {name} 引用了未知枚举 {enumName}");
        var values = Array.CreateInstance(enumType, valueNames.Length);
        for (var i = 0; i < valueNames.Length; i++)
            values.SetValue(Enum.Parse(enumType, valueNames[i]), i);
        var propertyType = typeof(EnumProperty<>).MakeGenericType(enumType);
        return (PropertyBase)Activator.CreateInstance(propertyType, name, values)!;
    }
}
