namespace NetCraft.Storage;

//FileMove 一次文件搬运的起止路径 对应原版 net.minecraft.util.filefix.virtualfilesystem.FileMove
//原版另带 moveCodec 依赖 ExtraCodecs.guardedPathCodec 该类型尚未移植 这里只留数据
public record FileMove(string From, string To);
