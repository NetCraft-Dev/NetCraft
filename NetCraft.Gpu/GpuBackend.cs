namespace NetCraft.Gpu;

//GpuBackend GPU backend enum
//Vulkan cross-platform primary backend
//Empty null backend, no rendering, for environments without a GPU
//Software software rendering backend, as a fallback
public enum GpuBackend
{
    Empty,
    Vulkan,
    Software
}
