using NetCraft.Registry;

namespace NetCraft.Resources;

//Pack, resource pack metadata interface, maps to vanilla net.minecraft.server.packs.Pack
//Describes a resource pack's id/title/description/priority
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
