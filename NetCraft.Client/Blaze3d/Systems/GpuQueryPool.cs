namespace NetCraft.Client.Blaze3d.Systems;

//GpuQueryPool a pool of query slots, aligns with vanilla com.mojang.blaze3d.systems.GpuQueryPool
public interface GpuQueryPool : IDisposable
{
    //Size the number of query slots
    int Size { get; }

    //GetValue the value of one slot, empty until the GPU has written it
    long? GetValue(int index);

    //GetValues the values of a slot range, empty entries for slots not yet written
    long?[] GetValues(int firstIndex, int count);
}
