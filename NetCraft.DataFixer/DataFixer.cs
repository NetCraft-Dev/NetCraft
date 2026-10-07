namespace NetCraft.DataFixer;

using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

//DataFixer data fixer interface maps to vanilla com.mojang.datafixers.DataFixer
//applies updates to a Dynamic by type and version range
public interface DataFixer
{
    //update applies updates to input by type and version range
    Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion);

    //getSchema takes the Schema by key
    Schema GetSchema(int key);
}
