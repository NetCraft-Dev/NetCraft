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
namespace NetCraft.Client.Blaze3d.Systems;

//IRenderPass render pass, maps to vanilla RenderPass
//After CreateRenderPass returns, render commands are recorded; Close ends the render pass
//Replaces the BeginRenderPass/EndRenderPass mix of the legacy GpuCommandBuffer
public interface IRenderPass : IDisposable
{
    //SetPipeline binds the graphics pipeline
    void SetPipeline(CompiledRenderPipeline pipeline);
    //SetVertexBuffer binds a vertex buffer to a binding slot
    void SetVertexBuffer(int slot, GpuBuffer buffer, ulong offset = 0);
    //SetIndexBuffer binds an index buffer
    void SetIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0);
    //BindDescriptorSet binds a descriptor set to the setIndex slot of the pipeline layout
    void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0);
    //EnableScissor enables the dynamic scissor rectangle, pixel coordinates with the top-left origin and y downward
    void EnableScissor(int x, int y, int width, int height);
    //DisableScissor disables clipping for full-screen rendering
    void DisableScissor();
    //Draw non-indexed draw
    void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);
    //DrawIndexed indexed draw; vertexOffset is the base vertex offset
    void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);
    //Close ends the render pass; later commands are recorded into the owning CommandEncoder
    void Close();
}
