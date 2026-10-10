using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;

using CompiledRenderPipeline = NetCraft.Client.Blaze3d.Pipeline.CompiledRenderPipeline;
using RenderPipelineDescription = NetCraft.Client.Blaze3d.Pipeline.RenderPipelineDescription;
using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using PipelineCache = NetCraft.Client.Blaze3d.Pipeline.PipelineCache;
using BindGroupLayout = NetCraft.Client.Blaze3d.Pipeline.BindGroupLayout;
using ColorTargetState = NetCraft.Client.Blaze3d.Pipeline.ColorTargetState;
using DepthStencilState = NetCraft.Client.Blaze3d.Pipeline.DepthStencilState;
using NetCraft.Client.Blaze3d.Pipeline;
using CompareOp = Silk.NET.Vulkan.CompareOp;
using BlendFactor = Silk.NET.Vulkan.BlendFactor;
using BlendOp = Silk.NET.Vulkan.BlendOp;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using PrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
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
namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanShader Vulkan backend SPIR-V shader module
//Wraps VkShaderModule created by VulkanGpuDevice.CreateShader
public sealed unsafe class VulkanShader : GpuShader
{
    private readonly Vk _vk;
    private readonly Device _device;
    private ShaderModule _module;
    private bool _disposed;

    public ShaderModule Handle => _module;

    internal VulkanShader(Vk vk, Device device, GpuShaderStage stage, byte[] spirvCode, string entryPoint)
        : base(stage, spirvCode, entryPoint)
    {
        _vk = vk;
        _device = device;
        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)spirvCode.Length
        };
        fixed (byte* codePtr = spirvCode)
        {
            createInfo.PCode = (uint*)codePtr;
            if (_vk.CreateShaderModule(_device, &createInfo, null, out _module) != Result.Success)
                throw new InvalidOperationException("ShaderModule creation failed");
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyShaderModule(_device, _module, null);
        _disposed = true;
    }
}
