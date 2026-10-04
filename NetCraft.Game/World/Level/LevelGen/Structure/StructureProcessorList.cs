using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureProcessorList 处理器列表 对应原版 StructureProcessorList
//一个处理器列表就是一条处理链 模板池按 processor_list 注册名引用它
public sealed class StructureProcessorList : NetCraft.Registry.StructureProcessorList
{
    //ElementCodec 注册表元素 codec 对应原版 LIST_OBJECT_CODEC 与 DIRECT_CODEC
    public static readonly Codec<NetCraft.Registry.StructureProcessorList> ElementCodec = new StructureProcessorListCodec();

    public IReadOnlyList<StructureProcessor> Processors { get; }

    public StructureProcessorList(IReadOnlyList<StructureProcessor> processors) => Processors = processors;

    //List 取处理器列表 对应原版 list
    public IReadOnlyList<StructureProcessor> List() => Processors;

    public override string ToString()
        => $"ProcessorList[{string.Join(", ", Processors.Select(p => p.GetType().Name))}]";
}

//StructureProcessorListCodec 处理器列表编解码 对应原版 processors 包装与裸数组两种形态
internal sealed class StructureProcessorListCodec : ScalarCodec<NetCraft.Registry.StructureProcessorList>
{
    public override DataResult<NetCraft.Registry.StructureProcessorList> Parse<U>(DynamicOps<U> ops, U input)
    {
        //形态一 {"processors":[...]} 形态二直接给处理器数组
        var mapResult = ops.GetMap(input);
        if (mapResult.Result().IsPresent)
        {
            var processorsTag = mapResult.GetOrThrow().Get("processors");
            if (!processorsTag.IsPresent)
                return DataResult<NetCraft.Registry.StructureProcessorList>.Error(() => "processor_list 缺少 processors");
            return ParseList(ops, processorsTag.Get());
        }
        return ParseList(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.StructureProcessorList value)
        => DataResult<U>.Error(() => "处理器列表编码暂未实现");

    //ParseList 逐项按 processor_type 分发解析
    private static DataResult<NetCraft.Registry.StructureProcessorList> ParseList<U>(DynamicOps<U> ops, U input)
    {
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<NetCraft.Registry.StructureProcessorList>.Error(() => "processors 必须是数组");
        var processors = new List<StructureProcessor>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = StructureProcessorType.SingleCodec.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<NetCraft.Registry.StructureProcessorList>.Error(() => "处理器列表里有一项解析失败");
            processors.Add(parsed.GetOrThrow());
        }
        return DataResult<NetCraft.Registry.StructureProcessorList>.Success(new StructureProcessorList(processors));
    }
}
