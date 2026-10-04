namespace NetCraft.DataFixer.Types.Families;

using NetCraft.DataFixer;

//Algebra代数对应原版com.mojang.datafixers.types.families.Algebra
//按index提供RewriteResult描述递归类型家族每个index的重写
public interface Algebra
{
    //apply按索引返回该位置的重写结果
    RewriteResult<object, object> Apply(int index);

    //toString带缩进级别
    string ToString(int level);
}
