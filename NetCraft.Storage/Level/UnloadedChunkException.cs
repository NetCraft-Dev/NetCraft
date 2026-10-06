using System;

namespace NetCraft.Storage;

//UnloadedChunkException, chunk-not-loaded exception, maps to vanilla net.minecraft.world.level.chunk.ChunkLoadingFailure
//Used as the right side of ChunkResult's Either to wrap a load failure for the scheduling chain, instead of throwing and aborting
public sealed class UnloadedChunkException : Exception
{
    public UnloadedChunkException() : base("Chunk is not loaded") { }

    public UnloadedChunkException(string message) : base(message) { }

    public UnloadedChunkException(string message, Exception inner) : base(message, inner) { }
}
