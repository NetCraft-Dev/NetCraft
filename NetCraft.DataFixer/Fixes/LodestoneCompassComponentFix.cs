using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//磁石指南针组件修复对应原版LodestoneCompassComponentFix
//1.20.5把minecraft:lodestone_target重命名为minecraft:lodestone_tracker并把pos/dimension移到target子map
public class LodestoneCompassComponentFix : DataComponentRemainderFix
{
    public LodestoneCompassComponentFix(Schema outputSchema)
        : base(outputSchema, "LodestoneCompassComponentFix", "minecraft:lodestone_target", "minecraft:lodestone_tracker") { }

    protected override Dynamic<object> FixComponent(Dynamic<object> input)
    {
        var pos = input.Get("pos").Result();
        var dimension = input.Get(FixConstants.ChunkRegionIoEventDimension).Result();
        var input2 = input.Remove("pos").Remove(FixConstants.ChunkRegionIoEventDimension);
        if (pos.IsPresent && dimension.IsPresent)
        {
            input2 = input2.Set(FixConstants.JigsawBlockEntityTarget,
                input2.EmptyMap().Set("pos", pos.Get()).Set(FixConstants.ChunkRegionIoEventDimension, dimension.Get()));
        }
        return input2;
    }
}
