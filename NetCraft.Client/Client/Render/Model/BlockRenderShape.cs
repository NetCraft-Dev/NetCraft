using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Client.Render.Model;

//BlockRenderShape block render shape, used for face culling decisions
//FullBlock full cube, can occlude neighbor block faces
//Empty air, not rendered and does not occlude
//Custom non-full blocks (glass/slabs/stairs) do not occlude neighbor faces
//The first version simplifies air=Empty, other registered blocks=FullBlock; later versions decide precisely from model element geometry
public enum BlockRenderShape
{
    Empty,
    FullBlock,
    Custom
}

//BlockRenderShapeProvider block render shape lookup
//Looks up the Block by BlockState.Id to decide whether it is air
//air Empty, others FullBlock
//Can later be extended to query shape via BlockBehaviour or decide from model geometry
public static class BlockRenderShapeProvider
{
    public static BlockRenderShape GetShape(BlockState state)
    {
        var block = BlockStateRegistry.Owner(state.Id);
        //Air block Id is minecraft:air
        if (block.Id.Path == "air")
            return BlockRenderShape.Empty;
        //The moving piston body does not go into the chunk mesh; MovingBlockRenderer draws it separately with progress
        //If baked into the static mesh, the block would flash the wrong shape first and then jump to the destination
        if (block.Id.Path == "moving_piston")
            return BlockRenderShape.Empty;
        return BlockRenderShape.FullBlock;
    }
}
