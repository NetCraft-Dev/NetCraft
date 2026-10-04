namespace NetCraft.Storage.Chunk;

//ChunkReadException对应原版net.minecraft.world.level.chunk.storage.ChunkReadException
//区块反序列化失败时抛出携带原始错误信息
public sealed class ChunkReadException : Exception
{
    public ChunkReadException(string message) : base(message) { }
    public ChunkReadException(string message, Exception innerException) : base(message, innerException) { }
}
