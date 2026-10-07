using System.Numerics;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//IWorldRenderer world rendering abstraction interface
//LevelRenderer implements this in NetCraft.Game, avoiding a circular dependency where VulkanGuiApp references NetCraft.Game
//VulkanGuiApp holds an IWorldRenderer and calls Prepare/Upload/Draw in OnRecordCommandBuffer
//The ViewProj property lets VulkanGuiApp upload the ViewProj UBO to the shader
public interface IWorldRenderer
{
    //Prepare builds mesh data into the internal StagedVertexBuffer, rebuilt every frame; W8 changes it to async caching
    void Prepare();

    //Upload uploads vertices/indices to the GPU, reusing buffers across frames
    void Upload(GpuDevice device);

    //ViewProj the current frame's view*proj matrix; VulkanGuiApp reads this to upload set 0 UBO
    Matrix4x4 ViewProj { get; }

    //Perf metrics for GameScreen F3 to display world render stats
    int SectionCount { get; }
    int VisibleSectionCount { get; }
    int TotalVertexCount { get; }
    int DrawCallCount { get; }

    //Draw renders in Solid→Cutout→Translucent order; pipelineResolver compiles the pipeline and descBinder binds the descriptor set
    void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder);
}
