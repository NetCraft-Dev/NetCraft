using NetCraft.Primitives;

namespace NetCraft.Storage;

//PoiManager, point-of-interest manager abstract class, maps to vanilla net.minecraft.world.entity.ai.village.poi.PoiManager
//Holds the per-chunk point-of-interest registry used by villages, iron golems and similar
//The full implementation depends on the PoiSection/PoiType subsystem
public abstract class PoiManager
{
    //GetChunk, placeholder for getting the point-of-interest data of the given chunk
    public abstract object? GetChunk(ChunkPos pos);
}
