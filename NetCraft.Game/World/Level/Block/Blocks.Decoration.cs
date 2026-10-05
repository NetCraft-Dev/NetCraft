using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Block;

//V-8 装饰与功能类方块按原版逐个移植形状 交互与方块实体行为留后续批次
//属性一律取内嵌方块表注入的那份 这里不重复声明
public static partial class Blocks
{
    public static readonly CactusBlock CACTUS = new("cactus");
    public static readonly CactusFlowerBlock CACTUS_FLOWER = new("cactus_flower");
    public static readonly FarmlandBlock FARMLAND = new("farmland");
    public static readonly DirtPathBlock DIRT_PATH = new("dirt_path");
    public static readonly EnchantingTableBlock ENCHANTING_TABLE = new("enchanting_table");
    public static readonly BrewingStandBlock BREWING_STAND = new("brewing_stand");
    public static readonly EndPortalBlock END_PORTAL = new("end_portal");
    public static readonly EndPortalFrameBlock END_PORTAL_FRAME = new("end_portal_frame");
    public static readonly DragonEggBlock DRAGON_EGG = new("dragon_egg");
    public static readonly EnderChestBlock ENDER_CHEST = new("ender_chest");
    public static readonly DaylightDetectorBlock DAYLIGHT_DETECTOR = new("daylight_detector");
    public static readonly StructureVoidBlock STRUCTURE_VOID = new("structure_void");
    public static readonly SnifferEggBlock SNIFFER_EGG = new("sniffer_egg");
    public static readonly DriedGhastBlock DRIED_GHAST = new("dried_ghast");
    public static readonly ConduitBlock CONDUIT = new("conduit");
    public static readonly StonecutterBlock STONECUTTER = new("stonecutter");
    public static readonly SculkSensorBlock SCULK_SENSOR = new("sculk_sensor");
    public static readonly DecoratedPotBlock DECORATED_POT = new("decorated_pot");
    public static readonly HeavyCoreBlock HEAVY_CORE = new("heavy_core");
    public static readonly HoneyBlock HONEY_BLOCK = new("honey_block");
    public static readonly FrogspawnBlock FROGSPAWN = new("frogspawn");
    public static readonly BaseTorchBlock TORCH = new("torch");
    public static readonly BaseTorchBlock SOUL_TORCH = new("soul_torch");
    public static readonly LanternBlock LANTERN = new("lantern");
    public static readonly LanternBlock SOUL_LANTERN = new("soul_lantern");
    public static readonly CampfireBlock CAMPFIRE = new("campfire");
    public static readonly CampfireBlock SOUL_CAMPFIRE = new("soul_campfire");
    public static readonly BaseFireBlock FIRE = new("fire");
    public static readonly BaseFireBlock SOUL_FIRE = new("soul_fire");
    public static readonly SnowLayerBlock SNOW = new("snow");
    public static readonly CakeBlock CAKE = new("cake");
    public static readonly ComposterBlock COMPOSTER = new("composter");
    public static readonly LightBlock LIGHT = new("light");
    public static readonly TurtleEggBlock TURTLE_EGG = new("turtle_egg");
    public static readonly BambooStalkBlock BAMBOO = new("bamboo");
    public static readonly ScaffoldingBlock SCAFFOLDING = new("scaffolding");
    public static readonly PistonHeadBlock PISTON_HEAD = new("piston_head");
    public static readonly SeaPickleBlock SEA_PICKLE = new("sea_pickle");

    //十六种染料色 地毯旗帜蜡烛蛋糕都是同一形状挂十六个注册名
    private static readonly string[] DyeColors =
    {
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black",
    };

    //五种珊瑚色 死珊瑚与死珊瑚扇共用形状 活的另属珊瑚批次
    private static readonly string[] CoralColors = { "tube", "brain", "bubble", "fire", "horn" };

