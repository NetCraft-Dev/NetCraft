using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace NetCraft.Gpu.Vulkan;

//VulkanGpuContext Vulkan backend GPU context
//Wraps Vk API + Instance + PhysicalDevice providing the creation entry point for VulkanGpuDevice
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

    //Surface the platform surface created and injected by VulkanTriangleApp, used to query the present queue family
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
            throw new NotSupportedException("The KHR_surface extension is unavailable");
        }
    }

    //CreateDevice creates the logical device and returns a VulkanGpuDevice
    //The Surface property must be set before calling
    public override GpuDevice CreateDevice(GpuDeviceOptions options)
    {
        if (!_surfaceCreated)
        {
            throw new InvalidOperationException("Surface is not set, cannot create the device");
        }
        return new VulkanGpuDevice(this, options);
    }

    //FindQueueFamilies finds the physical device's graphics and present queue families
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

    //IsDeviceSuitable checks whether the physical device supports the graphics queue and swapchain extension
    public bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);
        return indices.IsComplete();
    }

    private void CreateInstance(IWindow window, bool enableValidation)
    {
        if (window.VkSurface is null)
        {
            throw new NotSupportedException("The window platform does not support Vulkan");
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
            throw new InvalidOperationException("VkInstance creation failed");
        }
        _instance = instance;
        _vk.CurrentInstance = _instance;
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
    }

    //PickPhysicalDevice selects a physical GPU supporting graphics + present
    //Must be called after Surface is set, otherwise FindQueueFamilies cannot get the present queue
    public void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
        if (deviceCount == 0)
        {
            throw new NotSupportedException("No GPU supporting Vulkan was found");
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
        throw new NotSupportedException("No suitable graphics GPU device");
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

//QueueFamilyIndices graphics and present queue family indices
public struct QueueFamilyIndices
{
    public uint? GraphicsFamily;
    public uint? PresentFamily;

    public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
}
