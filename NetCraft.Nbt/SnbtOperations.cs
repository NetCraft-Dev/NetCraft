using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//Built-in SNBT operations, mirroring vanilla net.minecraft.nbt.SnbtOperations
//Defines the bool/uuid built-ins used by SnbtGrammar
public static class SnbtOperations
{
    public const string BuiltinTrue = "true";
    public const string BuiltinFalse = "false";

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedStringUuid =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected string UUID"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedNumberOrBoolean =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected number or boolean"));

    public static IReadOnlyDictionary<BuiltinKey, BuiltinOperation> BuiltinOperations { get; } = new Dictionary<BuiltinKey, BuiltinOperation>
    {
        [new BuiltinKey("bool", 1)] = new BoolOperation(),
        [new BuiltinKey("uuid", 1)] = new UuidOperation(),
    };

    public static SuggestionSupplier<CommandStringReader> BuiltinIds { get; } = _ =>
        new[] { BuiltinFalse, BuiltinTrue }.Concat(BuiltinOperations.Keys.Select(k => k.Id));
}

//Built-in operation interface, mirroring vanilla SnbtOperations.BuiltinOperation
public interface BuiltinOperation
{
    T? Run<T>(DynamicOps<T> ops, List<T> arguments, ParseState<CommandStringReader> state);
}

//BuiltinKey operation identifier, mirroring vanilla SnbtOperations.BuiltinKey
public sealed class BuiltinKey
{
    public string Id { get; }
    public int ArgCount { get; }

    public BuiltinKey(string id, int argCount)
    {
        Id = id;
        ArgCount = argCount;
    }

    public override int GetHashCode() => HashCode.Combine(Id, ArgCount);

    public override bool Equals(object? obj) => obj is BuiltinKey other && Id == other.Id && ArgCount == other.ArgCount;

    public override string ToString() => $"{Id}/{ArgCount}";
}

internal sealed class BoolOperation : BuiltinOperation
{
    public T? Run<T>(DynamicOps<T> ops, List<T> arguments, ParseState<CommandStringReader> state)
    {
        var arg = arguments[0];
        var asBool = ops.GetBooleanValue(arg).Result();
        if (asBool.IsPresent)
        {
            return ops.CreateBoolean(asBool.Get());
        }
        var asNumber = ops.GetNumberValue(arg).Result();
        if (asNumber.IsPresent)
        {
            return ops.CreateBoolean(asNumber.Get() != 0.0);
        }
        state.ErrorCollector.Store(state.Mark(), SnbtOperations.ErrorExpectedNumberOrBoolean);
        return default;
    }
}

internal sealed class UuidOperation : BuiltinOperation
{
    public T? Run<T>(DynamicOps<T> ops, List<T> arguments, ParseState<CommandStringReader> state)
    {
        var asString = ops.GetStringValue(arguments[0]).Result();
        if (!asString.IsPresent)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtOperations.ErrorExpectedStringUuid);
            return default;
        }
        try
        {
            var guid = Guid.Parse(asString.Get());
            var ints = GuidToIntArray(guid);
            return ops.CreateIntList(ints);
        }
        catch (FormatException)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtOperations.ErrorExpectedStringUuid);
            return default;
        }
    }

    //Guid to an array of 4 ints, mirroring vanilla UUIDUtil.uuidToIntArray
    private static IEnumerable<int> GuidToIntArray(Guid guid)
    {
        var bytes = guid.ToByteArray();
        for (var i = 0; i < 16; i += 4)
        {
            if (i + 4 > bytes.Length) yield break;
            yield return BitConverter.ToInt32(bytes, i);
        }
    }
}
