namespace NetCraft.DataFixer;

//FamilyOptic类型家族的optic集合对应原版com.mojang.datafixers.FamilyOptic
//按index返回对应TypedOptic
public sealed class FamilyOptic<A, B>
{
    private readonly Func<int, TypedOptic<object, object, A, B>> _optics;

    public FamilyOptic(Func<int, TypedOptic<object, object, A, B>> optics) => _optics = optics;

    //apply按索引取得对应TypedOptic
    public TypedOptic<object, object, A, B> Apply(int index) => _optics(index);
}
