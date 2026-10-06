using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//LightChunk, the chunk view used for lighting, maps to vanilla net.minecraft.world.level.chunk.LightChunk
//Extends BlockGetter and exposes only the block reads and light source enumeration the light engine needs
public interface LightChunk : BlockGetter
{
    //getSkyLightSources returns the sky light source column heightmap of this chunk
    ChunkSkyLightSources GetSkyLightSources();

    //findBlockLightSources enumerates all light-emitting blocks in the chunk
    void FindBlockLightSources(Action<BlockPos, BlockState> consumer);
}
