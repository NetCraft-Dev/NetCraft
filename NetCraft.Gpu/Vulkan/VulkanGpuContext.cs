using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace NetCraft.Gpu.Vulkan;

//VulkanGpuContext Vulkan 后端 GPU 上下文
//包装 Vk API + Instance + PhysicalDevice 提供给 VulkanGpuDevice 创建入口
public sealed unsafe class VulkanGpuContext : GpuContext
{
    private readonly Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private KhrSurface _khrSurface;
    private SurfaceKHR _surface;
    private bool _surfaceCreated;
    private bool _disposed;

    public Vk Api => _vk;
    public Instance Instance => _instance;
    public PhysicalDevice PhysicalDevice => _physicalDevice;
    public KhrSurface SurfaceExtension => _khrSurface;

    //Surface 平台 surface 由 VulkanTriangleApp 创建后注入用于查询 present 队列族
    public SurfaceKHR Surface
    {
        get => _surface;
        set
        {
            _surface = value;
            _surfaceCreated = true;
        }
    }

    public VulkanGpuContext(IWindow window, bool enableValidation = false) : base(GpuBackend.Vulkan)
    {
        _vk = Vk.GetApi();
        CreateInstance(window, enableValidation);
        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
        {
            throw new NotSupportedException("KHR_surface 扩展不可用");
        }
    }

    //CreateDevice 创建逻辑设备并返回 VulkanGpuDevice
    //调用前必须已设置 Surface 属性
    public override GpuDevice CreateDevice(GpuDeviceOptions options)
    {
        if (!_surfaceCreated)
        {
            throw new InvalidOperationException("Surface 未设置无法创建 device");
        }
        return new VulkanGpuDevice(this, options);
    }

    //FindQueueFamilies 查找物理设备的 graphics 和 present 队列族
    public QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();
        uint queryFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, null);
        var queueFamilies = stackalloc QueueFamilyProperties[(int)queryFamilyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, queueFamilies);
        for (uint i = 0; i < queryFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
            {
                indices.GraphicsFamily = i;
            }
            _khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
            if (presentSupport == Vk.True)
            {
                indices.PresentFamily = i;
            }
            if (indices.IsComplete())
            {
                break;
            }
        }
        return indices;
    }

    //IsDeviceSuitable 判断物理设备是否支持 graphics 队列和 swapchain 扩展
    public bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);
        return indices.IsComplete();
    }

    private void CreateInstance(IWindow window, bool enableValidation)
    {
        if (window.VkSurface is null)
        {
            throw new NotSupportedException("窗口平台不支持 Vulkan");
        }
        byte** requiredExts = window.VkSurface.GetRequiredExtensions(out uint extCount);
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("NetCraft.Gpu.Vulkan"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("NetCraft"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version11
        };
        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = extCount,
            PpEnabledExtensionNames = requiredExts
        };
        if (enableValidation)
        {
            createInfo.EnabledLayerCount = 1;
            var layerName = (byte*)SilkMarshal.StringToPtr("VK_LAYER_KHRONOS_validation");
            createInfo.PpEnabledLayerNames = &layerName;
        }
        Instance instance;
        if (_vk.CreateInstance(&createInfo, null, &instance) != Result.Success)
        {
            throw new InvalidOperationException("VkInstance 创建失败");
        }
        _instance = instance;
        _vk.CurrentInstance = _instance;
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
    }

    //PickPhysicalDevice 选择支持 graphics + present 的物理 GPU
    //必须在 Surface 设置后调用否则 FindQueueFamilies 取不到 present 队列
    public void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
        if (deviceCount == 0)
        {
            throw new NotSupportedException("未找到支持 Vulkan 的 GPU");
        }
        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        _vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices);
        for (int i = 0; i < deviceCount; i++)
        {
            if (IsDeviceSuitable(devices[i]))
            {
                _physicalDevice = devices[i];
                return;
            }
        }
        throw new NotSupportedException("没有合适的图形 GPU 设备");
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _khrSurface?.Dispose();
        if (_surfaceCreated && _surface.Handle != 0)
        {
            _khrSurface?.DestroySurface(_instance, _surface, null);
        }
        if (_instance.Handle != 0)
        {
            _vk.DestroyInstance(_instance, null);
        }
        _vk.Dispose();
        _disposed = true;
    }
}

//QueueFamilyIndices graphics 和 present 队列族索引
public struct QueueFamilyIndices
{
    public uint? GraphicsFamily;
    public uint? PresentFamily;

    public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
}
