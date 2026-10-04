using NetCraft.Registry;

namespace NetCraft.Resources;

//Resource 单个资源对应原版 net.minecraft.server.packs.resources.Resource
//包装资源标识和流式访问器
//含 sourcePackId 标记来源资源包
public sealed class Resource
{
    public Identifier Location { get; }
    public string SourcePackId { get; }
    private readonly Func<Stream> _streamFactory;

    public Resource(Identifier location, string sourcePackId, Func<Stream> streamFactory)
    {
        Location = location;
        SourcePackId = sourcePackId;
        _streamFactory = streamFactory;
    }

    //Open 打开资源流每次调用返回新流
    public Stream Open() => _streamFactory();
}
