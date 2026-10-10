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

namespace NetCraft.Client.Render.Entity;

//EntityRenderer entity renderer base class, maps to vanilla net.minecraft.client.renderer.entity.EntityRenderer
//Subclasses hold an EntityModel and implement Render to generate vertices; one Renderer instance per entity type
//shadowRadius/shadowStrength shadow parameters; the PoC skeleton does not render shadows, W9.2 adds them
public abstract class EntityRenderer
{
    //Model entity model, provided by subclasses
    public abstract EntityModel Model { get; }

    //Pipeline render pipeline defaults to Model.Pipeline; subclasses may override
    public virtual RenderPipeline Pipeline => Model.Pipeline;

    //ShadowRadius shadow radius; 0 means no shadow
    public virtual float ShadowRadius => 0f;

    //ShadowStrength shadow strength
    public virtual float ShadowStrength => 1f;

    //Render generates entity vertices and writes them into builder
    //poseStack already contains all transforms except the camera projection; the caller pushes the entity world transform
    //state entity render state, including position, orientation, animation, and light
    //overlayCoords overlay coordinates, default 0, no hurt red flash
    //color color multiplier, default -1 (white, unmodulated), maps to vanilla
    public virtual void Render(PoseStack poseStack, EntityVertexBuilder builder, EntityRenderState state)
    {
        Model.SetupAnim(state);
        //Light coordinates are read from state and filled by the caller from the light level at the entity's position; FullBright by default
        Model.RenderToBuffer(poseStack, builder, state.LightCoords, 0, -1);
    }
}
