namespace NetCraft.DataFixer.Types.Families;

using System;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;

//TypeFamily类型家族对应原版com.mojang.datafixers.types.families.TypeFamily
//按index返回不同子类型用于递归类型
public interface TypeFamily
{
    //apply按索引返回子类型
    T.Type<object> Apply(int index);

    //familyOptic工厂用IntFunction构造FamilyOptic
    static FamilyOptic<A, B> FamilyOptic<A, B>(Func<int, TypedOptic<object, object, A, B>> optics)
        => new(optics);
}
