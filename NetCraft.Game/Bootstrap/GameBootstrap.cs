using NetCraft.Game.Commands.Synchronization;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Carver;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Game.World.Clock;
using NetCraft.Game.World.Timeline;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Storage.Paletted;
using WorldCarverBootstrap = NetCraft.Game.World.Level.LevelGen.Carver.WorldCarver;

namespace NetCraft.Game.Bootstrap;

//GameBootstrap Game 层引导入口对应原版 net.minecraft.server.Bootstrap 的 Game 层扩展
//在 BootstrapClass.BootStrap 之前调用触发 Game 层内置注册表填充方块/物品/实体等
//确保 NoiseBasedChunkGenerator.FillFromNoise 等业务流程可访问已注册的 BlockState
public static class GameBootstrap
{
    //原版 Bootstrap 只有启动线程会调 一个 volatile 标志就够了
    //这里区块包构造在连接线程调 区块生成在 ThreadPool 调 必须靠锁保证只跑一次
    //后来的线程要等引导真正跑完再返回 不能只看标志就返回 否则会读到半成品注册表
    private static readonly object _gate = new();
    private static bool _bootstrapped;
    private static bool _preDataBootstrapped;
    //_ready 引导完整完成的快路径标志 生成链路每区块要调三次 Bootstrap 靠它绕开锁
    //与上面两个分开: 那两个必须先置位防重入 提前置位不代表真的做完了
    private static volatile bool _ready;

    static GameBootstrap() => Log.SetClassSource(typeof(GameBootstrap));

    //Bootstrap Game 层引导入口触发方块/物品等内置注册表填充
    //重复调用幂等直接返回避免重复注册
    //幂等分支不打日志 区块包序列化每个包都会调一次 开了 debug 会刷爆日志文件
    public static void Bootstrap()
    {
        //引导完整做完后走这条无锁快路径 生成热路径上的调用全都停在这里
        if (_ready) return;
        BootstrapBeforeDataLoad();
        BootstrapAfterDataLoad();
    }

    //BootstrapBeforeDataLoad 数据驱动注册表加载前的引导
    //元素 JSON 的 codec 会引用方块/密度函数类型/群系 这些必须先就位才能解码
    //不带数据驱动加载的调用方直接用 Bootstrap 一次跑完
    public static void BootstrapBeforeDataLoad()
    {
        lock (_gate)
        {
            if (_preDataBootstrapped) return;
            //先置位再执行 注册过程里若有重入 Monitor 可重入不会死锁 置位后重入直接返回
            _preDataBootstrapped = true;
            //Log.Debug("BootstrapBeforeDataLoad 入口");
            Log.Info("Game layer bootstrap started");
            Blocks.Bootstrap();
            RegisterFluids();
            //票类型要先于注册表冻结登记 存档里的 chunk_tickets.dat 按注册名还原票
            NetCraft.Storage.TicketType.Bootstrap();
            WorldCarverBootstrap.RegisterAll();
            //特征与放置修饰器注册表必须先于数据加载填好 否则 configured_feature/placed_feature 整批解不出来
            NetCraft.Game.World.Level.LevelGen.Features.FeatureBootstrap.RegisterAll();
            NetCraft.Game.World.Level.LevelGen.Placement.PlacementModifierBootstrap.RegisterAll();
            //结构放置类型必须先于 worldgen/structure_set 装载就位 否则 placement 的 type 派发找不到目标
            NetCraft.Game.World.Level.LevelGen.Structure.StructureBootstrap.RegisterAll();
            //片段类型必须先于结构读档就位 否则片段落盘的 id 查不到还原实现
            NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceBootstrap.RegisterAll();
            //处理器类型与规则测试/位置判定/方块实体修改器必须先于 processor_list 装载就位
            NetCraft.Game.World.Level.LevelGen.Structure.StructureProcessorBootstrap.RegisterAll();
            //池元素类型必须先于 worldgen/template_pool 装载就位 否则 element_type 派发找不到目标
            NetCraft.Game.World.Level.LevelGen.Structure.StructurePoolBootstrap.RegisterAll();
            Items.Bootstrap();
            //发射行为表按物品实例登记 必须在物品填好之后
            NetCraft.Game.World.Level.Block.Dispenser.DispenseBehaviors.Bootstrap();
            GameRules.Bootstrap();
            //粒子类型按原版声明顺序登记 命令与粒子包都按注册表 id 编码
            NetCraft.Game.World.Particle.ParticleTypes.Bootstrap();
            //药水效果按原版声明顺序登记 命令与效果包都按注册表 id 编码
            NetCraft.Game.World.Effect.MobEffects.Bootstrap();
            ArgumentTypeInfos.Bootstrap();
            //实体属性先于实体类型登记 实体构造按属性建表 也要早于注册表冻结
            NetCraft.Registry.EntityAttribute.Attributes.Bootstrap();
            EntityTypes.Bootstrap();
            //实体类型的属性默认表要在任何实体构造之前装配好
            NetCraft.Game.World.Entity.DefaultAttributes.Bootstrap();
            BlockEntityTypes.Bootstrap();
            MenuTypes.Bootstrap();
            WorldClocks.Bootstrap();
            Timelines.Bootstrap();
            DensityFunctionBootstrap.RegisterAll();
            //Log.Debug("BootstrapBeforeDataLoad 出口");
        }
    }

