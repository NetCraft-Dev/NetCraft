using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
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

namespace NetCraft.Client.Model;

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
