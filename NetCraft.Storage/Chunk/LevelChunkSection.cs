using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Paletted;

namespace NetCraft.Storage.Chunk;

//区块区段对应原版net.minecraft.world.level.chunk.LevelChunkSection
//持有states方块状态palette与biomes生物群系palette
//四个计数在同一遍扫描里算出来 对应原版 recalcBlockCounts 与 Paper 的 countEntries
public sealed class LevelChunkSection
{
    public const int BiomeContainerBits = 2;

    private short _nonEmptyBlockCount;
    private short _fluidCount;
    private short _tickingBlockCount;
    //流体随机刻计数 流体刻子系统接入前恒为 0 对应原版同名字段
    private short _tickingFluidCount;

    public PalettedContainer<BlockState> States { get; }
    public PalettedContainer<Holder<Biome>> Biomes { get; private set; }

    public LevelChunkSection(PalettedContainer<BlockState> states, PalettedContainer<Holder<Biome>> biomes)
    {
        States = states;
        Biomes = biomes;
        RecountStates();
    }

    public LevelChunkSection(Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        States = statesFactory();
        Biomes = biomesFactory();
    }

    //RecountStates 按容器实际内容重算区段计数
    //从存档还原或复制区段时计数不随容器带过来 不重算会让 HasOnlyAir 把有方块的区段误判成全空气
    //误判后果是落盘时该区段被写成 null 存档方块数据丢失 光照也会按空区段处理
    //空气按 Block.IsAir 判 不能拿某个坐标上的状态当基准 读档时那个坐标上可能是石头
    private void RecountStates()
    {
        _nonEmptyBlockCount = 0;
        _fluidCount = 0;
        _tickingBlockCount = 0;
        _tickingFluidCount = 0;
        States.Count((state, count) =>
        {
            if (!state.Owner.IsAir) _nonEmptyBlockCount += (short)count;
            if (!state.FluidState.IsEmpty) _fluidCount += (short)count;
            if (state.Owner.RandomTicks) _tickingBlockCount += (short)count;
        });
    }

    public BlockState GetBlockState(int sectionX, int sectionY, int sectionZ)
        => States.Get(sectionX, sectionY, sectionZ);

    //SetBlockState 写入方块并维护区段计数对应原版 setBlockState
    //逐个计数只在旧新状态在对应判定上不同才动 写法与重算那遍等价
    public BlockState SetBlockState(int sectionX, int sectionY, int sectionZ, BlockState state)
    {
        var old = States.GetAndSet(sectionX, sectionY, sectionZ, state);
        //三个判定各取一次 省得同一处属性访问在条件与增量里各求一遍
        var oldAir = old.Owner.IsAir;
        var newAir = state.Owner.IsAir;
        if (oldAir != newAir)
            _nonEmptyBlockCount = (short)Math.Max(0, _nonEmptyBlockCount + (newAir ? -1 : 1));

        var oldEmpty = old.FluidState.IsEmpty;
        var newEmpty = state.FluidState.IsEmpty;
        if (oldEmpty != newEmpty)
            _fluidCount = (short)Math.Max(0, _fluidCount + (newEmpty ? -1 : 1));

        var oldTicks = old.Owner.RandomTicks;
        var newTicks = state.Owner.RandomTicks;
        if (oldTicks != newTicks)
            _tickingBlockCount = (short)Math.Max(0, _tickingBlockCount + (newTicks ? 1 : -1));

        return old;
    }

    //NonEmptyBlockCount 非空方块数供网络序列化写入
    //原版客户端 LevelChunk.getBlockState 先看区段 hasOnlyAir 判空 写 0 会让整段被当空气
    //结果是客户端认为世界全是空气 玩家下坠且区段不渲染
    public short NonEmptyBlockCount => _nonEmptyBlockCount;

    //FluidCount 流体数供网络序列化写入
    public short FluidCount => _fluidCount;

    public bool HasOnlyAir() => _nonEmptyBlockCount == 0;

    public bool HasFluid() => _fluidCount > 0;

    public bool IsRandomlyTicking() => _tickingBlockCount > 0 || _tickingFluidCount > 0;

    public PalettedContainer<Holder<Biome>> GetBiomes() => Biomes;

    public LevelChunkSection Copy()
        => new(States.Copy(), (PalettedContainer<Holder<Biome>>)Biomes.Copy());

    public bool MaybeHas(Predicate<BlockState> predicate) => States.MaybeHas(predicate);

    public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        => Biomes.Get(quartX, quartY, quartZ);

    //SetBiome 按区段内 quart 坐标写入生物群系对应原版 setBiome
    //quart 坐标范围 0-3 每 4 个方块共享一个 biome
    public void SetBiome(int quartX, int quartY, int quartZ, Holder<Biome> biome)
        => Biomes.Set(quartX, quartY, quartZ, biome);
}
