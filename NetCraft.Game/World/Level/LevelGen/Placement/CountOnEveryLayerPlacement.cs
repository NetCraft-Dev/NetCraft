using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//CountOnEveryLayerPlacement 逐层计数放置对应原版 CountOnEveryLayerPlacement
//从地表往下逐层找可放置的地面 每层按 count 撒点 直到某层全部失败
public sealed class CountOnEveryLayerPlacement : PlacementModifier
{
    public static readonly Codec<CountOnEveryLayerPlacement> Codec =
        new SingleFieldPlacementCodec<CountOnEveryLayerPlacement, IntProvider>(
            IntProviders.NonNegativeCodec.FieldOf("count"),
            count => new CountOnEveryLayerPlacement(count),
            placement => placement.CountProvider);

    //CountProvider 每层撒点数量 属性名避开原版字段名 count
    public IntProvider CountProvider { get; }

    private CountOnEveryLayerPlacement(IntProvider count) => CountProvider = count;

    //Of 构造入口对应原版 of
    public static CountOnEveryLayerPlacement Of(IntProvider count) => new(count);

    public static CountOnEveryLayerPlacement Of(int count) => new(ConstantInt.Of(count));

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var positions = new List<BlockPos>();
        var layer = 0;
        bool foundAny;
        do
        {
            foundAny = false;
            var count = CountProvider.Sample(random);
            for (var i = 0; i < count; i++)
            {
                var x = random.NextInt(16) + origin.X;
                var z = random.NextInt(16) + origin.Z;
                var startY = context.Level.GetHeight(Heightmap.Types.MotionBlocking, x, z);
                var y = FindOnGroundYPosition(context, x, startY, z, layer);
                if (y != int.MaxValue)
                {
                    positions.Add(new BlockPos(x, y, z));
                    foundAny = true;
                }
            }
            layer++;
        } while (foundAny);
        return positions;
    }

    //FindOnGroundYPosition 从地表往下找第 layer 个实体地面之上的一格对应原版 findOnGroundYPosition
    private static int FindOnGroundYPosition(PlacementContext context, int xStart, int yStart, int zStart, int layerToPlaceOn)
    {
        var currentLayer = 0;
        var currentBlock = context.Level.GetBlockState(xStart, yStart, zStart);
        for (var y = yStart; y >= context.GetMinGenY() + 1; y--)
        {
            var belowBlock = context.Level.GetBlockState(xStart, y - 1, zStart);
            if (!IsEmpty(belowBlock) && IsEmpty(currentBlock) && belowBlock.Owner != Blocks.BEDROCK)
            {
                if (currentLayer == layerToPlaceOn) return y;
                currentLayer++;
            }
            currentBlock = belowBlock;
        }
        return int.MaxValue;
    }

    //IsEmpty 空液判定对应原版 isEmpty
    private static bool IsEmpty(BlockState state)
        => state.Owner == Blocks.AIR || state.Owner == Blocks.WATER || state.Owner == Blocks.LAVA;

    public override PlacementModifierType Type => CountOnEveryLayerPlacementType.Instance;
}

//CountOnEveryLayerPlacementType 对应原版 PlacementModifierType.COUNT_ON_EVERY_LAYER
public sealed class CountOnEveryLayerPlacementType : PlacementModifierType<CountOnEveryLayerPlacement>
{
    public static readonly CountOnEveryLayerPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("count_on_every_layer"), new CountOnEveryLayerPlacementType());

    private CountOnEveryLayerPlacementType()
        : base(Identifier.WithDefaultNamespace("count_on_every_layer"), CountOnEveryLayerPlacement.Codec) { }
}
