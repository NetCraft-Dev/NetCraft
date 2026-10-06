using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Storage;

//FileMove, the from/to paths of one file move, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.FileMove
public record FileMove(string From, string To)
{
    //MoveCodec, both paths are confined to their own directory, maps to vanilla moveCodec
    public static Codec<FileMove> MoveCodec(string fromDirectory, string toDirectory) => RecordCodecBuilder.Of2(
        ExtraCodecs.GuardedPathCodec(fromDirectory).FieldOf("from").ForGetter((FileMove move) => move.From),
        ExtraCodecs.GuardedPathCodec(toDirectory).FieldOf("to").ForGetter((FileMove move) => move.To),
        (from, to) => new FileMove(from, to));
}
