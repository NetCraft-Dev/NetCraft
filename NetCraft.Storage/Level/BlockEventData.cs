using NetCraft.Primitives;

namespace NetCraft.Storage;

//BlockEventData, a block event, maps to vanilla net.minecraft.world.level.BlockEventData
//All four fields take part in deduplication; the same pos, block and params are enqueued only once
//Pistons, chest opening and note blocks all go through this channel; it is separate from scheduled ticks
public readonly record struct BlockEventData(BlockPos Pos, NetCraft.Registry.Block Block, int ParamA, int ParamB);