    //RegisterDecoration 装饰与功能类方块登记进真实方块表
    private static void RegisterDecoration(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks =
        {
            CACTUS, CACTUS_FLOWER, FARMLAND, DIRT_PATH, ENCHANTING_TABLE, BREWING_STAND,
            END_PORTAL, END_PORTAL_FRAME, DRAGON_EGG, ENDER_CHEST, DAYLIGHT_DETECTOR,
            STRUCTURE_VOID, SNIFFER_EGG, DRIED_GHAST, CONDUIT, STONECUTTER, SCULK_SENSOR,
            DECORATED_POT, HEAVY_CORE, HONEY_BLOCK, FROGSPAWN,
            TORCH, SOUL_TORCH, LANTERN, SOUL_LANTERN, CAMPFIRE, SOUL_CAMPFIRE, FIRE, SOUL_FIRE,
            SNOW, CAKE, COMPOSTER, LIGHT, TURTLE_EGG, BAMBOO, SCAFFOLDING, PISTON_HEAD, SEA_PICKLE,
        };
        foreach (var block in blocks) real[block.Id.Path] = block;

        foreach (var color in DyeColors)
        {
            real[$"{color}_carpet"] = new CarpetBlock($"{color}_carpet");
            real[$"{color}_banner"] = new BannerBlock($"{color}_banner");
            real[$"{color}_candle"] = new CandleBlock($"{color}_candle");
            real[$"{color}_candle_cake"] = new CandleCakeBlock($"{color}_candle_cake");
        }
        real["moss_carpet"] = new CarpetBlock("moss_carpet");

        foreach (var color in CoralColors)
        {
            real[$"dead_{color}_coral"] = new BaseCoralPlantBlock($"dead_{color}_coral");
            real[$"dead_{color}_coral_fan"] = new BaseCoralFanBlock($"dead_{color}_coral_fan");
        }
    }