    //BootstrapAfterDataLoad 数据驱动注册表加载后的补全
    //Noises.Bootstrap 与 RegisterBiomes 都对已被数据驱动填充的键跳过 保证 JSON 里的原版真值优先
    public static void BootstrapAfterDataLoad()
    {
        lock (_gate)
        {
            if (_bootstrapped) return;
            _bootstrapped = true;
            Noises.Bootstrap();
            RegisterBiomes();
            Log.Debug("Game layer bootstrap finished, blocks registered");
            //Log.Debug("BootstrapAfterDataLoad 出口");
            //置位放最后: 快路径读到 true 时必须保证引导真的做完了 提前置位会让别的线程读到半成品
            _ready = true;
        }
    }

    //StructureTemplates 世界装配注入的结构模板管理器
    //读档还原池元素片段时上下文要从这里取 没注入时读档的结构片段放不下东西
    public static StructureTemplateManager? StructureTemplates { get; private set; }

    //InjectStructureTemplates 数据加载完成后把结构模板管理器注入需要读模板的内容
    //jigsaw 结构装配与化石/模板特征都要按注册名读 data/<ns>/structure/<path>.nbt
    //没注入时这些内容按原版消耗完随机数后直接返回 false 表现为世界里一个 jigsaw 结构都没有
    public static void InjectStructureTemplates(ResourceManager resources)
    {
        var manager = new StructureTemplateManager(resources);
        StructureTemplates = manager;
        foreach (var holder in BuiltInRegistries.STRUCTURE.ListElements())
        {
            if (holder.Value is JigsawStructure jigsaw) jigsaw.TemplateManager = manager;
        }
        FossilFeature.TemplateManager = manager;
        TemplateFeature.TemplateManager = manager;
    }

    //RegisterFluids 登记五个内置流体
    //雕刻器的 matching_fluids 与湖特征的 fluid 字段都按注册名引用 缺一个整条配置就解不出来
    //项目还没有流体流动与流体状态体系 这里只登记注册名与"是否为空"所需的身份
    private static void RegisterFluids()
    {
        RegisterFluid("empty");
        RegisterFluid("flowing_water");
        RegisterFluid("water");
        RegisterFluid("flowing_lava");
        RegisterFluid("lava");
    }

    private static void RegisterFluid(string path)
    {
        var id = Identifier.WithDefaultNamespace(path);
        BuiltInRegistries.FLUID.Register(ResourceKey<Fluid>.Create(Registries.FLUID, id),
            new SimpleFluid(id), RegistrationInfo.BuiltIn);
    }

    //SimpleFluid 只带注册名的流体 供注册表身份识别
    private sealed class SimpleFluid : Fluid
    {
        private readonly Identifier _id;

        public SimpleFluid(Identifier id) => _id = id;

        public override Identifier Id => _id;
    }

    //RegisterBiomes 注册兜底生物群系到 BuiltInRegistries.BIOME 并把全部群系同步进容器工厂
    //真实群系由第 8 步数据驱动装载 这里只在 BIOME 里没有 plains 时补占位(无数据包场景)
    //容器工厂必须拿到全部群系: GlobalPalette.IdFor 对未注册值返回 0 只注册 plains 时
    //区段的群系调色板一旦溢出转全局 除 plains 外的群系会被静默写成 id 0 读回来全变平原
    private static void RegisterBiomes()
    {
        var plainsId = Identifier.WithDefaultNamespace("plains");
        if (!BuiltInRegistries.BIOME.ContainsKey(plainsId))
        {
            var plainsKey = ResourceKey<Biome>.Create(Registries.BIOME, plainsId);
            BuiltInRegistries.BIOME.Register(plainsKey, Biome.Plains, RegistrationInfo.BuiltIn);
        }
        //注册顺序即容器工厂里的全局 id 顺序 遍历注册表保持两边一致
        //RegisterBiome 内部走 IdMap 的幂等 Add 重复注册返回已有 id 不会打乱顺序
        var factory = PalettedContainerFactory.Default;
        foreach (var key in BuiltInRegistries.BIOME.RegistryKeySet)
        {
            var biome = BuiltInRegistries.BIOME.GetValue(key);
            if (biome is not null) factory.RegisterBiome(Holder<Biome>.Direct(biome));
        }
        var plains = BuiltInRegistries.BIOME.GetValue(plainsId) ?? Biome.Plains;
        factory.RegisterBiome(Holder<Biome>.Direct(plains));
    }

    public static bool IsBootstrapped
    {
        get { lock (_gate) return _bootstrapped; }
    }

    public static void Reset()
    {
        lock (_gate)
        {
            _bootstrapped = false;
            _preDataBootstrapped = false;
            DensityFunctionBootstrap.Reset();
        }
    }
}
