using System.Numerics;
using NetCraft.Client.Render.Model;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render.Entity;

//ItemEntityRenderer dropped item renderer, maps to vanilla net.minecraft.client.renderer.entity.ItemEntityRenderer
//Block items reuse the block's baked model directly; cullface quads from six directions assemble into a 1/4-block cube
//Non-block items have no matching model and are not rendered here; vanilla uses ItemRenderer's flat item model, not wired up
public sealed class ItemEntityRenderer : EntityRenderer
{
    //DropScale dropped item edge length relative to a block, maps to vanilla 0.25
    private const float DropScale = 0.25f;

    //ModelPivot block model coordinate pivot; the model's 0-16 units map to one block, and subtracting 8 makes the model centered on its own origin
    private const float ModelPivot = 8f;

    //MinHoverHeight minimum height of the model's bottom face above the entity position, maps to vanilla 1/16 block
    private const float MinHoverHeight = 0.0625f;

    //_mapper block state to baked model mapping, a separate instance
    //Chunk mesh building uses it on a background thread; each holds its own cache to avoid concurrent reads/writes on the same cache dictionary
    private readonly BlockStateModelMapper _mapper;

    public ItemEntityRenderer(BlockStateModelMapper mapper) => _mapper = mapper;

    //Model dropped items write vertices directly from the block model without the ModelPart tree; an empty root node satisfies the base class contract
    public override EntityModel Model { get; } = PlaceholderModel.Instance;

    //Pipeline block textures have cutout pixels; use cutout to avoid black edges at transparent areas
    public override RenderPipeline Pipeline => EntityRenderPipelines.ENTITY_CUTOUT;

    //Render takes the six-face model of the item's block and assembles a small cube
    public override void Render(PoseStack poseStack, EntityVertexBuilder builder, EntityRenderState state)
    {
        if (state.CustomData is not ItemStack stack || stack.IsEmpty()) return;
        if (stack.GetItem() is not BlockItem blockItem) return;
        var model = _mapper.GetModel(blockItem.PlacedBlock.DefaultBlockState);
        if (model is null) return;
        poseStack.PushPose();
        //Bobbing and spin follow vanilla formulas; the phase is derived from the entity id and not synced by the server
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

    //PlaceholderModel empty model; dropped items are not rendered through the ModelPart tree
    private sealed class PlaceholderModel : EntityModel
    {
        public static readonly PlaceholderModel Instance = new();

        private PlaceholderModel() : base(new ModelPart(
            new List<ModelPart.Cube>(), new Dictionary<string, ModelPart>())) { }
    }
}
