using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//WorldCoordinate 单轴坐标段对应原版 WorldCoordinate
//~ 前缀相对当前值 空白表示偏移 0 绝对值可加 0.5 居中修正
public sealed record WorldCoordinate(bool Relative, double Value)
{
    //Get 以基准值求绝对值 相对段做加法
    public double Get(double original) => Relative ? Value + original : Value;

    public bool IsRelative => Relative;

    //ParseDouble 解析单个坐标段 遇 ^ 抛混合类型错 center 为 true 且无小数点的绝对值加 0.5 对齐方块中心
    public static WorldCoordinate ParseDouble(StringReader reader, bool center)
    {
        if (reader.CanRead() && reader.Peek() == '^')
            throw Vec3Argument.ErrorMixedType.CreateWithContext(reader);
        if (!reader.CanRead())
            throw ErrorExpectedDouble.CreateWithContext(reader);
        var relative = IsRelativePrefix(reader);
        var start = reader.Cursor;
        var value = !reader.CanRead() || reader.Peek() == ' ' ? 0.0 : reader.ReadDouble();
        var number = reader.String[start..reader.Cursor];
        if (relative && number.Length == 0) return new WorldCoordinate(true, 0.0);
        if (!number.Contains('.') && !relative && center) value += 0.5;
        return new WorldCoordinate(relative, value);
    }

    //ParseInt 解析整数坐标段 相对段允许小数 绝对段只接受整数
    public static WorldCoordinate ParseInt(StringReader reader)
    {
        if (reader.CanRead() && reader.Peek() == '^')
            throw Vec3Argument.ErrorMixedType.CreateWithContext(reader);
        if (!reader.CanRead())
            throw ErrorExpectedInt.CreateWithContext(reader);
        var relative = IsRelativePrefix(reader);
        double value;
        if (reader.CanRead() && reader.Peek() != ' ')
            value = relative ? reader.ReadDouble() : reader.ReadInt();
        else
            value = 0.0;
        return new WorldCoordinate(relative, value);
    }

    //IsRelativePrefix 消费可选的 ~ 前缀返回是否相对
    private static bool IsRelativePrefix(StringReader reader)
    {
        if (reader.Peek() != '~') return false;
        reader.Skip();
        return true;
    }

    public static readonly SimpleCommandExceptionType ErrorExpectedDouble =
        new(new TranslatableMessage("argument.pos.missing.double"));

    public static readonly SimpleCommandExceptionType ErrorExpectedInt =
        new(new TranslatableMessage("argument.pos.missing.int"));
}
