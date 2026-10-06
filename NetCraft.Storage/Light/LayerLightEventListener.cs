using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//LayerLightEventListener, per-layer light listener, maps to vanilla net.minecraft.world.level.lighting.LayerLightEventListener
//Adds per-layer light data reads on top of LightEventListener
public interface LayerLightEventListener : LightEventListener
{
    //getDataLayerData returns the section's layer data, or null when absent
    DataLayer? GetDataLayerData(SectionPos pos);

    //getLightValue returns the light level at a block pos
    int GetLightValue(BlockPos pos);
}

//DummyLightLayerEventListener, empty implementation, maps to vanilla LayerLightEventListener.DummyLightLayerEventListener
//Placeholder for when lighting is disabled, so callers need not null-check everywhere
public sealed class DummyLightLayerEventListener : LayerLightEventListener
{
    public static readonly DummyLightLayerEventListener Instance = new();

    private DummyLightLayerEventListener() { }

    public DataLayer? GetDataLayerData(SectionPos pos) => null;

    public int GetLightValue(BlockPos pos) => 0;

    public void CheckBlock(BlockPos pos) { }

    public bool HasLightWork() => false;

    public int RunLightUpdates() => 0;

    public void UpdateSectionStatus(SectionPos pos, bool sectionEmpty) { }

    public void SetLightEnabled(ChunkPos pos, bool enable) { }

    public void PropagateLightSources(ChunkPos pos) { }
}
