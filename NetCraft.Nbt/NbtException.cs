namespace NetCraft.Nbt;

//Generic NBT runtime exception. Mirrors vanilla net.minecraft.nbt.NbtException (extends java.lang.RuntimeException).
//Mapped to System.Exception in C# (the runtime exception semantics).
public class NbtException(string message) : Exception(message)
{
}

//NBT format exception. Mirrors vanilla net.minecraft.nbt.NbtFormatException.
//Signals input that violates the NBT binary format (a negative ListTag length, a missing element type, and so on).
public sealed class NbtFormatException(string message) : NbtException(message)
{
}

