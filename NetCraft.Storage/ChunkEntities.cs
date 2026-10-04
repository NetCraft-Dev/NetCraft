namespace NetCraft.Storage;

using System.Collections.Generic;
using NetCraft.Primitives;

//chunk实体集合对应原版net.minecraft.world.level.entity.ChunkEntities
//持有chunk位置与实体列表提供isEmpty判定与getEntities流
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
