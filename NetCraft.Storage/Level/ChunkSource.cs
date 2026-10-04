using System;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkSource 区块源抽象基类对应原版 net.minecraft.world.level.chunk.ChunkSource
//提供按 ChunkPos 与 ChunkStatus 获取区块的接口子类实现异步调度
//阶段 11.48 引入替代 PersistentServerLevel.GetChunk 同步等待
public abstract class ChunkSource : IDisposable
{
    private bool _disposed;

    //GetChunk 按 chunkX/chunkZ 获取完整区块对应原版 getChunk
    //未加载返回 null
    public abstract ChunkAccess? GetChunk(int x, int z);

    //GetChunk 按 chunkX/chunkZ 与 ChunkStatus 获取区块对应原版 getChunk
    //require 为 true 时未加载抛 UnloadedChunkException false 时返回 null
    public abstract ChunkAccess? GetChunk(int x, int z, ChunkStatus status, bool require);

    //HasChunk 判断区块是否已加载对应原版 hasChunk
    public abstract bool HasChunk(int x, int z);

    //Tick 推进区块调度对应原版 tick
    //推进 ChunkHolder 完成的区块移入缓存回收空闲 holder
    public abstract void Tick();

    //Close 释放资源对应原版 close
    public virtual void Close() { }

    public void Dispose()
    {
        Log.Debug($"Dispose entry _disposed={_disposed}");
        if (_disposed)
        {
            Log.Debug("Dispose called again, skipping");
            return;
        }
        Dispose(true);
        _disposed = true;
        GC.SuppressFinalize(this);
        //Log.Debug($"Dispose 出口");
    }

    protected virtual void Dispose(bool disposing) { }
}
