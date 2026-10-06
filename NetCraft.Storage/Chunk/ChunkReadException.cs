namespace NetCraft.Storage.Chunk;

//ChunkReadException, maps to vanilla net.minecraft.world.level.chunk.storage.ChunkReadException
//Thrown when chunk deserialization fails, carrying the original error message
public sealed class ChunkReadException : Exception
{
    public ChunkReadException(string message) : base(message) { }
    public ChunkReadException(string message, Exception innerException) : base(message, innerException) { }
}
