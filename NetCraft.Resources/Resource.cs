using NetCraft.Registry;

namespace NetCraft.Resources;

//Resource, a single resource, maps to vanilla net.minecraft.server.packs.resources.Resource
//Wraps a resource identifier and a streaming accessor
//Carries sourcePackId to mark the originating resource pack
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

    //Open opens the resource stream, each call returns a fresh stream
    public Stream Open() => _streamFactory();
}
