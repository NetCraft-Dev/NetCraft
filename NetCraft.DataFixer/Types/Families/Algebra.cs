namespace NetCraft.DataFixer.Types.Families;

using NetCraft.DataFixer;

//Algebra maps to vanilla com.mojang.datafixers.types.families.Algebra
//provides a RewriteResult per index, describing the rewrite of each index in a recursive type family
public interface Algebra
{
    //apply returns the rewrite result at the given index
    RewriteResult<object, object> Apply(int index);

    //toString with an indent level
    string ToString(int level);
}
