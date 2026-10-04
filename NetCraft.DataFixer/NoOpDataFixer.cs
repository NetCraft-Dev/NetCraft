namespace NetCraft.DataFixer;

using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

//无操作DataFixer实现供测试与占位使用
//Update直接返回原input不修改数据GetSchema抛NotSupported因未注册任何Schema
//阶段D路径接通用真实DataFixer替换后此类型仅在测试中保留
public sealed class NoOpDataFixer : DataFixer
{
    //Update不修改input直接返回对应版本相同时原版行为
    //版本不同时本应走规则升级但此实现为占位直接返回原值
    public Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion)
        => input;

    //GetSchema未注册Schema抛NotSupportedException
    public Schema GetSchema(int key) => throw new NotSupportedException("NoOpDataFixer未注册任何Schema");
}
