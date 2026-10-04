using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Storage;

//FileMove 一次文件搬运的起止路径 对应原版 net.minecraft.util.filefix.virtualfilesystem.FileMove
public record FileMove(string From, string To)
{
    //MoveCodec 起止路径都限定在各自目录内 对应原版 moveCodec
    public static Codec<FileMove> MoveCodec(string fromDirectory, string toDirectory) => RecordCodecBuilder.Of2(
        ExtraCodecs.GuardedPathCodec(fromDirectory).FieldOf("from").ForGetter((FileMove move) => move.From),
        ExtraCodecs.GuardedPathCodec(toDirectory).FieldOf("to").ForGetter((FileMove move) => move.To),
        (from, to) => new FileMove(from, to));
}
