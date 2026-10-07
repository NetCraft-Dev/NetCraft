using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//EntityModel entity model base class, maps to vanilla net.minecraft.client.model.EntityModel
//Holds the root ModelPart; subclasses build the model tree in the constructor and setupAnim drives animation
//renderToBuffer walks the ModelPart tree, generating vertices into an EntityVertexBuilder
//The Pipeline property specifies the render pipeline, defaulting to ENTITY_CUTOUT; subclasses may override
public abstract class EntityModel
{
    //Root model root node, filled in by subclasses at construction
    public ModelPart Root { get; }
    //Pipeline render pipeline defaulting to ENTITY_CUTOUT; subclasses may override to return SOLID/TRANSLUCENT
    public virtual RenderPipeline Pipeline => EntityRenderPipelines.ENTITY_CUTOUT;

    protected EntityModel(ModelPart root) => Root = root;

    //SetupAnim animation driver; subclasses override to adjust ModelPart rotations by state
    //PoC skeleton version with an empty implementation; W9.2 wires real animation
    public virtual void SetupAnim(EntityRenderState state) { }

    //RenderToBuffer renders the model tree into the builder
    //poseStack already holds the entity world transform (position+orientation); light/overlay/color come from EntityRenderer
    public void RenderToBuffer(PoseStack poseStack, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
    {
        Root.Render(poseStack, builder, lightCoords, overlayCoords, color);
    }
}
