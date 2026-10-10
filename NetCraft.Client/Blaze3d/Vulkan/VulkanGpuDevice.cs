using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;
using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;

using CompiledRenderPipeline = NetCraft.Client.Blaze3d.Pipeline.CompiledRenderPipeline;
using RenderPipelineDescription = NetCraft.Client.Blaze3d.Pipeline.RenderPipelineDescription;
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

//VulkanGpuDevice Vulkan backend logical device
//Wraps Device + GraphicsQueue + PresentQueue + CommandPool + the KhrSwapchain extension
//Provides creation entry points for Buffer/Image/Shader/CommandBuffer/CompiledRenderPipeline
public sealed unsafe class VulkanGpuDevice : GpuDevice
{
    private readonly VulkanGpuContext _context;
    private readonly Vk _vk;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    private uint _graphicsFamily;
    private uint _presentFamily;
    private CommandPool _commandPool;
    private DescriptorPool _descriptorPool;
    private KhrSwapchain _swapchainExtension;
    //DynamicRenderingExt the VK_KHR_dynamic_rendering extension for VulkanRenderPass/VulkanCommandBuffer to call CmdBeginRenderingKHR
    private KhrDynamicRendering _dynamicRenderingExtension;
    private PhysicalDeviceMemoryProperties _memoryProperties;
    //Limits device hardware limits queried from VkPhysicalDeviceLimits.maxImageDimension2D at construction
    private readonly DeviceLimits _limits;
    private bool _disposed;

    public Vk Api => _vk;
    public Device Device => _device;
    public Queue GraphicsQueue => _graphicsQueue;
    public Queue PresentQueue => _presentQueue;
    public uint GraphicsFamilyIndex => _graphicsFamily;
    //PipelineCache declarative RenderPipeline → CompiledRenderPipeline compile cache avoiding recompilation
    public NetCraft.Client.Blaze3d.Pipeline.PipelineCache PipelineCache { get; }
    public uint PresentFamilyIndex => _presentFamily;
    public CommandPool CommandPool => _commandPool;
    public KhrSwapchain SwapchainExtension => _swapchainExtension;
    //DynamicRenderingExt exposes the KHR_dynamic_rendering extension instance for RenderPass to call CmdBeginRenderingKHR/CmdEndRenderingKHR
    public KhrDynamicRendering DynamicRenderingExt => _dynamicRenderingExtension;
    public PhysicalDevice PhysicalDevice => _context.PhysicalDevice;
    //Limits GPU hardware limits, maps to vanilla device.getDeviceInfo().limits()
    public override DeviceLimits Limits => _limits;

    //SupportsGpuRendering the Vulkan backend supports recording GPU render commands so ItemItemAtlas takes the real render path
    public override bool SupportsGpuRendering => true;

    internal VulkanGpuDevice(VulkanGpuContext context, GpuDeviceOptions options) : base(context)
    {
        _context = context;
        _vk = context.Api;
        var indices = context.FindQueueFamilies(context.PhysicalDevice);
        CreateLogicalDevice(indices, options);
        _vk.CurrentDevice = _device;
        if (!_vk.TryGetDeviceExtension(context.Instance, _device, out _swapchainExtension))
        {
            throw new NotSupportedException("The KHR_swapchain device extension is unavailable");
        }
        //KHR_dynamic_rendering is a Vulkan 1.3 core extension; the 4.3 rework replaces the traditional RenderPass with CmdBeginRenderingKHR
        if (!_vk.TryGetDeviceExtension(context.Instance, _device, out _dynamicRenderingExtension))
        {
            throw new NotSupportedException("The KHR_dynamic_rendering device extension is unavailable; Vulkan 1.3+ or the KHR extension is required");
        }
        CreateCommandPool(indices);
        CreateDescriptorPool();
        _vk.GetPhysicalDeviceMemoryProperties(_context.PhysicalDevice, out _memoryProperties);
        //Queries VkPhysicalDeviceLimits.maxImageDimension2D as the max texture size, maps to vanilla limits.maxTextureSize()
        //MinUniformBufferOffsetAlignment used by Lighting for UBO slice alignment, maps to vanilla limits.minUniformOffsetAlignment()
        _vk.GetPhysicalDeviceProperties(_context.PhysicalDevice, out var props);
        _limits = new DeviceLimits((int)props.Limits.MaxImageDimension2D, (int)props.Limits.MinUniformBufferOffsetAlignment);
        PipelineCache = new PipelineCache(this);
    }

