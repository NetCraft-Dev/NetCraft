namespace NetCraft.Gpu;

//GpuBackend GPU 后端枚举
//Vulkan 跨平台主推后端
//Empty 空后端无渲染用于无 GPU 环境
//Software 软件渲染后端备用
public enum GpuBackend
{
    Empty,
    Vulkan,
    Software
}
