using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//IntegerModifier 整数修饰符对应原版 IntegerModifier
public static class IntegerModifier
{
    public static readonly AttributeModifier<int, int> Add = new SimpleIntegerModifier((subject, argument) => subject + argument);

    public static readonly AttributeModifier<int, int> Subtract = new SimpleIntegerModifier((subject, argument) => subject - argument);

    public static readonly AttributeModifier<int, int> Multiply = new SimpleIntegerModifier((subject, argument) => subject * argument);

    public static readonly AttributeModifier<int, int> Minimum = new SimpleIntegerModifier(Math.Min);

    public static readonly AttributeModifier<int, int> Maximum = new SimpleIntegerModifier(Math.Max);

    //SimpleIntegerModifier 参数即整数的修饰符
    private sealed class SimpleIntegerModifier : AttributeModifier<int, int>
    {
        private readonly Func<int, int, int> _function;

        public SimpleIntegerModifier(Func<int, int, int> function) { _function = function; }

        public int Apply(int subject, int argument) => _function(subject, argument);

        public Codec<int> ArgumentCodec(EnvironmentAttribute<int> attribute) => Codecs.Int;
    }
}
