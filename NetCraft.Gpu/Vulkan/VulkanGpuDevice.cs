using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu.Vulkan;

//VulkanGpuDevice Vulkan 后端逻辑设备
//包装 Device + GraphicsQueue + PresentQueue + CommandPool + KhrSwapchain 扩展
//提供 Buffer/Image/Shader/CommandBuffer/CompiledRenderPipeline 创建入口
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
    //DynamicRenderingExt VK_KHR_dynamic_rendering 扩展供 VulkanRenderPass/VulkanCommandBuffer 调 CmdBeginRenderingKHR
    private KhrDynamicRendering _dynamicRenderingExtension;
    private PhysicalDeviceMemoryProperties _memoryProperties;
    //Limits 设备硬件限制构造时从 VkPhysicalDeviceLimits.maxImageDimension2D 查询
    private readonly DeviceLimits _limits;
    private bool _disposed;

    public Vk Api => _vk;
    public Device Device => _device;
    public Queue GraphicsQueue => _graphicsQueue;
    public Queue PresentQueue => _presentQueue;
    public uint GraphicsFamilyIndex => _graphicsFamily;
    //PipelineCache 声明式 RenderPipeline → CompiledRenderPipeline 编译缓存避免重复编译
    public PipelineCache PipelineCache { get; }
    public uint PresentFamilyIndex => _presentFamily;
    public CommandPool CommandPool => _commandPool;
    public KhrSwapchain SwapchainExtension => _swapchainExtension;
    //DynamicRenderingExt 暴露 KHR_dynamic_rendering 扩展实例供 RenderPass 调 CmdBeginRenderingKHR/CmdEndRenderingKHR
    public KhrDynamicRendering DynamicRenderingExt => _dynamicRenderingExtension;
    public PhysicalDevice PhysicalDevice => _context.PhysicalDevice;
    //Limits GPU 设备硬件限制对标原版 device.getDeviceInfo().limits()
    public override DeviceLimits Limits => _limits;

    //SupportsGpuRendering Vulkan 后端支持录制 GPU 渲染命令 ItemItemAtlas 走真渲染路径
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
            throw new NotSupportedException("KHR_swapchain 设备扩展不可用");
        }
        //KHR_dynamic_rendering 是 Vulkan 1.3 核心扩展 4.3 改造替代传统 RenderPass 走 CmdBeginRenderingKHR
        if (!_vk.TryGetDeviceExtension(context.Instance, _device, out _dynamicRenderingExtension))
        {
            throw new NotSupportedException("KHR_dynamic_rendering 设备扩展不可用需 Vulkan 1.3+ 或 KHR 扩展");
        }
        CreateCommandPool(indices);
        CreateDescriptorPool();
        _vk.GetPhysicalDeviceMemoryProperties(_context.PhysicalDevice, out _memoryProperties);
        //查询 VkPhysicalDeviceLimits.maxImageDimension2D 作为最大纹理尺寸对标原版 limits.maxTextureSize()
        //MinUniformBufferOffsetAlignment 供 Lighting UBO 切片对齐对标原版 limits.minUniformOffsetAlignment()
        _vk.GetPhysicalDeviceProperties(_context.PhysicalDevice, out var props);
        _limits = new DeviceLimits((int)props.Limits.MaxImageDimension2D, (int)props.Limits.MinUniformBufferOffsetAlignment);
        PipelineCache = new PipelineCache(this);
    }

    //PrecompilePipeline override 调基类 FromDeclaration+CreateDescriptorLayout+CreateRenderPipeline 编译
    //缓存由 PipelineCache.Precompile 在外部管理调用方走 PipelineCache.Precompile 命中缓存零编译
    //旧实现调 PipelineCache.Precompile 致 PipelineCache 又回调 PrecompilePipeline 无限递归已修
    public override CompiledRenderPipeline PrecompilePipeline(RenderPipeline declaration)
        => base.PrecompilePipeline(declaration);

    //CreateCommandBuffer 从命令池分配一个主级命令缓冲
    //Submit 内部用 fence 同步等待完成适合单线程串行提交
    //4.3 改造传 DynamicRenderingExt 供 VulkanCommandBuffer 调 CmdBeginRendering
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
            throw new InvalidOperationException("命令缓冲分配失败");
        }
        return new VulkanCommandBuffer(_vk, _device, _commandPool, buffer, _graphicsQueue, _dynamicRenderingExtension);
    }

    //CreateRenderPipeline 从 RenderPipelineDescription 创建 VulkanRenderPipeline
    //description.VertexShaderSource/FragmentShaderSource 可为 embedded:vert.spv 占位使用 PoC 内置 shader
    public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
    {
        return VulkanRenderPipeline.FromDescription(this, description);
    }

    //CreateBuffer 创建 VulkanBuffer 走 HostVisible+HostCoherent 内存简化无 staging
    public override GpuBuffer CreateBuffer(int size, GpuBufferUsage usage)
        => new VulkanBuffer(_vk, _device, this, size, usage);

    //CreateHostVisibleBuffer override 强制 HostVisible 内存适合每帧更新的 vertex/index buffer
    //走 map+memcpy 避免 staging 的 QueueSubmit+QueueWaitIdle 同步开销消除每帧 GPU 阻塞
    //协变返回 VulkanBuffer 现有调用方 VulkanGuiRenderer 直接赋给 VulkanBuffer 字段零改动
    public override VulkanBuffer CreateHostVisibleBuffer(int size, GpuBufferUsage usage)
        => new VulkanBuffer(_vk, _device, this, size, usage, hostVisible: true);

    //CreateImage 创建 VulkanImage 并上传初始布局转换
    public override GpuImage CreateImage(GpuImageDescription desc)
        => new VulkanImage(_vk, _device, this, desc);

    //CreateShader 创建 VkShaderModule
    public override GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main")
        => new VulkanShader(_vk, _device, stage, spirvCode, entryPoint);

    //CreateDescriptorLayout 创建 VkDescriptorSetLayout
    public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description)
        => new VulkanDescriptorLayout(_vk, _device, description);

    //AllocateDescriptorSet 从内部 pool 分配一个 VkDescriptorSet
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
            throw new InvalidOperationException("DescriptorSet 分配失败");
        return new VulkanDescriptorSet(_vk, _device, vkLayout, set);
    }

    //CreateSampler 创建 VkSampler
    public override GpuSampler CreateSampler(GpuSamplerDescription description)
        => new VulkanSampler(_vk, _device, description);

    //CreateCommandEncoder 创建 VulkanCommandEncoder 录制 copy/render pass 命令
    //替代旧 CreateCommandBuffer 分离命令编码和渲染通道
    //4.3 改造传入 DynamicRenderingExt 供 VulkanRenderPass 调 CmdBeginRendering
    public override ICommandEncoder CreateCommandEncoder()
        => new VulkanCommandEncoder(_vk, _device, this, _commandPool, _graphicsQueue, _dynamicRenderingExtension);

    //FindMemoryType 查找匹配 typeBits 和 properties 的内存类型索引
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
        throw new InvalidOperationException("未找到匹配的内存类型");
    }

    //CreateBufferInternal 内部创建原生 VkBuffer+DeviceMemory 供 VulkanBuffer/VulkanImage 复用
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
            throw new InvalidOperationException("Buffer 创建失败");
        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);
        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };
        DeviceMemory memory;
        if (_vk.AllocateMemory(_device, &allocInfo, null, &memory) != Result.Success)
            throw new InvalidOperationException("Memory 分配失败");
        _vk.BindBufferMemory(_device, buffer, memory, 0);
        return (buffer, memory);
    }

    //RunOneTimeCommand 提交一次性命令缓冲执行完等待完成
    //用于 Image 布局转换和 buffer 拷贝
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

    //WaitIdle 等待设备所有队列空闲
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
        //KHR_swapchain 交换链 + KHR_dynamic_rendering 4.3 改造替代传统 RenderPass
        string[] deviceExtensions = { KhrSwapchain.ExtensionName, KhrDynamicRendering.ExtensionName };
        var enabledExtNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions);
        //PhysicalDeviceDynamicRenderingFeaturesKHR 通过 PNext 链启用 dynamic rendering 特性
        //Vulkan 1.3+ 必须显式启用 VK_TRUE 才允许 CmdBeginRenderingKHR 调用
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
            throw new InvalidOperationException("VkDevice 创建失败");
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
            throw new InvalidOperationException("命令池创建失败");
        }
        _commandPool = pool;
    }

    //CreateDescriptorPool 创建 uniform buffer 和 combined image sampler 各 100 个的描述符池
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
                throw new InvalidOperationException("DescriptorPool 创建失败");
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
