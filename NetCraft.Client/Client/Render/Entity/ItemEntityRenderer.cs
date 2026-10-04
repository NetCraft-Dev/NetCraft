using System.Numerics;
using NetCraft.Game.Client.Render.Model;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.Entity;

//ItemEntityRenderer 掉落物渲染器对标原版 net.minecraft.client.renderer.entity.ItemEntityRenderer
//方块物品直接沿用该方块的已烘焙模型 六个方向的 cullface quad 拼成一个 1/4 格的小立方体
//非方块物品没有对应模型 本作不渲染 原版走 ItemRenderer 的平面物品模型 未接入
public sealed class ItemEntityRenderer : EntityRenderer
{
    //DropScale 掉落物边长相对方块的比例 对应原版 0.25
    private const float DropScale = 0.25f;

    //ModelPivot 方块模型坐标的轴心 模型 0-16 单位对应一格 减 8 后模型以自身中心为原点
    private const float ModelPivot = 8f;

    //MinHoverHeight 模型底面离实体位置的最小高度 对应原版 1/16 格
    private const float MinHoverHeight = 0.0625f;

    //_mapper 方块状态到烘焙模型的映射 单独一个实例
    //区块网格构建在后台线程用它 各自持缓存避免并发读写同一个缓存字典
    private readonly BlockStateModelMapper _mapper;

    public ItemEntityRenderer(BlockStateModelMapper mapper) => _mapper = mapper;

    //Model 掉落物直接按方块模型写顶点不走 ModelPart 树 给空根节点满足基类契约
    public override EntityModel Model { get; } = PlaceholderModel.Instance;

    //Pipeline 方块纹理带镂空像素 用 cutout 免得透明处出黑边
    public override RenderPipeline Pipeline => EntityRenderPipelines.ENTITY_CUTOUT;

    //Render 取物品对应方块的六面模型拼成小立方体
    public override void Render(PoseStack poseStack, EntityVertexBuilder builder, EntityRenderState state)
    {
        if (state.CustomData is not ItemStack stack || stack.IsEmpty()) return;
        if (stack.GetItem() is not BlockItem blockItem) return;
        var model = _mapper.GetModel(blockItem.PlacedBlock.DefaultBlockState);
        if (model is null) return;
        poseStack.PushPose();
        //上下浮动与自转都按原版公式 相位由实体 id 派生 服务端不同步该值
        var bob = MathF.Sin(state.AgeInTicks / 10f + state.BobOffset) * 0.1f + 0.1f;
        poseStack.Translate(0f, bob + DropScale / 2f + MinHoverHeight, 0f);
        poseStack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY,
            ItemEntity.GetSpin(state.AgeInTicks, state.BobOffset)));
        poseStack.Scale(DropScale, DropScale, DropScale);
        poseStack.Translate(-ModelPivot, -ModelPivot, -ModelPivot);
        var pose = poseStack.Pose();
        var normalMatrix = poseStack.Normal();
        foreach (Direction direction in Enum.GetValues<Direction>())
        foreach (var quad in model.GetCullfaceQuads(direction))
            builder.AddQuad(quad, pose, normalMatrix, -1, 0, state.LightCoords);
        poseStack.PopPose();
    }

    //PlaceholderModel 空模型 掉落物不通过 ModelPart 树渲染
    private sealed class PlaceholderModel : EntityModel
    {
        public static readonly PlaceholderModel Instance = new();

        private PlaceholderModel() : base(new ModelPart(
            new List<ModelPart.Cube>(), new Dictionary<string, ModelPart>())) { }
    }
}
