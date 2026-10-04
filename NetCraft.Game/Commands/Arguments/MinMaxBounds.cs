using System.Globalization;
using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//MinMaxBounds 数值区间对应原版 net.minecraft.advancements.predicates.MinMaxBounds
//[min..max]语法解析 单值视作min=max 全空非法 选择器distance/level/rotation选项共用
public static class MinMaxBounds
{
    public static readonly SimpleCommandExceptionType ErrorEmpty =
        new(new TranslatableMessage("argument.range.empty"));
    public static readonly SimpleCommandExceptionType ErrorSwapped =
        new(new TranslatableMessage("argument.range.swapped"));

    //'.'后跟'.'表示区间分隔符停止读数 单独的'.'是数字小数点
    private static bool IsAllowedInputChar(StringReader reader)
    {
        var c = reader.Peek();
        if (c is >= '0' and <= '9' or '-') return true;
        if (c == '.') return !(reader.CanRead(2) && reader.Peek(1) == '.');
        return false;
    }

    //ReadNumber 读一段数字文本转换失败抛对应解析异常 空串返回null
    private static T? ReadNumber<T>(StringReader reader, Func<string, T> converter,
        DynamicCommandExceptionType parseError) where T : struct
    {
        var start = reader.Cursor;
        while (reader.CanRead() && IsAllowedInputChar(reader))
            reader.Skip();
        var number = reader.String[start..reader.Cursor];
        if (number.Length == 0) return null;
        try
        {
            return converter(number);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            throw parseError.CreateWithContext(reader, number);
        }
    }

    //ReadBounds 读区间读出min/max无'..'分隔时max=min 内部异常回滚游标后按start位置重抛
    private static (T? Min, T? Max) ReadBounds<T>(StringReader reader, Func<string, T> converter,
        DynamicCommandExceptionType parseError) where T : struct
    {
        if (!reader.CanRead())
            throw ErrorEmpty.CreateWithContext(reader);
        var start = reader.Cursor;
        try
        {
            var min = ReadNumber(reader, converter, parseError);
            T? max;
            if (reader.CanRead(2) && reader.Peek() == '.' && reader.Peek(1) == '.')
            {
                reader.Skip();
                reader.Skip();
                max = ReadNumber(reader, converter, parseError);
            }
            else
            {
                max = min;
            }
            if (min is null && max is null)
                throw ErrorEmpty.CreateWithContext(reader);
            return (min, max);
        }
        catch (CommandSyntaxException e)
        {
            reader.SetCursor(start);
            throw new CommandSyntaxException(e.Type, e.RawMessage, e.Input, start);
        }
    }

    //Doubles 双精度区间 平方值预计算供距离平方匹配
    public sealed class Doubles
    {
        public static readonly Doubles Any = new(null, null);

        public Doubles(double? min, double? max)
        {
            Min = min;
            Max = max;
            MinSqr = min * min;
            MaxSqr = max * max;
        }

        public double? Min { get; }
        public double? Max { get; }
        private double? MinSqr { get; }
        private double? MaxSqr { get; }

        public bool IsAny => Min is null && Max is null;

        public bool Matches(double value)
            => (Min is null || Min <= value) && (Max is null || Max >= value);

        public bool MatchesSqr(double valueSqr)
            => (MinSqr is null || MinSqr <= valueSqr) && (MaxSqr is null || MaxSqr >= valueSqr);

        public static Doubles FromReader(StringReader reader)
        {
            var start = reader.Cursor;
            var (min, max) = ReadBounds(reader,
                s => double.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidDouble());
            if (min > max)
            {
                reader.SetCursor(start);
                throw ErrorSwapped.CreateWithContext(reader);
            }
            return new Doubles(min, max);
        }
    }

    //Ints 整数区间 平方值用long承载防止溢出
    public sealed class Ints
    {
        public static readonly Ints Any = new(null, null);

        public Ints(int? min, int? max)
        {
            Min = min;
            Max = max;
            MinSqr = min is null ? null : (long)min * min;
            MaxSqr = max is null ? null : (long)max * max;
        }

        public int? Min { get; }
        public int? Max { get; }
        private long? MinSqr { get; }
        private long? MaxSqr { get; }

        public bool IsAny => Min is null && Max is null;

        public bool Matches(int value)
            => (Min is null || Min <= value) && (Max is null || Max >= value);

        public bool MatchesSqr(long valueSqr)
            => (MinSqr is null || MinSqr <= valueSqr) && (MaxSqr is null || MaxSqr >= valueSqr);

        public static Ints FromReader(StringReader reader)
        {
            var start = reader.Cursor;
            var (min, max) = ReadBounds(reader,
                s => int.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidInt());
            if (min > max)
            {
                reader.SetCursor(start);
                throw ErrorSwapped.CreateWithContext(reader);
            }
            return new Ints(min, max);
        }
    }

    //FloatDegrees 角度区间 度数环绕无swapped校验
    public sealed class FloatDegrees
    {
        public static readonly FloatDegrees Any = new(null, null);

        public FloatDegrees(float? min, float? max)
        {
            Min = min;
            Max = max;
        }

        public float? Min { get; }
        public float? Max { get; }

        public bool IsAny => Min is null && Max is null;

        public static FloatDegrees FromReader(StringReader reader)
        {
            var (min, max) = ReadBounds(reader,
                s => float.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidFloat());
            return new FloatDegrees(min, max);
        }
    }
}
