namespace NetCraft.Util;

//MemoryReserve holds 10 MiB that is dropped right before a crash report is assembled, maps to vanilla net.minecraft.util.MemoryReserve
//Vanilla keeps it so an out-of-memory crash still has room to build its report; on the CLR the block is ordinary
//managed memory, so clearing the reference and collecting is enough to hand the space back
public static class MemoryReserve
{
    //ReserveSize the block size vanilla reserves, 0xA00000 bytes
    private const int ReserveSize = 0xA00000;

    private static byte[]? _reserve;

    //Allocate reserves the block, maps to vanilla allocate
    public static void Allocate() => _reserve = new byte[ReserveSize];

    //Release drops the block and asks for a collection, maps to vanilla release
    //Only runs once per process; reclaiming the reserve is best effort and must never break the crash path
    public static void Release()
    {
        if (_reserve is null) return;
        _reserve = null;
        try
        {
            GC.Collect();
            GC.Collect();
            GC.Collect();
        }
        catch
        {
            //a failed collection still leaves the block unreferenced, so there is nothing to report
        }
    }
}
