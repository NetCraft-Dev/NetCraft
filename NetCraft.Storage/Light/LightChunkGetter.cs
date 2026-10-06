using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage.Light;

//LightChunkGetter, chunk getter for lighting, maps to vanilla net.minecraft.world.level.chunk.LightChunkGetter
//The light engine uses it to get the LightChunk view by chunk coords
public interface LightChunkGetter
{
    //getChunkForLighting returns the chunk view for lighting; returns null when not loaded
    LightChunk? GetChunkForLighting(int chunkX, int chunkZ);

    //getLevel returns the world read entry point, providing the height range and block reads by coords
    BlockGetter GetLevel();

    //onLightUpdate, light update completion callback; a default interface method in vanilla
    void OnLightUpdate(LightLayer layer, SectionPos pos) { }
}
