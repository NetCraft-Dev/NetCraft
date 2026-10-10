using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Gui.Render.State;

//GuiElementRenderState GUI element render state interface, maps to vanilla GuiElementRenderState
//RenderState subclasses implement this interface, submitting to GuiRenderState to participate in SortElements sorting and batching
//Immutable value object carrying pipeline/texture/pose snapshot/scissor snapshot
public interface GuiElementRenderState
{
    //BuildVertices builds vertices into the consumer
    void BuildVertices(IVertexConsumer consumer);
    //Pipeline declarative render pipeline compiled by GuiRenderer via PipelineCache into a CompiledRenderPipeline
    RenderPipeline Pipeline { get; }
    //TextureSetup texture binding config
    TextureSetup TextureSetup { get; }
    //ScissorArea scissor rectangle
    ScreenRectangle ScissorArea { get; }
    //Bounds used for level intersection tests, computed from the element geometry+pose+scissor
    ScreenRectangle Bounds { get; }
}
