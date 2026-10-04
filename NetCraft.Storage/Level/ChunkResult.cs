using System;

namespace NetCraft.Storage;

//ChunkResult 区块加载结果包装对应原版 net.minecraft.world.level.chunk.ChunkResult
//Either<ChunkAccess, UnloadedChunkException> 包装加载成功或失败避免抛异常中断调度链
public sealed class ChunkResult
{
    private readonly ChunkAccess? _chunk;
    private readonly UnloadedChunkException? _error;

    public bool IsSuccess => _chunk is not null;

    //Chunk 成功时的区块失败时返回 null
    public ChunkAccess? Chunk => _chunk;

    //Error 失败时的异常成功时返回 null
    public UnloadedChunkException? Error => _error;

    private ChunkResult(ChunkAccess? chunk, UnloadedChunkException? error)
    {
        _chunk = chunk;
        _error = error;
    }

    //Success 构造成功结果对应原版 ChunkResult.of
    public static ChunkResult Success(ChunkAccess chunk) => new(chunk, null);

    //Failure 构造失败结果对应原版 ChunkResult.error
    public static ChunkResult Failure(UnloadedChunkException error) => new(null, error);

    //OrElse 失败时返回 other 成功时返回自身 chunk 对应原版 ChunkResult.orElse
    public ChunkAccess? OrElse(ChunkAccess? other) => _chunk ?? other;

    //IfSuccess 成功时执行 action 失败时跳过对应原版 ChunkResult.ifSuccess
    public void IfSuccess(Action<ChunkAccess> action)
    {
        if (_chunk is not null) action(_chunk);
    }
}
