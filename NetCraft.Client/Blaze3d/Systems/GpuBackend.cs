using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Render;

namespace NetCraft.Client.Blaze3d.Systems;

//GpuBackend a graphics backend, aligns with vanilla com.mojang.blaze3d.systems.GpuBackend
//NetCraft keeps a single Vulkan implementation; the empty backend exists so code runs without a GPU
public interface GpuBackend
{
    //Name the backend name reported to the device info
    string Name { get; }

    //SetWindowHints applies the backend's window creation hints before the window is created
    void SetWindowHints();

    //CreateDevice creates the logical device for a window
    //Vanilla passes a ShaderSource here; NetCraft's ShaderManager plays that role
    GpuDevice CreateDevice(long windowHandle, ShaderManager shaderManager, GpuDebugOptions debugOptions, System.Action criticalShaderLoader);
}
