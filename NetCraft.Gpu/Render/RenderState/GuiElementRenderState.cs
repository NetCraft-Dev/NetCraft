using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

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