    //PrecompilePipeline override calls the base's FromDeclaration+CreateDescriptorLayout+CreateRenderPipeline to compile
    //The cache is managed externally by PipelineCache.Precompile; callers go through PipelineCache.Precompile for a zero-compile cache hit
    //The old implementation called PipelineCache.Precompile, which called back into PrecompilePipeline and recursed infinitely; fixed
    public override CompiledRenderPipeline PrecompilePipeline(RenderPipeline declaration)
        => base.PrecompilePipeline(declaration);

    //CreateCommandBuffer allocates a primary command buffer from the command pool
    //Submit internally uses a fence to wait synchronously, suited to single-threaded serial submission
    //4.3 rework passes DynamicRenderingExt for VulkanCommandBuffer to call CmdBeginRendering
    public override GpuCommandBuffer CreateCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        CommandBuffer buffer;
        if (_vk.AllocateCommandBuffers(_device, &allocInfo, &buffer) != Result.Success)
        {
            throw new InvalidOperationException("Command buffer allocation failed");
        }
        return new VulkanCommandBuffer(_vk, _device, _commandPool, buffer, _graphicsQueue, _dynamicRenderingExtension);
    }

    //CreateRenderPipeline creates a VulkanRenderPipeline from a RenderPipelineDescription
    //description.VertexShaderSource/FragmentShaderSource may be an embedded:vert.spv placeholder using the PoC's built-in shader
    public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
    {
        return VulkanRenderPipeline.FromDescription(this, description);
    }

    //CreateBuffer creates a VulkanBuffer using host-visible+host-coherent memory, simplified without staging
    public override GpuBuffer CreateBuffer(int size, GpuBufferUsage usage)
        => new VulkanBuffer(_vk, _device, this, size, usage);

    //CreateHostVisibleBuffer override forces host-visible memory, suited to per-frame vertex/index buffers
    //Uses map+memcpy to avoid staging's QueueSubmit+QueueWaitIdle synchronization cost, removing the per-frame GPU block
    //Covariant return of VulkanBuffer; existing callers like VulkanGuiRenderer assign it directly to a VulkanBuffer field with no change
    public override VulkanBuffer CreateHostVisibleBuffer(int size, GpuBufferUsage usage)
        => new VulkanBuffer(_vk, _device, this, size, usage, hostVisible: true);

    //CreateImage creates a VulkanImage and performs the initial layout transition
    public override GpuImage CreateImage(GpuImageDescription desc)
        => new VulkanImage(_vk, _device, this, desc);

    //CreateShader creates a VkShaderModule
    public override GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main")
        => new VulkanShader(_vk, _device, stage, spirvCode, entryPoint);

    //CreateDescriptorLayout creates a VkDescriptorSetLayout
    public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description)
        => new VulkanDescriptorLayout(_vk, _device, description);

    //AllocateDescriptorSet allocates a VkDescriptorSet from the internal pool
    public override GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout layout)
    {
        var vkLayout = (VulkanDescriptorLayout)layout;
        var layouts = stackalloc DescriptorSetLayout[1];
        layouts[0] = vkLayout.Handle;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = layouts
        };
        DescriptorSet set;
        if (_vk.AllocateDescriptorSets(_device, &allocInfo, &set) != Result.Success)
            throw new InvalidOperationException("DescriptorSet allocation failed");
        return new VulkanDescriptorSet(_vk, _device, vkLayout, set);
    }

    //CreateSampler creates a VkSampler
    public override GpuSampler CreateSampler(GpuSamplerDescription description)
        => new VulkanSampler(_vk, _device, description);

    //CreateCommandEncoder creates a VulkanCommandEncoder to record copy/render pass commands
    //Replaces the legacy CreateCommandBuffer, separating command encoding from render passes
    //4.3 rework passes in DynamicRenderingExt for VulkanRenderPass to call CmdBeginRendering
    public override ICommandEncoder CreateCommandEncoder()
        => new VulkanCommandEncoder(_vk, _device, this, _commandPool, _graphicsQueue, _dynamicRenderingExtension);

    //FindMemoryType finds the memory type index matching typeBits and properties
    public uint FindMemoryType(uint typeBits, MemoryPropertyFlags properties)
    {
        for (int i = 0; i < _memoryProperties.MemoryTypeCount; i++)
        {
            if ((typeBits & (1u << i)) != 0 &&
                (_memoryProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }
        throw new InvalidOperationException("No matching memory type found");
    }

    //CreateBufferInternal internally creates a native VkBuffer+DeviceMemory reused by VulkanBuffer/VulkanImage
    internal (Silk.NET.Vulkan.Buffer handle, DeviceMemory memory) CreateBufferInternal(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };
        Buffer buffer;
        if (_vk.CreateBuffer(_device, &bufferInfo, null, &buffer) != Result.Success)
            throw new InvalidOperationException("Buffer creation failed");
        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);
        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };
        DeviceMemory memory;
        if (_vk.AllocateMemory(_device, &allocInfo, null, &memory) != Result.Success)
            throw new InvalidOperationException("Memory allocation failed");
        _vk.BindBufferMemory(_device, buffer, memory, 0);
        return (buffer, memory);
    }

    //RunOneTimeCommand submits a one-time command buffer and waits for completion
    //Used for image layout transitions and buffer copies
    public void RunOneTimeCommand(Action<CommandBuffer> record)
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        CommandBuffer cmd;
        _vk.AllocateCommandBuffers(_device, &allocInfo, &cmd);
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(cmd, &beginInfo);
        try
        {
            record(cmd);
        }
        finally
        {
            _vk.EndCommandBuffer(cmd);
        }
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };
        _vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, default);
        _vk.QueueWaitIdle(_graphicsQueue);
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &cmd);
    }

    //WaitIdle waits for all device queues to idle
    public void WaitIdle()
    {
        _vk.DeviceWaitIdle(_device);
    }

    private void CreateLogicalDevice(QueueFamilyIndices indices, GpuDeviceOptions options)
    {
        var uniqueQueueFamilies = indices.GraphicsFamily.Value == indices.PresentFamily.Value
            ? new[] { indices.GraphicsFamily.Value }
            : new[] { indices.GraphicsFamily.Value, indices.PresentFamily.Value };
        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[uniqueQueueFamilies.Length];
        float queuePriority = 1f;
        for (int i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }
        var deviceFeatures = new PhysicalDeviceFeatures();
        //KHR_swapchain swapchain + KHR_dynamic_rendering 4.3 rework replacing the traditional RenderPass
        string[] deviceExtensions = { KhrSwapchain.ExtensionName, KhrDynamicRendering.ExtensionName };
        var enabledExtNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions);
        //PhysicalDeviceDynamicRenderingFeaturesKHR enables the dynamic rendering feature via the PNext chain
        //Vulkan 1.3+ requires explicitly enabling VK_TRUE before CmdBeginRenderingKHR may be called
        var dynamicRenderingFeatures = new PhysicalDeviceDynamicRenderingFeaturesKHR
        {
            SType = StructureType.PhysicalDeviceDynamicRenderingFeatures,
            DynamicRendering = Vk.True
        };
        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &dynamicRenderingFeatures,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = enabledExtNames
        };
        Device device;
        if (_vk.CreateDevice(_context.PhysicalDevice, &createInfo, null, &device) != Result.Success)
        {
            throw new InvalidOperationException("VkDevice creation failed");
        }
        _device = device;
        _graphicsFamily = indices.GraphicsFamily.Value;
        _presentFamily = indices.PresentFamily.Value;
        _vk.GetDeviceQueue(_device, _graphicsFamily, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, _presentFamily, 0, out _presentQueue);
        SilkMarshal.Free((nint)enabledExtNames);
    }

    private void CreateCommandPool(QueueFamilyIndices indices)
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = indices.GraphicsFamily.Value,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };
        CommandPool pool;
        if (_vk.CreateCommandPool(_device, &poolInfo, null, &pool) != Result.Success)
        {
            throw new InvalidOperationException("Command pool creation failed");
        }
        _commandPool = pool;
    }

    //CreateDescriptorPool creates a descriptor pool with 100 uniform buffers and 100 combined image samplers
    private void CreateDescriptorPool()
    {
        var poolSizes = new[]
        {
            new DescriptorPoolSize { Type = DescriptorType.UniformBuffer, DescriptorCount = 100 },
            new DescriptorPoolSize { Type = DescriptorType.CombinedImageSampler, DescriptorCount = 100 }
        };
        fixed (DescriptorPoolSize* p = poolSizes)
        {
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = p,
                MaxSets = 100,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit
            };
            if (_vk.CreateDescriptorPool(_device, &poolInfo, null, out _descriptorPool) != Result.Success)
                throw new InvalidOperationException("DescriptorPool creation failed");
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
        _vk.DestroyCommandPool(_device, _commandPool, null);
        _vk.DestroyDevice(_device, null);
        _swapchainExtension?.Dispose();
        _dynamicRenderingExtension?.Dispose();
        _disposed = true;
    }
}
