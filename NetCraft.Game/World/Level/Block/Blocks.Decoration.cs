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

//V-8 decoration and functional blocks ported one by one from vanilla with their shapes; interactions and block entity behavior are left to a later batch
//Properties always come from the injected embedded block table, they are not repeated here
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

    //Sixteen dye colors; carpets, banners, candles and cakes share the same shape across sixteen registry names
    private static readonly string[] DyeColors =
    {
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black",
    };

    //Five coral colors; dead coral and dead coral fans share a shape, live ones belong to the coral batch
    private static readonly string[] CoralColors = { "tube", "brain", "bubble", "fire", "horn" };

    //RegisterDecoration registers decoration and functional blocks into the real block table
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

    //CactusBlock cactus, the collision shape is one pixel shorter than the visual shape so walking alongside it does not hurt
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

        //CanSurvive it dies when the sides touch a full block or lava; above must not be a fluid and below must be the same family or supports_cactus
        //Maps to vanilla canSurvive; vanilla uses legacySolid for the sides but this project's block table has no such column, so a full-block collision shape is used as an approximation
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

        //IsLava whether this cell is lava; this project has only water and lava, so any fluid that is not water is lava
        private static bool IsLava(BlockState state)
            => state.FluidState is { IsEmpty: false, IsWater: false };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeCollision;
    }

    //CactusFlowerBlock cactus flower, grows on top of a cactus
    public sealed class CactusFlowerBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 12.0);

        public CactusFlowerBlock(string name) : base(name) { }

        //MayPlaceOn the block below is accepted directly if it is support_override_cactus_flower, otherwise it checks the center of the upward face, maps to vanilla mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && (behaviour.IsInTag(BlockTags.SupportOverrideCactusFlower)
                    || behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, belowPos, belowState,
                        NetCraft.Primitives.Direction.Up, SupportType.Center));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //FarmlandBlock farmland, the top face is one pixel below a full block
    public sealed class FarmlandBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 15.0);

        public FarmlandBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DirtPathBlock dirt path, same height as farmland
    public sealed class DirtPathBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 15.0);

        public DirtPathBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CarpetBlock carpet and moss carpet, a one-pixel-thick sheet
    public sealed class CarpetBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.0);

        public CarpetBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EnchantingTableBlock enchanting table
    public sealed class EnchantingTableBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 12.0);

        public EnchantingTableBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BrewingStandBlock brewing stand, a thin rod plus base in two parts
    public sealed class BrewingStandBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(2.0, 2.0, 14.0),
            NetCraft.Registry.Block.Column(14.0, 0.0, 2.0));

        public BrewingStandBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EndPortalBlock end portal, floats in the middle of the cell and does not take part in collisions
    public sealed class EndPortalBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 6.0, 12.0);

        public EndPortalBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EndPortalFrameBlock end portal frame, an extra top piece appears once an eye is inserted
    public sealed class EndPortalFrameBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeEmpty = NetCraft.Registry.Block.Column(16.0, 0.0, 13.0);
        private static readonly VoxelShape ShapeFull = Shapes.Or(
            ShapeEmpty, NetCraft.Registry.Block.Column(8.0, 13.0, 16.0));

        public EndPortalFrameBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Eye) ? ShapeFull : ShapeEmpty;
    }

    //DragonEggBlock dragon egg
    public sealed class DragonEggBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public DragonEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //EnderChestBlock ender chest
    public sealed class EnderChestBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public EnderChestBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DaylightDetectorBlock daylight detector, outputs 0-15 from sky light and sun angle, inverted takes the complement
    //Vanilla refreshes every 20 ticks via the block entity ticker; this project has no ticker channel, so scheduled ticks reschedule themselves for the same period
    public sealed class DaylightDetectorBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 6.0);

        //RefreshPeriod refresh interval, maps to gameTime % 20 in the vanilla tickEntity
        private const int RefreshPeriod = 20;

        //TicksPerDay ticks in a day, the vanilla timeline period
        private const int TicksPerDay = 24000;

        //SunNoonTick start of the sun angle track, noon
        private const int SunNoonTick = 6000;

        //SkyFirstTick first keyframe of the sky light multiplier track, earlier moments belong to the previous cycle
        private const int SkyFirstTick = 133;

        //SkyLightBase sky light strength baseline, maps to the default of vanilla sky_light_level
        private const float SkyLightBase = 15f;

        //SkyMultiplierKeys sky light multiplier keyframes, an equivalent point across the cycle is appended for linear interpolation
        //Maps to vanilla 133:1.0 11867:1.0 13670:0.2667 22330:0.2667 then back to 133
        private static readonly (int Tick, float Value)[] SkyMultiplierKeys =
        {
            (133, 1.0f),
            (11867, 1.0f),
            (13670, 0.26666668f),
            (22330, 0.26666668f),
            (24133, 1.0f),
        };

        //SunXCurve and SunYCurve are the two component curves of the sun angle easing, expanded from symmetric Bezier control points
        private static readonly (float A, float B, float C) SunXCurve = BezierCurve(0.362f, 0.638f);
        private static readonly (float A, float B, float C) SunYCurve = BezierCurve(0.241f, 0.759f);

        public DaylightDetectorBlock(string name) : base(name) { }

        //Properties states must be declared here; the property instances generated in the table are not the same as the BlockStateProperties singletons
        //Without the override GetValue(BlockStateProperties.Power) cannot find a value
        //The order follows inverted|power in blocks.txt; changing it shifts the global state ids
        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase>
            {
                ["inverted"] = BlockStateProperties.Inverted,
                ["power"] = BlockStateProperties.Power,
            };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //The vanilla detector occludes light by shape and is not full, taking full sky light directly would darken the space below one level early
        public override bool UseShapeForLightOcclusion => true;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //OnPlace computes once on placement and schedules the refresh cycle, maps to the first tick after the vanilla block entity is created
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateSignalStrength(level, pos, state);
            level.ScheduleTick(pos, state.Owner, NextRefreshDelay(level));
        }

        //Tick recomputes and reschedules at every multiple of 20, maps to gameTime % 20 in the vanilla tickEntity
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            UpdateSignalStrength(level, pos, state);
            level.ScheduleTick(pos, state.Owner, NextRefreshDelay(level));
        }

        //NextRefreshDelay ticks until the next multiple of 20, so all detectors refresh on the same tick like vanilla
        private static int NextRefreshDelay(ServerLevel level)
            => RefreshPeriod - (int)(level.GameTime % RefreshPeriod);

        //UseOn bare-hand right click toggles inverted, maps to vanilla useWithoutItem
        //Adventure and spectator modes cannot change it, same as vanilla player.mayBuild
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            var newState = state.Cycle(BlockStateProperties.Inverted);
            level.SetBlock(pos, newState, BlockUpdateFlags.Clients);
            UpdateSignalStrength(level, pos, newState);
            return true;
        }

        //UpdateSignalStrength recomputes the output strength from sky light and sun angle, maps to the vanilla method of the same name
        //Inverted takes 15 minus sky light directly; normal multiplies by the cosine of the sun height and drops to 0 at night
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

        //EffectiveSkyBrightness sky light level minus the night darkening, maps to vanilla getEffectiveSkyBrightness
        //Vanilla sets no lower bound and leaves negatives to the later clamp; in inverted mode negatives are clamped back to 15
        private static int EffectiveSkyBrightness(ServerLevel level, BlockPos pos)
            => level.GetLightValue(LightLayer.Sky, pos) - SkyDarken(level.GameTime);

        //SunAngle sun angle in degrees, noon 0 and midnight 180, maps to the vanilla sun_angle track
        //The track has a single cycle starting at noon, covering exactly 360 degrees in a day, eased with a symmetric cubic Bezier
        private static float SunAngle(ServerLevel level)
            => 360f * EaseSunAngle(TickInDay(level.GameTime - SunNoonTick) / (float)TicksPerDay);

        //SkyDarken night darkening 0-11, maps to vanilla updateSkyBrightness
        //Vanilla takes 15 minus the sky_light_level property, which is modulated by the multiplier track in the timeline
        private static int SkyDarken(long gameTime)
            => (int)(SkyLightBase - SkyLightBase * SkyMultiplier(TickInDay(gameTime)));

        //SkyMultiplier sky light multiplier linearly interpolated between keyframes, copied from the multiplier track of vanilla sky_light_level
        //1.0 by day and 0.2667 (4/15) at night; dusk darkens from tick 11867 reaching full at 13670, dawn restores the same way
        private static float SkyMultiplier(int tick)
        {
            //Moments before the first keyframe belong to the previous cycle and fold onto the cross-cycle segment
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

        //TickInDay folds any moment into 0..23999 within a day
        private static int TickInDay(long gameTime)
            => (int)(((gameTime % TicksPerDay) + TicksPerDay) % TicksPerDay);

        //EaseSunAngle sun angle easing, the same Newton iteration with bisection fallback as EasingType.CubicBezier.apply
        //Control points expanded from symmetricCubicBezier(0.362, 0.241) into (0.362,0.241)(0.638,0.759)
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

        //BezierCurve expands cubic Bezier component control points into polynomial coefficients, maps to vanilla curveFromControls
        private static (float A, float B, float C) BezierCurve(float v1, float v2)
            => (3f * v1 - 3f * v2 + 1f, -6f * v1 + 3f * v2, 3f * v1);

        //SampleCurve samples a component curve, maps to vanilla CubicCurve.sample
        private static float SampleCurve((float A, float B, float C) curve, float t)
            => ((curve.A * t + curve.B) * t + curve.C) * t;

        //SampleGradient derivative of a component curve, maps to vanilla CubicCurve.sampleGradient
        private static float SampleGradient((float A, float B, float C) curve, float t)
            => (3f * curve.A * t + 2f * curve.B) * t + curve.C;
    }

    //StructureVoidBlock structure void, visible but does not block
    public sealed class StructureVoidBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Cube(6.0);

        public StructureVoidBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SnifferEggBlock sniffer egg, the size differs on the two horizontal axes
    public sealed class SnifferEggBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 12.0, 0.0, 16.0);

        public SnifferEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DriedGhastBlock dried ghast
    public sealed class DriedGhastBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(10.0, 10.0, 0.0, 10.0);

        public DriedGhastBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //ConduitBlock conduit
    public sealed class ConduitBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Cube(6.0);

        public ConduitBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //StonecutterBlock stonecutter
    public sealed class StonecutterBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 9.0);

        public StonecutterBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //UseOn right click opens the stonecutter screen, maps to vanilla StonecutterBlock.useWithoutItem
        //The Direction in the property enum clashes with this type's name, the signature is fully qualified
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction face)
        {
            player.OpenMenu(new StonecutterMenuProvider());
            return true;
        }

        //StonecutterMenuProvider stonecutter menu, the title uses the vanilla container.stonecutter
        private sealed class StonecutterMenuProvider : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.stonecutter");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => new StonecutterMenu(containerId, inventory);
        }
    }

    //SculkSensorBlock sculk sensor
    public sealed class SculkSensorBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        public SculkSensorBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DecoratedPotBlock decorated pot
    public sealed class DecoratedPotBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public DecoratedPotBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HeavyCoreBlock heavy core
    public sealed class HeavyCoreBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(8.0, 0.0, 8.0);

        public HeavyCoreBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HoneyBlock honey block, only the collision shape is lowered, the visual shape is still a full block
    public sealed class HoneyBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 15.0);

        public HoneyBlock(string name) : base(name) { }

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => Shape;
    }

    //FrogspawnBlock frogspawn, a thin sheet floating on water
    public sealed class FrogspawnBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.5);

        public FrogspawnBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BannerBlock banner
    public sealed class BannerBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(8.0, 0.0, 16.0);

        public BannerBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseCoralPlantBlock dead coral
    public sealed class BaseCoralPlantBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 15.0);

        public BaseCoralPlantBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseCoralFanBlock dead coral fan
    public sealed class BaseCoralFanBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 4.0);

        public BaseCoralFanBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BaseTorchBlock torch and soul torch, a thin column standing in the cell
    public sealed class BaseTorchBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(4.0, 0.0, 10.0);

        public BaseTorchBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //LanternBlock lantern, raised one pixel overall when hanging
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

    //CandleBlock candle, the shape grows with the number of candles
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

    //CandleCakeBlock cake with candles, a thin candle plus a slightly lower cake body
    public sealed class CandleCakeBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(2.0, 8.0, 14.0),
            NetCraft.Registry.Block.Column(14.0, 0.0, 8.0));

        public CandleCakeBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CampfireBlock campfire and soul campfire, a seven-pixel-high pile of wood
    public sealed class CampfireBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 7.0);

        public CampfireBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CreateBlockEntity the items cooking on the campfire and their progress are stored in the block entity
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new CampfireBlockEntity(pos);

        //HasBlockEntity the campfire has a block entity and cannot be pushed by a piston
        public override bool HasBlockEntity => true;

        //UseOn puts the held food on the campfire, maps to the place-food branch of vanilla CampfireBlock.useItemOn
        //An extinguished campfire takes no food; the Direction in the property enum clashes with this type's name, the signature is fully qualified
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

        //IsSmokeyPos whether the position is in a campfire smoke column, looking one to five blocks down for a lit campfire, maps to vanilla isSmokeyPos
        //Vanilla also has a branch where a solid block blocking the smoke makes it look only one further down; only the main semantics are kept here
        public static bool IsSmokeyPos(ILevelReader level, BlockPos pos)
        {
            for (var i = 1; i <= 5; i++)
            {
                var state = level.GetBlockState(pos.Offset(0, -i, 0));
                if (state is null) return false;
                if (IsLitCampfire(state.Value)) return true;
            }
            return false;
        }

        //IsLitCampfire whether it is a lit campfire, maps to vanilla isLitCampfire
        //Vanilla checks the CAMPFIRES tag; this project only has the two built-in campfires
        public static bool IsLitCampfire(BlockState state)
            => (ReferenceEquals(state.Owner, CAMPFIRE) || ReferenceEquals(state.Owner, SOUL_CAMPFIRE))
                && state.HasProperty(BlockStateProperties.Lit)
                && state.GetValue(BlockStateProperties.Lit);
    }

    //BaseFireBlock fire and soul fire, a one-pixel-thick flame layer that does not take part in collisions
    public sealed class BaseFireBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 1.0);

        public BaseFireBlock(string name) : base(name) { }

        public override bool HasCollision => false;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CanSurvive fire must stand on a block whose top face is sturdy enough, maps to vanilla BaseFireBlock.canSurvive
        //Direction fully qualified: this file imports both the Primitives and Registry.Enums Direction
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(NetCraft.Primitives.Direction.Down);
            return level.GetBlockState(belowPos) is { } support
                && support.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, support, NetCraft.Primitives.Direction.Up);
        }

        //GetState placement state of the fire, maps to vanilla getState
        //Vanilla splits normal and soul fire by whether soul soil is below; nc does not register soul soil or soul sand, so normal fire is always placed
        public static BlockState GetState(ServerLevel level, BlockPos pos) => Blocks.FIRE.DefaultBlockState;

        //CanBePlacedAt whether fire can be lit here, the position must be empty and the fire must stand, maps to vanilla canBePlacedAt
        //Flint and steel on air goes through it; the vanilla portal branch is not wired up since nc has no portal block
        public static bool CanBePlacedAt(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos) is not { } state || !state.Owner.IsAir) return false;
            var fire = GetState(level, pos);
            return fire.Owner is BlockBehaviour behaviour && behaviour.CanSurvive(level, pos, fire);
        }
    }

    //SnowLayerBlock snow layer, the visual shape thickens with each layer and collision only counts the layers below
    public sealed class SnowLayerBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable =
            NetCraft.Registry.Block.Boxes(8, layer => NetCraft.Registry.Block.Column(16.0, 0.0, layer * 2));

        public SnowLayerBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.Layers)];

        //Vanilla uses the thickness one layer lower when standing on snow, so standing on thin snow does not push you up
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeTable[state.GetValue(BlockStateProperties.Layers) - 1];
    }

    //CakeBlock cake, each bite removes two pixels from the left
    public sealed class CakeBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable = NetCraft.Registry.Block.Boxes(6,
            bite => NetCraft.Registry.Block.Box(1 + (bite * 2), 0.0, 1.0, 15.0, 8.0, 15.0));

        public CakeBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[state.GetValue(BlockStateProperties.Bites)];
    }

    //ComposterBlock composter, a pit in the middle that gets shallower with the fill level
    public sealed class ComposterBlock : NamedBlock
    {
        private static readonly VoxelShape[] ShapeTable = BuildShapeTable();

        public ComposterBlock(string name) : base(name) { }

        //BuildShapeTable digs the middle pit out of a full block, the pit floor rises with the level, the eighth level is full and reuses the seventh
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

        //Vanilla always uses the empty composter collision so a filled one does not push out entities standing beside it
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => ShapeTable[0];
    }

    //LightBlock light block, emits light without blocking
    public sealed class LightBlock : NamedBlock
    {
        public LightBlock(string name) : base(name) { }

        //Vanilla gives a full block when the light item is held to ease placement; the item is not ported so the default invisible shape is used
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shapes.Empty();
    }

    //TurtleEggBlock turtle egg, a single egg is a small lump and multiple eggs spread into a ring
    public sealed class TurtleEggBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeSingle = NetCraft.Registry.Block.Box(3.0, 0.0, 3.0, 12.0, 7.0, 12.0);
        private static readonly VoxelShape ShapeMultiple = NetCraft.Registry.Block.Column(14.0, 0.0, 7.0);

        public TurtleEggBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Eggs) == 1 ? ShapeSingle : ShapeMultiple;
    }

    //BambooStalkBlock bamboo stalk, thicker with more leaves, collision only counts the central stalk
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

    //ScaffoldingBlock scaffolding, a top board plus four corner posts, with an extra ground-level ring of bars when supported from below
    public sealed class ScaffoldingBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeStable = BuildStableShape();
        private static readonly VoxelShape ShapeUnstableBottom = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);
        private static readonly VoxelShape ShapeUnstable = Shapes.Or(ShapeStable, ShapeUnstableBottom, BuildRingShape());
        private static readonly VoxelShape ShapeBelowBlock = Shapes.Block().Move(0.0, -1.0, 0.0).Optimize();

        public ScaffoldingBlock(string name) : base(name) { }

        //BuildStableShape top board plus four corner posts; vanilla rotates one corner post into the four horizontal directions and merges them
        private static VoxelShape BuildStableShape() => Shapes.Or(
            NetCraft.Registry.Block.Column(16.0, 14.0, 16.0),
            Shapes.RotateHorizontal(NetCraft.Registry.Block.Box(0.0, 0.0, 0.0, 2.0, 16.0, 2.0))
                .Values.Aggregate(Shapes.Empty(), (acc, shape) => Shapes.Or(acc, shape)));

        //BuildRingShape the ground-level ring of bars, also assembled by rotation
        private static VoxelShape BuildRingShape() => Shapes.RotateHorizontal(
                NetCraft.Registry.Block.BoxZ(16.0, 0.0, 2.0, 0.0, 2.0))
            .Values.Aggregate(Shapes.Empty(), (acc, shape) => Shapes.Or(acc, shape));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Bottom) ? ShapeUnstable : ShapeStable;

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
        {
            //The placement preview does not take part in collisions, otherwise the scaffolding in the crosshair would push the player away
            if (context.IsPlacement) return Shapes.Empty();
            if (!context.IsAbove(Shapes.Block(), pos, true) || context.IsDescending())
            {
                //The ring of bars only collides when floating but reachable from below, otherwise everything passes through
                if (state.GetValue(BlockStateProperties.StabilityDistance) != 0
                    && state.GetValue(BlockStateProperties.Bottom)
                    && context.IsAbove(ShapeBelowBlock, pos, true))
                    return ShapeUnstableBottom;
                return Shapes.Empty();
            }
            return ShapeStable;
        }
    }

    //PistonHeadBlock piston head, the short arm is four pixels less than the long arm
    //It must touch an extended base or the moving segment, and disappears when the base is gone
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

        //CanSurvive what is behind must be an extended base or the moving segment, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (level.GetBlockState(basePos) is not { } baseState) return false;
            if (IsFittingBase(state, baseState)) return true;
            return baseState.Owner.Id.Path == "moving_piston"
                && baseState.GetValue(BlockStateProperties.FacingProperty)
                    == state.GetValue(BlockStateProperties.FacingProperty);
        }

        //UpdateShape disappears when the base is replaced, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Primitives.Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour
                    == state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite
                && !CanSurvive(level, pos, state))
                return Blocks.AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        //NeighborChanged forwards the update to the base when it is fine itself, maps to vanilla neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            if (!CanSurvive(level, pos, state)) return;
            level.NeighborChanged(
                pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite),
                changedBlock);
        }

        //PlayerWillDestroy removes the base without drops when breaking the piston head in creative, maps to vanilla playerWillDestroy
        //Without it the following removal hook would drop a piston from the base, and creative should drop nothing
        public override void PlayerWillDestroy(ServerLevel level, ServerPlayer player, BlockPos pos,
            BlockState state)
        {
            if (player.GameType != NetCraft.Game.World.Level.GameType.Creative) return;
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (IsFittingBase(state, level.GetBlockState(basePos)))
                level.BlockUpdateSink?.DestroyBlock(basePos, false, BlockUpdateFlags.UpdateLimitDefault);
        }

        //AffectNeighborsAfterRemoval the base must also drop when the piston head is broken, maps to vanilla affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            var basePos = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            if (IsFittingBase(state, level.GetBlockState(basePos)))
                level.BlockUpdateSink?.DestroyBlock(basePos, true, BlockUpdateFlags.UpdateLimitDefault);
        }

        //IsFittingBase the base is a matching piston, is extended and faces the right way, maps to vanilla isFittingBase
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

    //SeaPickleBlock sea pickle, more pickles in a cell spread wider, four also sit one pixel higher
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
