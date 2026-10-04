namespace NetCraft.Nbt;

//NBT 通用运行时异常。对应原版 net.minecraft.nbt.NbtException（继承 java.lang.RuntimeException）。
//C# 中映射为 System.Exception（运行时异常语义）。
public class NbtException(string message) : Exception(message)
{
}

//NBT 格式异常。对应原版 net.minecraft.nbt.NbtFormatException。
//表示输入数据不符合 NBT 二进制格式规范（如负的 ListTag 长度、缺失元素类型等）。
public sealed class NbtFormatException(string message) : NbtException(message)
{
}

