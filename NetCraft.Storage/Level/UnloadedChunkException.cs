using System;

namespace NetCraft.Storage;

//UnloadedChunkException 区块未加载异常对应原版 net.minecraft.world.level.chunk.ChunkLoadingFailure
//ChunkResult 的 Either 右侧用于包装加载失败场景供调度链传递而非抛异常中断
public sealed class UnloadedChunkException : Exception
{
    public UnloadedChunkException() : base("Chunk is not loaded") { }

    public UnloadedChunkException(string message) : base(message) { }

    public UnloadedChunkException(string message, Exception inner) : base(message, inner) { }
}
