namespace NetCraft.DataFixer;

//FamilyOptic optic collection of a type family, maps to vanilla com.mojang.datafixers.FamilyOptic
//returns the corresponding TypedOptic by index
public sealed class FamilyOptic<A, B>
{
    private readonly Func<int, TypedOptic<object, object, A, B>> _optics;

    public FamilyOptic(Func<int, TypedOptic<object, object, A, B>> optics) => _optics = optics;

    //apply takes the corresponding TypedOptic by index
    public TypedOptic<object, object, A, B> Apply(int index) => _optics(index);
}
