namespace NetCraft.Client.Blaze3d.Buffers;

//GpuFence a GPU completion fence, aligns with vanilla com.mojang.blaze3d.buffers.GpuFence
public interface GpuFence : IDisposable
{
    //AwaitCompletion waits up to timeoutNS nanoseconds, returns true when the fence has completed, maps to vanilla awaitCompletion
    bool AwaitCompletion(long timeoutNS);
}
