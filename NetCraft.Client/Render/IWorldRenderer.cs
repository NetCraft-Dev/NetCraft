using System.Numerics;
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

namespace NetCraft.Client.Render;

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
