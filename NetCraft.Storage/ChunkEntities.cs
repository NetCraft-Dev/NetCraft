namespace NetCraft.Storage;

using System.Collections.Generic;
using NetCraft.Primitives;

//Chunk entities collection, maps to vanilla net.minecraft.world.level.entity.ChunkEntities
//Holds the chunk pos and entity list, providing isEmpty and the getEntities stream
public sealed class ChunkEntities<T>
{
    public ChunkPos Pos { get; }
    private readonly List<T> _entities;

    public ChunkEntities(ChunkPos pos, List<T> entities)
    {
        Pos = pos;
        _entities = entities;
    }

    public ChunkPos GetPos() => Pos;
    public IReadOnlyList<T> GetEntities() => _entities;
    public bool IsEmpty() => _entities.Count == 0;
}
