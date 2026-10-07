namespace NetCraft.DataFixer.Types.Families;

using System;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;

//TypeFamily type family maps to vanilla com.mojang.datafixers.types.families.TypeFamily
//returns a different child type per index, used for recursive types
public interface TypeFamily
{
    //apply returns the child type at the given index
    T.Type<object> Apply(int index);

    //familyOptic factory builds a FamilyOptic from an IntFunction
    static FamilyOptic<A, B> FamilyOptic<A, B>(Func<int, TypedOptic<object, object, A, B>> optics)
        => new(optics);
}