    //CactusBlock 仙人掌 碰撞形状比视觉形状矮一像素 免得贴着走被扎
    public sealed class CactusBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);
        private static readonly VoxelShape ShapeCollision = NetCraft.Registry.Block.Column(14.0, 0.0, 15.0);
        private static readonly NetCraft.Primitives.Direction[] Horizontals =
        {
            NetCraft.Primitives.Direction.North, NetCraft.Primitives.Direction.South,
            NetCraft.Primitives.Direction.West, NetCraft.Primitives.Direction.East,
        };

        public CactusBlock(string name) : base(name) { }

        //CanSurvive 侧面贴着整块方块或挨着岩浆就活不了 上方不能是液体 下方得是同族或 supports_cactus
        //对应原版 canSurvive 原版按 legacySolid 判侧面 本作方块表没这一列 用碰撞形状占满整格近似
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            foreach (var direction in Horizontals)
            {
                var neighbourPos = pos.Offset(direction);
                if (level.GetBlockState(neighbourPos) is not { } neighbour) continue;
                if (neighbour.Owner is BlockBehaviour body
                    && body.IsCollisionShapeFullBlock(neighbour, EmptyBlockGetter.Instance, neighbourPos))
                    return false;
                if (IsLava(neighbour)) return false;
            }
            if (level.GetBlockState(pos.Offset(NetCraft.Primitives.Direction.Up))?.FluidState.IsEmpty == false)
                return false;
            var belowPos = pos.Offset(NetCraft.Primitives.Direction.Down);
            return level.GetBlockState(belowPos) is { } belowState
                && (belowState.Owner is CactusBlock
                    || (belowState.Owner is BlockBehaviour behaviour
                        && behaviour.IsInTag(BlockTags.SupportsCactus)));
        }

        //IsLava 这格是不是岩浆 本作只有水与岩浆两种流体 有流体且不是水就是岩浆
        private static bool IsLava(BlockState state)
            => state.FluidState is { IsEmpty: false, IsWater: false };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeCollision;
    }

    //CactusFlowerBlock 仙人掌花 长在仙人掌顶上
    public sealed class CactusFlowerBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 12.0);

        public CactusFlowerBlock(string name) : base(name) { }

        //MayPlaceOn 下方直接认 support_override_cactus_flower 否则按朝上那面的中心判定 对应原版 mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && (behaviour.IsInTag(BlockTags.SupportOverrideCactusFlower)
                    || behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, belowPos, belowState,
                        NetCraft.Primitives.Direction.Up, SupportType.Center));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //FarmlandBlock 耕地 顶面比整块低一像素
    public sealed class FarmlandBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 15.0);

        public FarmlandBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DirtPathBlock 土径 与耕地同高
    public sealed class DirtPathBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 15.0);

        public DirtPathBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CarpetBlock 地毯与苔藓地毯 一像素厚的薄片
    public sealed class CarpetBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.0);

        public CarpetBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EnchantingTableBlock 附魔台
    public sealed class EnchantingTableBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 12.0);

        public EnchantingTableBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BrewingStandBlock 酿造台 细杆加底座两段
    public sealed class BrewingStandBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(2.0, 2.0, 14.0),
            NetCraft.Registry.Block.Column(14.0, 0.0, 2.0));

        public BrewingStandBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EndPortalBlock 末地传送门 悬在格子中段 且不参与碰撞
    public sealed class EndPortalBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 6.0, 12.0);

        public EndPortalBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EndPortalFrameBlock 末地传送门框架 插了眼之后顶上多出一块
    public sealed class EndPortalFrameBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeEmpty = NetCraft.Registry.Block.Column(16.0, 0.0, 13.0);
        private static readonly VoxelShape ShapeFull = Shapes.Or(
            ShapeEmpty, NetCraft.Registry.Block.Column(8.0, 13.0, 16.0));

        public EndPortalFrameBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Eye) ? ShapeFull : ShapeEmpty;
    }

    //DragonEggBlock 龙蛋
    public sealed class DragonEggBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public DragonEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EnderChestBlock 末影箱
    public sealed class EnderChestBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public EnderChestBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DaylightDetectorBlock 阳光探测器 按天光与太阳角输出 0-15 反向时取补
    //原版靠方块实体 ticker 每 20 刻刷一次 本作没有 ticker 通道 改用调度刻自排达到同样周期
    public sealed class DaylightDetectorBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 6.0);

        //RefreshPeriod 刷新间隔 对应原版 tickEntity 里的 gameTime % 20
        private const int RefreshPeriod = 20;

        //TicksPerDay 一天的刻数 原版时间线周期
        private const int TicksPerDay = 24000;

        //SunNoonTick 太阳角轨道的起点 正午
        private const int SunNoonTick = 6000;

        //SkyFirstTick 天光乘子轨道的首个关键帧 早于它的时刻属于上一轮循环
        private const int SkyFirstTick = 133;

        //SkyLightBase 天光强度基准值 对应原版 sky_light_level 的默认值
        private const float SkyLightBase = 15f;

        //SkyMultiplierKeys 天光乘子关键帧 末尾补一个跨周期的等价点以便线性插值
        //对应原版 133:1.0 11867:1.0 13670:0.2667 22330:0.2667 之后回到 133
        private static readonly (int Tick, float Value)[] SkyMultiplierKeys =
        {
            (133, 1.0f),
            (11867, 1.0f),
            (13670, 0.26666668f),
            (22330, 0.26666668f),
            (24133, 1.0f),
        };

        //SunXCurve SunYCurve 太阳角缓动的两条分量曲线 由对称贝塞尔控制点展开
        private static readonly (float A, float B, float C) SunXCurve = BezierCurve(0.362f, 0.638f);
        private static readonly (float A, float B, float C) SunYCurve = BezierCurve(0.241f, 0.759f);

        public DaylightDetectorBlock(string name) : base(name) { }

        //Properties 状态必须自己声明 表里生成的属性实例与 BlockStateProperties 的单例不是同一个
        //不覆写的话 GetValue(BlockStateProperties.Power) 取不到值
        //顺序照 blocks.txt 的 inverted|power 走 顺序变了全局状态 id 会跟着错位
        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase>
            {
                ["inverted"] = BlockStateProperties.Inverted,
                ["power"] = BlockStateProperties.Power,
            };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //原版探测器按形状挡光 不满格 直接收满格天光会让下方早黑一个亮度
        public override bool UseShapeForLightOcclusion => true;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //OnPlace 刚放下先算一次并排上刷新周期 对应原版方块实体创建后首次 tick
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateSignalStrength(level, pos, state);
            level.ScheduleTick(pos, state.Owner, NextRefreshDelay(level));
        }

        //Tick 每到 20 的整数刻重算一次并续排 对应原版 tickEntity 的 gameTime % 20
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            UpdateSignalStrength(level, pos, state);
            level.ScheduleTick(pos, state.Owner, NextRefreshDelay(level));
        }

        //NextRefreshDelay 距下一次 20 整数刻还有多久 让所有探测器和原版一样同一刻刷新
        private static int NextRefreshDelay(ServerLevel level)
            => RefreshPeriod - (int)(level.GameTime % RefreshPeriod);

        //UseOn 空手右键切换反向 对应原版 useWithoutItem
        //冒险与旁观模式不给改 与原版 player.mayBuild 一致
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            var newState = state.Cycle(BlockStateProperties.Inverted);
            level.SetBlock(pos, newState, BlockUpdateFlags.Clients);
            UpdateSignalStrength(level, pos, newState);
            return true;
        }

        //UpdateSignalStrength 按天光与太阳角重算输出强度 对应原版同名方法
        //反向时直接取 15 减去天光 正向时再乘太阳高度余弦 夜里落到 0
        private static void UpdateSignalStrength(ServerLevel level, BlockPos pos, BlockState state)
        {
            var target = EffectiveSkyBrightness(level, pos);
            var sunAngle = SunAngle(level) * (float)(Math.PI / 180.0);
            if (state.GetValue(BlockStateProperties.Inverted))
            {
                target = 15 - target;
            }
            else if (target > 0)
            {
                var offset = sunAngle < Math.PI ? 0f : (float)(Math.PI * 2);
                sunAngle += (offset - sunAngle) * 0.2f;
                target = (int)Math.Round(target * Math.Cos(sunAngle));
            }
            target = Math.Clamp(target, 0, 15);
            if (state.GetValue(BlockStateProperties.Power) == target) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, target),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //EffectiveSkyBrightness 天光层亮度减去夜空变暗量 对应原版 getEffectiveSkyBrightness
        //原版不设下限 负值交给后面的 clamp 处理 反向模式下负数会被夹回 15
        private static int EffectiveSkyBrightness(ServerLevel level, BlockPos pos)
            => level.GetLightValue(LightLayer.Sky, pos) - SkyDarken(level.GameTime);

        //SunAngle 太阳角 单位为度 正午 0 度 午夜 180 度 对应原版 sun_angle 轨道
        //轨道只有一个循环段 从正午起算 一天刚好走满 360 度 缓动为对称三次贝塞尔
        private static float SunAngle(ServerLevel level)
            => 360f * EaseSunAngle(TickInDay(level.GameTime - SunNoonTick) / (float)TicksPerDay);

        //SkyDarken 夜空变暗量 0-11 对应原版 updateSkyBrightness
        //原版取 15 减去 sky_light_level 属性 该属性由时间线里的乘子轨道调制
        private static int SkyDarken(long gameTime)
            => (int)(SkyLightBase - SkyLightBase * SkyMultiplier(TickInDay(gameTime)));

        //SkyMultiplier 天光乘子 关键帧之间线性插值 抄自原版 sky_light_level 的乘子轨道
        //白天 1.0 夜里 0.2667(即 4/15) 黄昏 11867 刻起变暗 13670 刻到位 清晨原路恢复
        private static float SkyMultiplier(int tick)
        {
            //早于首个关键帧的时刻属于上一轮循环 折算到跨周期那一段上
            var t = tick < SkyFirstTick ? tick + TicksPerDay : tick;
            for (var i = 0; i < SkyMultiplierKeys.Length - 1; i++)
            {
                var (fromTick, fromValue) = SkyMultiplierKeys[i];
                var (toTick, toValue) = SkyMultiplierKeys[i + 1];
                if (t >= toTick) continue;
                var alpha = (t - fromTick) / (float)(toTick - fromTick);
                return fromValue + (toValue - fromValue) * alpha;
            }
            return SkyMultiplierKeys[^1].Value;
        }

        //TickInDay 把任意时刻折算到一天之内的 0..23999
        private static int TickInDay(long gameTime)
            => (int)(((gameTime % TicksPerDay) + TicksPerDay) % TicksPerDay);

        //EaseSunAngle 太阳角缓动 与 EasingType.CubicBezier.apply 同一套牛顿迭代加二分兜底
        //控制点由 symmetricCubicBezier(0.362, 0.241) 展开为 (0.362,0.241)(0.638,0.759)
        private static float EaseSunAngle(float x)
        {
            var t = x;
            for (var i = 0; i < 4; i++)
            {
                var error = SampleCurve(SunXCurve, t) - x;
                if (Math.Abs(error) < 1e-5f) return SampleCurve(SunYCurve, t);
                var gradient = SampleGradient(SunXCurve, t);
                if (gradient < 1e-5f) break;
                t -= Math.Clamp(error / gradient, -0.25f, 0.25f);
            }
            var lo = 0f;
            var hi = 1f;
            while (lo < hi)
            {
                var error = SampleCurve(SunXCurve, t) - x;
                if (Math.Abs(error) < 1e-5f) break;
                if (error < 0f) lo = t;
                else hi = t;
                t = (hi + lo) / 2f;
            }
            return SampleCurve(SunYCurve, t);
        }

        //BezierCurve 把三次贝塞尔分量控制点展开成多项式系数 对应原版 curveFromControls
        private static (float A, float B, float C) BezierCurve(float v1, float v2)
            => (3f * v1 - 3f * v2 + 1f, -6f * v1 + 3f * v2, 3f * v1);

        //SampleCurve 分量曲线取值 对应原版 CubicCurve.sample
        private static float SampleCurve((float A, float B, float C) curve, float t)
            => ((curve.A * t + curve.B) * t + curve.C) * t;

        //SampleGradient 分量曲线导数 对应原版 CubicCurve.sampleGradient
        private static float SampleGradient((float A, float B, float C) curve, float t)
            => (3f * curve.A * t + 2f * curve.B) * t + curve.C;
    }

    //StructureVoidBlock 结构空位 只看得见不挡路
    public sealed class StructureVoidBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Cube(6.0);

        public StructureVoidBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SnifferEggBlock 嗅探兽蛋 水平两轴尺寸不同
    public sealed class SnifferEggBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 12.0, 0.0, 16.0);

        public SnifferEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DriedGhastBlock 干枯恶魂
    public sealed class DriedGhastBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(10.0, 10.0, 0.0, 10.0);

        public DriedGhastBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //ConduitBlock 潮涌核心
    public sealed class ConduitBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Cube(6.0);

        public ConduitBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //StonecutterBlock 切石机
    public sealed class StonecutterBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 9.0);

        public StonecutterBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //UseOn 右击打开切石机界面 对应原版 StonecutterBlock.useWithoutItem
        //属性枚举里的 Direction 与本类型的同名 签名写全限定名
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction face)
        {
            player.OpenMenu(new StonecutterMenuProvider());
            return true;
        }

        //StonecutterMenuProvider 切石机菜单 标题用原版 container.stonecutter
        private sealed class StonecutterMenuProvider : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.stonecutter");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => new StonecutterMenu(containerId, inventory);
        }
    }

    //SculkSensorBlock 幽匿感测体
    public sealed class SculkSensorBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        public SculkSensorBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DecoratedPotBlock 饰纹陶罐
    public sealed class DecoratedPotBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public DecoratedPotBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HeavyCoreBlock 重核
    public sealed class HeavyCoreBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(8.0, 0.0, 8.0);

        public HeavyCoreBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HoneyBlock 蜂蜜块 只压碰撞形状 视觉形状仍是整块
    public sealed class HoneyBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 15.0);

        public HoneyBlock(string name) : base(name) { }

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => Shape;
    }

    //FrogspawnBlock 蛙卵 浮在水面的薄片
    public sealed class FrogspawnBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.5);

        public FrogspawnBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BannerBlock 旗帜
    public sealed class BannerBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(8.0, 0.0, 16.0);

        public BannerBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseCoralPlantBlock 死珊瑚
    public sealed class BaseCoralPlantBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 15.0);

        public BaseCoralPlantBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseCoralFanBlock 死珊瑚扇
    public sealed class BaseCoralFanBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 4.0);

        public BaseCoralFanBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseTorchBlock 火把与灵魂火把 立在格子中的细柱
    public sealed class BaseTorchBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(4.0, 0.0, 10.0);

        public BaseTorchBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //LanternBlock 灯笼 吊挂时整体上移一像素
    public sealed class LanternBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeStanding = Shapes.Or(
            NetCraft.Registry.Block.Column(4.0, 7.0, 9.0),
            NetCraft.Registry.Block.Column(6.0, 0.0, 7.0));
        private static readonly VoxelShape ShapeHanging = ShapeStanding.Move(0.0, 0.0625, 0.0).Optimize();

        public LanternBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Hanging) ? ShapeHanging : ShapeStanding;
    }

    //CandleBlock 蜡烛 插的支数越多形状越大
    public sealed class CandleBlock : NamedBlock
    {
        private static readonly VoxelShape[] Shapes =
        {
            NetCraft.Registry.Block.Column(2.0, 0.0, 6.0),
            NetCraft.Registry.Block.Box(5.0, 0.0, 6.0, 11.0, 6.0, 9.0),
            NetCraft.Registry.Block.Box(5.0, 0.0, 6.0, 10.0, 6.0, 11.0),
            NetCraft.Registry.Block.Box(5.0, 0.0, 5.0, 11.0, 6.0, 10.0),
        };

        public CandleBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shapes[state.GetValue(BlockStateProperties.Candles) - 1];
    }

    //CandleCakeBlock 插蜡烛的蛋糕 细蜡烛加低一圈的蛋糕体
    public sealed class CandleCakeBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(2.0, 8.0, 14.0),
            NetCraft.Registry.Block.Column(14.0, 0.0, 8.0));

        public CandleCakeBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CampfireBlock 营火与灵魂营火 七像素高的柴堆
    public sealed class CampfireBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 7.0);

        public CampfireBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CreateBlockEntity 营火上烤着的东西与各自进度存在方块实体里
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new CampfireBlockEntity(pos);

        //HasBlockEntity 营火带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        //UseOn 把手里的食材放到营火上 对应原版 CampfireBlock.useItemOn 的放食物分支
        //熄灭的营火不收食物 属性枚举里的 Direction 与本类型的同名 签名写全限定名
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction face)
        {
            if (!state.GetValue(BlockStateProperties.Lit)) return false;
            if (level.GetBlockEntity<CampfireBlockEntity>(pos) is not { } campfire) return false;
            var held = player.Inventory.GetSelectedItem();
            if (held.IsEmpty() || !campfire.PlaceFood(held)) return false;
            if (player.GameType != NetCraft.Game.World.Level.GameType.Creative) held.Shrink(1);
            return true;
        }

        //IsSmokeyPos 该位置是否处在营火烟柱上 向下一到五格找点燃的营火 对应原版 isSmokeyPos
        //原版还有"烟被实体方块挡住就只看再往下一格"的分支 本作只保留主语义
        public static bool IsSmokeyPos(ServerLevel level, BlockPos pos)
        {
            for (var i = 1; i <= 5; i++)
            {
                var state = level.GetBlockState(pos.Offset(0, -i, 0));
                if (state is null) return false;
                if (IsLitCampfire(state.Value)) return true;
            }
            return false;
        }

        //IsLitCampfire 是否点燃的营火 对应原版 isLitCampfire
        //原版按 CAMPFIRES 标签判定 本作营火只有内置两种
        public static bool IsLitCampfire(BlockState state)
            => (ReferenceEquals(state.Owner, CAMPFIRE) || ReferenceEquals(state.Owner, SOUL_CAMPFIRE))
                && state.HasProperty(BlockStateProperties.Lit)
                && state.GetValue(BlockStateProperties.Lit);
    }

    //BaseFireBlock 火与灵魂火 一像素厚的火苗层 且不参与碰撞
    public sealed class BaseFireBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.0);

        public BaseFireBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CanSurvive 火要站在上表面够坚固的方块上 对应原版 BaseFireBlock.canSurvive
        //Direction 全限定: 本文件同时引了 Primitives 与 Registry.Enums 两套 Direction
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(NetCraft.Primitives.Direction.Down);
            return level.GetBlockState(belowPos) is { } support
                && support.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, support, NetCraft.Primitives.Direction.Up);
        }

        //GetState 火焰落位状态 对应原版 getState
        //原版按脚下是不是灵魂土分普通火与灵魂火 nc 未注册灵魂土与灵魂沙 一律落普通火
        public static BlockState GetState(ServerLevel level, BlockPos pos) => Blocks.FIRE.DefaultBlockState;

        //CanBePlacedAt 该位置能不能点火 须为空位且火自身站得住 对应原版 canBePlacedAt
        //打火石点空气那支走它 原版的传送门分支在 nc 没传送门方块 不接
        public static bool CanBePlacedAt(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos) is not { } state || !state.Owner.IsAir) return false;
            var fire = GetState(level, pos);
            return fire.Owner is BlockBehaviour behaviour && behaviour.CanSurvive(level, pos, fire);
        }
    }

    //SnowLayerBlock 雪层 视觉形状按层数逐级加厚 碰撞只算下面那几层
    public sealed class SnowLayerBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(8, layer => NetCraft.Registry.Block.Column(16.0, 0.0, layer * 2));

        public SnowLayerBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.Layers)];

        //原版踩在雪上时按低一层的厚度算 免得站在薄雪上被顶起来
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeTable[state.GetValue(BlockStateProperties.Layers) - 1];
    }

    //CakeBlock 蛋糕 每被咬一口左边少两像素
    public sealed class CakeBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable = NetCraft.Registry.Block.Boxes(6,
            bite => NetCraft.Registry.Block.Box(1 + (bite * 2), 0.0, 1.0, 15.0, 8.0, 15.0));

        public CakeBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.Bites)];
    }

    //ComposterBlock 堆肥桶 中间挖一个随装填等级变浅的坑
    public sealed class ComposterBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable = BuildShapeTable();

        public ComposterBlock(string name) : base(name) { }

        //BuildShapeTable 整块挖去中间的坑 坑底随等级抬高 第八格是满桶沿用第七格
        private static VoxelShape[] BuildShapeTable()
        {
            var table = NetCraft.Registry.Block.Boxes(8, level => Shapes.Join(Shapes.Block(),
                NetCraft.Registry.Block.Column(12.0, Math.Clamp(1.0 + (level * 2), 2.0, 16.0), 16.0),
                BooleanOps.OnlyFirst));
            table[8] = table[7];
            return table;
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.LevelComposter)];

        //原版碰撞一律按空桶算 免得装满之后把站在旁边的实体挤出去
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeTable[0];
    }

    //LightBlock 光源方块 只发光不挡路
    public sealed class LightBlock : NamedBlock
    {
        public LightBlock(string name) : base(name) { }

        //原版手持光源物品时给整块便于摆放 物品未移植故取默认的无形
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shapes.Empty();
    }

    //TurtleEggBlock 海龟蛋 单个一窝是一小坨 多颗一窝摊成一圈
    public sealed class TurtleEggBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeSingle = NetCraft.Registry.Block.Box(3.0, 0.0, 3.0, 12.0, 7.0, 12.0);
        private static readonly VoxelShape ShapeMultiple = NetCraft.Registry.Block.Column(14.0, 0.0, 7.0);

        public TurtleEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Eggs) == 1 ? ShapeSingle : ShapeMultiple;
    }

    //BambooStalkBlock 竹竿 叶多时粗一圈 碰撞只算中间那根杆
    public sealed class BambooStalkBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeSmall = NetCraft.Registry.Block.Column(6.0, 0.0, 16.0);
        private static readonly VoxelShape ShapeLarge = NetCraft.Registry.Block.Column(10.0, 0.0, 16.0);
        private static readonly VoxelShape ShapeCollision = NetCraft.Registry.Block.Column(3.0, 0.0, 16.0);

        public BambooStalkBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.BambooLeavesProperty) == BambooLeaves.large ? ShapeLarge : ShapeSmall;

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeCollision;
    }

    //ScaffoldingBlock 脚手架 顶板加四角立杆 托在方块下方时多一圈贴地横梁
    public sealed class ScaffoldingBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeStable = BuildStableShape();
        private static readonly VoxelShape ShapeUnstableBottom = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);
        private static readonly VoxelShape ShapeUnstable = Shapes.Or(ShapeStable, ShapeUnstableBottom, BuildRingShape());
        private static readonly VoxelShape ShapeBelowBlock = Shapes.Block().Move(0.0, -1.0, 0.0).Optimize();

        public ScaffoldingBlock(string name) : base(name) { }

        //BuildStableShape 顶板加四根角柱 原版把一根角柱按水平四向转出来再并起来
        private static VoxelShape BuildStableShape() => Shapes.Or(
            NetCraft.Registry.Block.Column(16.0, 14.0, 16.0),
            Shapes.RotateHorizontal(NetCraft.Registry.Block.Box(0.0, 0.0, 0.0, 2.0, 16.0, 2.0))
                .Values.Aggregate(Shapes.Empty(), (acc, shape) => Shapes.Or(acc, shape)));

        //BuildRingShape 贴地那一圈横梁 同样靠旋转拼出来
        private static VoxelShape BuildRingShape() => Shapes.RotateHorizontal(
                NetCraft.Registry.Block.BoxZ(16.0, 0.0, 2.0, 0.0, 2.0))
            .Values.Aggregate(Shapes.Empty(), (acc, shape) => Shapes.Or(acc, shape));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Bottom) ? ShapeUnstable : ShapeStable;

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
        {
            //摆放预览不参与碰撞 否则准星里的脚手架会把玩家顶开
            if (context.IsPlacement) return Shapes.Empty();
            if (!context.IsAbove(Shapes.Block(), pos, true) || context.IsDescending())
            {
                //悬空但底下踩得到时才给那圈横梁的碰撞 否则整块穿过去
                if (state.GetValue(BlockStateProperties.StabilityDistance) != 0
                    && state.GetValue(BlockStateProperties.Bottom)
                    && context.IsAbove(ShapeBelowBlock, pos, true))
                    return ShapeUnstableBottom;
                return Shapes.Empty();
            }
            return ShapeStable;
        }
    }

    //PistonHeadBlock 活塞头 短臂比长臂少四像素
    //它必须贴着伸出的底座或正在移动的那截 底座没了自己也跟着消失
    public sealed class PistonHeadBlock : NamedBlock
    {
        private static readonly VoxelShape ShapePlatform = NetCraft.Registry.Block.BoxZ(16.0, 0.0, 4.0);
        private static readonly Dictionary<NetCraft.Primitives.Direction, VoxelShape> ShapesShort = Shapes.RotateAll(
            Shapes.Or(ShapePlatform, NetCraft.Registry.Block.BoxZ(4.0, 4.0, 16.0)));
        private static readonly Dictionary<NetCraft.Primitives.Direction, VoxelShape> ShapesNormal = Shapes.RotateAll(
            Shapes.Or(ShapePlatform, NetCraft.Registry.Block.BoxZ(4.0, 4.0, 20.0)));

        public PistonHeadBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => (state.GetValue(BlockStateProperties.Short) ? ShapesShort : ShapesNormal)
                [state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive()];

        //CanSurvive 后面得是伸出的底座或正在移动的那截 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (level.GetBlockState(basePos) is not { } baseState) return false;
            if (IsFittingBase(state, baseState)) return true;
            return baseState.Owner.Id.Path == "moving_piston"
                && baseState.GetValue(BlockStateProperties.FacingProperty)
                    == state.GetValue(BlockStateProperties.FacingProperty);
        }

        //UpdateShape 底座被换掉就消失 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour
                    == state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite
                && !CanSurvive(level, pos, state))
                return Blocks.AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        //NeighborChanged 自身没事时把更新转给底座 对应原版 neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            if (!CanSurvive(level, pos, state)) return;
            level.NeighborChanged(
                pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite),
                changedBlock);
        }

        //PlayerWillDestroy 创造模式拆活塞头时把底座也无掉落解掉 对应原版 playerWillDestroy
        //不解的话紧随其后的移除钩子会让底座掉出一个活塞 创造模式本不该掉任何东西
        public override void PlayerWillDestroy(ServerLevel level, ServerPlayer player, BlockPos pos,
            BlockState state)
        {
            if (player.GameType != NetCraft.Game.World.Level.GameType.Creative) return;
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (IsFittingBase(state, level.GetBlockState(basePos)))
                level.BlockUpdateSink?.DestroyBlock(basePos, false, BlockUpdateFlags.UpdateLimitDefault);
        }

        //AffectNeighborsAfterRemoval 活塞头被拆掉时底座也要掉 对应原版 affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (IsFittingBase(state, level.GetBlockState(basePos)))
                level.BlockUpdateSink?.DestroyBlock(basePos, true, BlockUpdateFlags.UpdateLimitDefault);
        }

        //IsFittingBase 底座是同类活塞 伸出了 朝向也对得上 对应原版 isFittingBase
        private static bool IsFittingBase(BlockState armState, BlockState? potentialBase)
        {
            if (potentialBase is not { } baseState) return false;
            var expected = armState.GetValue(BlockStateProperties.PistonTypeProperty) == PistonType.normal
                ? "piston"
                : "sticky_piston";
            return baseState.Owner.Id.Path == expected
                && baseState.GetValue(BlockStateProperties.Extended)
                && baseState.GetValue(BlockStateProperties.FacingProperty)
                    == armState.GetValue(BlockStateProperties.FacingProperty);
        }
    }

    //SeaPickleBlock 海泡菜 一格里的颗数越多摊得越开 四颗时还高一像素
    public sealed class SeaPickleBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeOne = NetCraft.Registry.Block.Column(4.0, 0.0, 6.0);
        private static readonly VoxelShape ShapeTwo = NetCraft.Registry.Block.Column(10.0, 0.0, 6.0);
        private static readonly VoxelShape ShapeThree = NetCraft.Registry.Block.Column(12.0, 0.0, 6.0);
        private static readonly VoxelShape ShapeFour = NetCraft.Registry.Block.Column(12.0, 0.0, 7.0);

        public SeaPickleBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Pickles) switch
            {
                2 => ShapeTwo,
                3 => ShapeThree,
                4 => ShapeFour,
                _ => ShapeOne,
            };
    }
}
