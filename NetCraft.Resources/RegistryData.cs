using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Resources;

//RegistryData 注册表数据驱动声明对应原版 RegistryDataLoader.RegistryData
//把「哪个注册表」与「元素怎么解码」配成一条 供 RegistryDataLoader 按目录扫描加载
//非泛型基类让不同元素类型的声明能放进同一个列表
public abstract class RegistryData
{
    //RegistryId 注册表自身标识符 决定扫描目录 data/<namespace>/<RegistryId.Path>/
    public abstract Identifier RegistryId { get; }

    //TryLoadElement 解码一份元素 JSON 并写入目标注册表 失败把原因写进 error
    internal abstract bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error);

    //Boxed 弱类型注册表装载 用于 Registries 侧声明为 Registry<object> 的注册表
    //Registry 项目不能反向引用 Game 的密度函数/噪声设置类型 这些注册表只能以 object 键声明
    //强类型 codec 解出的元素装箱后写进 object 注册表
    public static RegistryData Boxed<T>(WritableRegistry<object> registry, Codec<T> codec) where T : class
        => new BoxedRegistryData<T>(registry, codec);

    //Decode 读元素文件并按 codec 解码 失败抛异常由调用方转成错误字符串
    //RegistryData<T> 与 BoxedRegistryData<T> 只有写入注册表的方式不同 读取与解码共用这一处
    //直接走 JsonOps 的流重载 内部按 UTF-8 字节解析 不做 ReadToEnd 的 UTF-16 转码
    private protected static T Decode<T>(Resource resource, RegistryAccess context, Codec<T> codec) where T : class
    {
        JsonNode? node;
        using (var stream = resource.Open())
        {
            node = JsonOps.Parse(stream).GetOrThrow();
        }

        var ops = new RegistryOps<JsonNode?>(JsonOps.Instance, context);
        return codec.Parse(ops, node).GetOrThrow();
    }
}

//RegistryData<T> 具体元素类型的注册表数据声明
public sealed class RegistryData<T> : RegistryData where T : class
{
    public RegistryData(WritableRegistry<T> registry, Codec<T> elementCodec)
    {
        Registry = registry;
        ElementCodec = elementCodec;
    }

    //Registry 目标注册表 必须在 Freeze 之前调用加载
    public WritableRegistry<T> Registry { get; }

    //ElementCodec 元素 JSON 编解码
    public Codec<T> ElementCodec { get; }

    public override Identifier RegistryId => Registry.Key.Identifier;

    internal override bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error)
    {
        error = string.Empty;
        try
        {
            var value = Decode(resource, context, ElementCodec);
            Registry.Register(ResourceKey<T>.Create(Registry.Key, elementId), value, RegistrationInfo.BuiltIn);
            //元素自带的标识只能从注册名回填 原版 id 只存在于注册表里
            if (value is RegistryIdentified identified) identified.SetRegistryId(elementId);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{elementId}: {ex.Message}";
            return false;
        }
    }
}

//BoxedRegistryData<T> 元素强类型而注册表弱类型的装载实现
//对应原版不存在这种形态 是本作 Registry 项目与 Game 项目分层导致的折中
internal sealed class BoxedRegistryData<T> : RegistryData where T : class
{
    private readonly WritableRegistry<object> _registry;
    private readonly Codec<T> _codec;

    public BoxedRegistryData(WritableRegistry<object> registry, Codec<T> codec)
    {
        _registry = registry;
        _codec = codec;
    }

    public override Identifier RegistryId => _registry.Key.Identifier;

    internal override bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error)
    {
        error = string.Empty;
        try
        {
            var value = Decode(resource, context, _codec);
            _registry.Register(ResourceKey<object>.Create(_registry.Key, elementId), value, RegistrationInfo.BuiltIn);
            //元素自带的标识只能从注册名回填 原版 id 只存在于注册表里
            if (value is RegistryIdentified identified) identified.SetRegistryId(elementId);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{elementId}: {ex.Message}";
            return false;
        }
    }
}
