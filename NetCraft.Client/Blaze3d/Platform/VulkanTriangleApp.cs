using System.Numerics;
using Silk.NET.Vulkan;
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

namespace NetCraft.Client.Blaze3d.Platform;

//VulkanTriangleApp Vulkan triangle PoC main program
//Extends VulkanAppBase and only implements pipeline creation and command recording; the rest is handled by the base
public sealed unsafe class VulkanTriangleApp : VulkanAppBase
{
    private VulkanRenderPipeline _pipeline = null!;

    public VulkanTriangleApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu.Vulkan Triangle PoC";

    //OnCreatePipelineResources creates the triangle pipeline using the built-in SpirvShaders shader
    protected override void OnCreatePipelineResources()
    {
        //The PoC uses the built-in SpirvShaders shader and leaves description null, using VulkanRenderPipeline defaults
        var description = new RenderPipelineDescription();
        _pipeline = new VulkanRenderPipeline(_vkDevice.Api, _vkDevice.Device, _swapchainImageFormat, _swapchainExtent, description);
    }

    //OnRecordCommandBuffer records one triangle into the scene texture
    protected override void OnRecordCommandBuffer(CommandEncoder encoder, GpuTextureView sceneView)
    {
        var descriptor = RenderPassDescriptor.Create(() => "triangle")
            .WithColorAttachment(sceneView, new Vector4(0.1f, 0.1f, 0.1f, 1f))
            .WithRenderArea(new NetCraft.Client.Blaze3d.Systems.RenderPass.RenderArea(0, 0, (int)_swapchainExtent.Width, (int)_swapchainExtent.Height));
        var pass = (VulkanRenderPass)encoder.Backend.CreateRenderPass(descriptor);
        pass.SetCompiledPipeline(_pipeline);
        pass.Draw(3, 1, 0, 0);
        encoder.SubmitRenderPass();
    }

    //OnCleanupPipelineResources destroys pipeline resources
    protected override void OnCleanupPipelineResources()
    {
        _pipeline.Dispose();
    }
}
