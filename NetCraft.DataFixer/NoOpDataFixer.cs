namespace NetCraft.DataFixer;

using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

//no-op DataFixer implementation for tests and placeholders
//Update returns input unchanged; GetSchema throws NotSupported because no Schema is registered
//after the stage-D path is connected and replaced by the real DataFixer, this type is kept only for tests
public sealed class NoOpDataFixer : DataFixer
{
    //Update returns input unchanged; matches vanilla behavior when versions are equal
    //different versions would normally go through rule upgrades, but this placeholder returns the original value directly
    public Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion)
        => input;

    //GetSchema throws NotSupportedException because no Schema is registered
    public Schema GetSchema(int key) => throw new NotSupportedException("NoOpDataFixer has no registered Schema");
}
