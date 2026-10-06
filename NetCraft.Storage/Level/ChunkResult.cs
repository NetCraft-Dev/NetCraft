using System;

namespace NetCraft.Storage;

//ChunkResult, chunk load result wrapper, maps to vanilla net.minecraft.world.level.chunk.ChunkResult
//Either<ChunkAccess, UnloadedChunkException> wraps load success or failure, avoiding exceptions that break the scheduling chain
public sealed class ChunkResult
{
    private readonly ChunkAccess? _chunk;
    private readonly UnloadedChunkException? _error;

    public bool IsSuccess => _chunk is not null;

    //Chunk, the chunk on success, null on failure
    public ChunkAccess? Chunk => _chunk;

    //Error, the exception on failure, null on success
    public UnloadedChunkException? Error => _error;

    private ChunkResult(ChunkAccess? chunk, UnloadedChunkException? error)
    {
        _chunk = chunk;
        _error = error;
    }

    //Success builds a success result, maps to vanilla ChunkResult.of
    public static ChunkResult Success(ChunkAccess chunk) => new(chunk, null);

    //Failure builds a failure result, maps to vanilla ChunkResult.error
    public static ChunkResult Failure(UnloadedChunkException error) => new(null, error);

    //OrElse returns other on failure, own chunk on success, maps to vanilla ChunkResult.orElse
    public ChunkAccess? OrElse(ChunkAccess? other) => _chunk ?? other;

    //IfSuccess runs action on success, skips on failure, maps to vanilla ChunkResult.ifSuccess
    public void IfSuccess(Action<ChunkAccess> action)
    {
        if (_chunk is not null) action(_chunk);
    }
}
