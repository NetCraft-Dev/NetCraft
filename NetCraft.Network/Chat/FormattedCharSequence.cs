namespace NetCraft.Network.Chat;

//FormattedCharSink 富文本字符接收器对标原版 net.minecraft.util.FormattedCharSink
//原版 @FunctionalInterface 单方法接口 C# 用 delegate 支持 lambda 传参
//StringDecomposer 遍历时按 codepoint 回调返回 false 停止遍历
//position 是源字符串索引 style 是当前字符样式 codepoint 是 Unicode 码点
public delegate bool FormattedCharSink(int position, Style style, int codepoint);

//FormattedCharSequence 富文本序列对标原版 net.minecraft.util.FormattedCharSequence
//原版 @FunctionalInterface accept(FormattedCharSink) C# 用 delegate
//持有一个 accept 委托遍历时回调 sink 逐字符消费返回 false 停止
//工厂方法在 FormattedCharSequences 静态类对标原版 FormattedCharSequence 静态方法
public delegate bool FormattedCharSequence(FormattedCharSink output);

//FormattedCharSequences 富文本序列工厂对标原版 FormattedCharSequence 静态方法
//Codepoint 单字符 Forward 正向遍历 Backward 反向遍历 Composite 组合多段
//C# delegate 不能有静态方法故工厂放独立静态类调用方用 FormattedCharSequences.Forward(...)
public static class FormattedCharSequences
{
    //Empty 空序列 accept 立即返回 true 对标原版 FormattedCharSequence.EMPTY
    public static readonly FormattedCharSequence Empty = _ => true;

    //Codepoint 单字符序列对标原版 FormattedCharSequence.codepoint
    public static FormattedCharSequence Codepoint(int codepoint, Style style)
        => output => output(0, style, codepoint);

    //Forward 正向遍历纯文本对标原版 FormattedCharSequence.forward
    //委托 StringDecomposer.Iterate 做 UTF-16 代理对解码不解析 § 颜色码
    public static FormattedCharSequence Forward(string text, Style style)
        => string.IsNullOrEmpty(text) ? Empty : output => StringDecomposer.Iterate(text, style, output);

    //Backward 反向遍历纯文本对标原版 FormattedCharSequence.backward
    public static FormattedCharSequence Backward(string text, Style style)
        => string.IsNullOrEmpty(text) ? Empty : output => StringDecomposer.IterateBackwards(text, style, output);

    //Composite 两段组合对标原版 FormattedCharSequence.fromPair
    //第一段遍历完继续第二段任一段返回 false 停止
    public static FormattedCharSequence Composite(FormattedCharSequence first, FormattedCharSequence second)
        => output => first(output) && second(output);

    //Composite 多段组合对标原版 FormattedCharSequence.fromList
    public static FormattedCharSequence Composite(IReadOnlyList<FormattedCharSequence> parts)
        => parts.Count switch
        {
            0 => Empty,
            1 => parts[0],
            _ => output =>
            {
                foreach (var part in parts)
                    if (!part(output)) return false;
                return true;
            }
        };

    //Composite 可变参数组合对标原版 FormattedCharSequence.composite(parts...)
    public static FormattedCharSequence Composite(params FormattedCharSequence[] parts)
        => Composite((IReadOnlyList<FormattedCharSequence>)parts);
}
