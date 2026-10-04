using NetCraft.Registry;

namespace NetCraft.Resources;

//Pack 资源包元数据接口对应原版 net.minecraft.server.packs.Pack
//描述一个资源包的 id/标题/描述/优先级
public sealed class Pack
{
    public Identifier Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int Priority { get; }
    public bool IsBuiltin { get; }
    public PackResources Resources { get; }

    public Pack(Identifier id, string title, string description, int priority, bool isBuiltin, PackResources resources)
    {
        Id = id;
        Title = title;
        Description = description;
        Priority = priority;
        IsBuiltin = isBuiltin;
        Resources = resources;
    }

    public override string ToString() => $"Pack[{Id} prio={Priority}]";
}
