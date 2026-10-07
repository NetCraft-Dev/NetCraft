namespace NetCraft.DataFixer;

using System;
using System.Collections;
using System.Collections.Generic;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//DataFixUtils utility class maps to vanilla com.mojang.datafixers.DataFixUtils
//provides Optional composition and version key encoding
public static class DataFixUtils
{
    //smallestEncompassingPowerOfTwo returns the smallest enclosing power of two
    public static int SmallestEncompassingPowerOfTwo(int input)
    {
        int result = input - 1;
        result |= result >> 1;
        result |= result >> 2;
        result |= result >> 4;
        result |= result >> 8;
        result |= result >> 16;
        return result + 1;
    }

    private static bool IsPowerOfTwo(int input) => input != 0 && (input & (input - 1)) == 0;

    private static readonly int[] MULTIPLY_DE_BRUIJN_BIT_POSITION = {
        0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
        31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
    };

    //ceillog2 rounds up log2
    public static int Ceillog2(int input)
    {
        input = IsPowerOfTwo(input) ? input : SmallestEncompassingPowerOfTwo(input);
        return MULTIPLY_DE_BRUIJN_BIT_POSITION[(int)((uint)input * 0x077CB531 >> 27) & 0x1F];
    }

    //make invokes the factory
    public static T Make<T>(Func<T> factory) => factory();

    //make applies a consumer to the value then returns it
    public static T Make<T>(T t, Action<T> consumer)
    {
        consumer(t);
        return t;
    }

    //orElse takes the Optional value or the default
    public static U OrElse<U>(Optional<U> optional, U other)
        => optional.IsPresent ? optional.Get() : other;

    //orElseGet takes the Optional value or a lazy default
    public static U OrElseGet<U>(Optional<U> optional, Func<U> other)
        => optional.IsPresent ? optional.Get() : other();

    //or returns the first Optional with a value
    public static Optional<U> Or<U>(Optional<U> optional, Func<Optional<U>> other)
        => optional.IsPresent ? optional : other();

    //makeKey composes a key from version and subversion
    public static int MakeKey(int version) => MakeKey(version, 0);

    public static int MakeKey(int version, int subVersion) => version * 10 + subVersion;

    //getVersion takes the version from the key
    public static int GetVersion(int key) => key / 10;

    //getSubVersion takes the subversion from the key
    public static int GetSubVersion(int key) => key % 10;

    //consumerToFunction converts an Action to a Func
    public static Func<T, T> ConsumerToFunction<T>(Action<T> consumer)
        => s => { consumer(s); return s; };

    //writeAndReadTypedOrThrow maps to vanilla net.minecraft.util.Util.writeAndReadTypedOrThrow
    //writes typed with the source ops, fixes with fn, then reads with the target ops; uses the partial value on failure
    public static Typed<object> WriteAndReadTypedOrThrow<TOld, TNew>(
        Typed<TOld> typed, T.Type<TNew> newType, Func<Dynamic<object>, Dynamic<object>> fn)
    {
        var written = typed.Write().GetOrThrow(err => new InvalidOperationException("Failed to write typed: " + err));
        var fixedDynamic = fn(written);
        return ReadTypedOrThrow((T.Type<object>)(object)newType!, fixedDynamic, true);
    }

    //readTypedOrThrow maps to the two-parameter vanilla net.minecraft.util.Util.readTypedOrThrow, which takes no partial by default
    public static Typed<object> ReadTypedOrThrow<TA>(T.Type<TA> type, Dynamic<object> dynamic)
        => ReadTypedOrThrow(type, dynamic, false);

    //readTypedOrThrow maps to the three-parameter vanilla net.minecraft.util.Util.readTypedOrThrow
    //when acceptPartial is true, the partial value is preferred
    public static Typed<object> ReadTypedOrThrow<TA>(T.Type<TA> type, Dynamic<object> dynamic, bool acceptPartial)
    {
        var result = type.Read(dynamic);
        var pair = acceptPartial ? result.GetPartialOrThrow() : result.GetOrThrow();
        return new Typed<object>((T.Type<object>)(object)type!, dynamic.Ops, (object)pair.First!);
    }
}
