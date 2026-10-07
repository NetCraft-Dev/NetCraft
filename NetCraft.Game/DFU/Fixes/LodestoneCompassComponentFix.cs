using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Lodestone compass component fix, maps to vanilla LodestoneCompassComponentFix
//1.20.5 renames minecraft:lodestone_target to minecraft:lodestone_tracker and moves pos/dimension into the target sub-map
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
