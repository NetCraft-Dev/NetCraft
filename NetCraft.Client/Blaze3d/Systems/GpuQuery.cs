namespace NetCraft.Client.Blaze3d.Systems;

//GpuQuery a single GPU query value, aligns with vanilla com.mojang.blaze3d.systems.GpuQuery
public interface GpuQuery : IDisposable
{
    //GetValue the query result, empty until the GPU has written it
    long? GetValue();
}
