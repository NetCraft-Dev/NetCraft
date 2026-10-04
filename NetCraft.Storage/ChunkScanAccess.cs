using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//区块扫描接口对应原版ChunkScanAccess
//支持按流式visitor扫描chunk不构建完整Tag对象用于blending等扫描场景
public interface ChunkScanAccess
{
    Task ScanChunk(ChunkPos pos, StreamTagVisitor visitor);
}
