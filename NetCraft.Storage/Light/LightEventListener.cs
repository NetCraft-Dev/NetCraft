using NetCraft.Primitives;

namespace NetCraft.Storage.Light;

//LightEventListener, light event listener entry point, maps to vanilla net.minecraft.world.level.lighting.LightEventListener
//Both the light engine and the light storage layer implement it so the chunk system can notify light changes
public interface LightEventListener
{
    //checkBlock registers a recompute after a block change
    void CheckBlock(BlockPos pos);

    //hasLightWork, whether there is pending light work
    bool HasLightWork();

    //runLightUpdates runs one round of light updates and returns the number of nodes processed
    int RunLightUpdates();

    //updateSectionStatus notifies when a section's empty flag changes
    void UpdateSectionStatus(SectionPos pos, bool sectionEmpty);

    //setLightEnabled toggles lighting for a chunk
    void SetLightEnabled(ChunkPos pos, bool enable);

    //propagateLightSources propagates all light sources in the chunk
    void PropagateLightSources(ChunkPos pos);

    //updateSectionStatus block-coord overload; a default interface method in vanilla
    void UpdateSectionStatus(BlockPos pos, bool sectionEmpty)
        => UpdateSectionStatus(SectionPos.Of(pos), sectionEmpty);
}
