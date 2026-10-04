namespace NetCraft.DataFixer;

using System;
using System.Collections;
using System.Collections.Generic;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//DataFixUtils工具类对应原版com.mojang.datafixers.DataFixUtils
//提供Optional组合与版本key编码
public static class DataFixUtils
{
    //smallestEncompassingPowerOfTwo返回最小包围的2幂
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

    //ceillog2向上取log2
    public static int Ceillog2(int input)
    {
        input = IsPowerOfTwo(input) ? input : SmallestEncompassingPowerOfTwo(input);
        return MULTIPLY_DE_BRUIJN_BIT_POSITION[(int)((uint)input * 0x077CB531 >> 27) & 0x1F];
    }

    //make执行工厂
    public static T Make<T>(Func<T> factory) => factory();

    //make对值执行consumer后返回
    public static T Make<T>(T t, Action<T> consumer)
    {
        consumer(t);
        return t;
    }

    //orElse取Optional值或默认
    public static U OrElse<U>(Optional<U> optional, U other)
        => optional.IsPresent ? optional.Get() : other;

    //orElseGet取Optional值或惰性默认
    public static U OrElseGet<U>(Optional<U> optional, Func<U> other)
        => optional.IsPresent ? optional.Get() : other();

    //or返回首个有值的Optional
    public static Optional<U> Or<U>(Optional<U> optional, Func<Optional<U>> other)
        => optional.IsPresent ? optional : other();

    //makeKey按版本与子版本合成key
    public static int MakeKey(int version) => MakeKey(version, 0);

    public static int MakeKey(int version, int subVersion) => version * 10 + subVersion;

    //getVersion从key取版本
    public static int GetVersion(int key) => key / 10;

    //getSubVersion从key取子版本
    public static int GetSubVersion(int key) => key % 10;

    //consumerToFunction把Action转为Func
    public static Func<T, T> ConsumerToFunction<T>(Action<T> consumer)
        => s => { consumer(s); return s; };

    //writeAndReadTypedOrThrow对应原版net.minecraft.util.Util.writeAndReadTypedOrThrow
    //把typed用源ops写后用fn修复再用目标ops读失败用partial值
    public static Typed<object> WriteAndReadTypedOrThrow<TOld, TNew>(
        Typed<TOld> typed, T.Type<TNew> newType, Func<Dynamic<object>, Dynamic<object>> fn)
    {
        var written = typed.Write().GetOrThrow(err => new InvalidOperationException("Failed to write typed: " + err));
        var fixedDynamic = fn(written);
        return ReadTypedOrThrow((T.Type<object>)(object)newType!, fixedDynamic, true);
    }

    //readTypedOrThrow对应原版net.minecraft.util.Util.readTypedOrThrow两参数版默认不接收partial
    public static Typed<object> ReadTypedOrThrow<TA>(T.Type<TA> type, Dynamic<object> dynamic)
        => ReadTypedOrThrow(type, dynamic, false);

    //readTypedOrThrow对应原版net.minecraft.util.Util.readTypedOrThrow三参数版
    //acceptPartial为true时优先用partial值
    public static Typed<object> ReadTypedOrThrow<TA>(T.Type<TA> type, Dynamic<object> dynamic, bool acceptPartial)
    {
        var result = type.Read(dynamic);
        var pair = acceptPartial ? result.GetPartialOrThrow() : result.GetOrThrow();
        return new Typed<object>((T.Type<object>)(object)type!, dynamic.Ops, (object)pair.First!);
    }
}
