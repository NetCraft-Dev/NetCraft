using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//BooleanModifier boolean modifier, maps to vanilla BooleanModifier
public sealed class BooleanModifier : AttributeModifier<bool, bool>
{
    public static readonly BooleanModifier And = new((subject, argument) => argument && subject);

    public static readonly BooleanModifier Nand = new((subject, argument) => !(argument && subject));

    public static readonly BooleanModifier Or = new((subject, argument) => argument || subject);

    public static readonly BooleanModifier Nor = new((subject, argument) => !(argument || subject));

    public static readonly BooleanModifier Xor = new((subject, argument) => argument ^ subject);

    public static readonly BooleanModifier Xnor = new((subject, argument) => argument == subject);

    private readonly Func<bool, bool, bool> _function;

    private BooleanModifier(Func<bool, bool, bool> function) { _function = function; }

    public bool Apply(bool subject, bool argument) => _function(subject, argument);

    public Codec<bool> ArgumentCodec(EnvironmentAttribute<bool> attribute) => Codecs.Bool;
}
