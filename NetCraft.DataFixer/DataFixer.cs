namespace NetCraft.DataFixer;

using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

//DataFixer数据修复器接口对应原版com.mojang.datafixers.DataFixer
//按type与版本范围对Dynamic执行更新
public interface DataFixer
{
    //update按type与版本范围对input执行更新
    Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion);

    //getSchema按key取得Schema
    Schema GetSchema(int key);
}
