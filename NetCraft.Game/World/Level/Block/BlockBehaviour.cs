using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Game.World.Items.Component;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Block;

//BlockBehaviour block behavior base class, maps to vanilla net.minecraft.world.level.block.state.BlockBehaviour
//Extends the abstract Block base class and provides the base behavior contract such as Properties/StateDefinition
//Subclasses override the concrete behavior methods as needed, this class only carries the state definition and default state
//Behavior callbacks default to empty, the server block update entry dispatches them by event and the client is not involved
//The current namespace segment named Block clashes with the Registry.Block type, so fully qualified names avoid ambiguity
public abstract class BlockBehaviour : NetCraft.Registry.Block, IBlockUpdateBehaviour, IBlockSignalBehaviour,
    IEntityInsideBehaviour
{
    private BlockState? _defaultState;
    private BlockStateDefinition? _stateDefinition;
    private PropertyBase[]? _tableProperties;
    private IReadOnlyDictionary<string, string>? _defaultStateValues;
    private PlacementKind _placement = PlacementKind.None;
    private bool _canOcclude = true;
    private NetCraft.Registry.Enums.PushReaction _pushReaction = NetCraft.Registry.Enums.PushReaction.normal;
    private bool? _redstoneConductor;

    //Properties block property set, defaults to the one injected from the embedded block table
    //The table is extracted from vanilla so real classes need not repeat the properties; missing one shifts the state count and the global ids with it
    //Blocks with unusual states can still override and declare their own
    public virtual IDictionary<string, PropertyBase> Properties
        => _tableProperties is { Length: > 0 }
            ? _tableProperties.ToDictionary(p => p.Name)
            : new Dictionary<string, PropertyBase>();

    //ApplyTableProperties fills in the properties given by the second column of the embedded block table during registration
    //Must be called before the first access to StateDefinition, once the state definition is built the properties are fixed
    internal void ApplyTableProperties(PropertyBase[] properties) => _tableProperties = properties;

    //Instrument the instrument this block gives as a note block base, maps to instrument in vanilla Properties
    //Vanilla specifies them one by one at registration; here they are extracted from vanilla Blocks.java into a table and injected by the registration flow
    //The Enums namespace segment also has same-named enums like Direction, so fully qualified names avoid ambiguity here
    public NetCraft.Registry.Enums.NoteBlockInstrument Instrument { get; private set; } =
        NetCraft.Registry.Enums.NoteBlockInstrument.harp;

    internal void ApplyInstrument(NetCraft.Registry.Enums.NoteBlockInstrument instrument)
        => Instrument = instrument;

    //DefaultStateValues overrides of the default state relative to the first entry of the state definition, maps to registerDefaultState at the end of the vanilla constructor
    //The key is the property name and the value is the serialized name; unlisted properties keep the first value in any() order, null means the default already is any()
    public IReadOnlyDictionary<string, string>? DefaultStateValues => _defaultStateValues;

    //ApplyTableDefaults fills in the third column of the embedded block table during registration
    //Must be called before the first access to DefaultBlockState, otherwise the state is already built and cannot be changed
    internal void ApplyTableDefaults(IReadOnlyDictionary<string, string>? defaults)
        => _defaultStateValues = defaults;

    //ApplyTablePlacement fills in the placement category given by the place= section of the embedded block table during registration
    internal void ApplyTablePlacement(PlacementKind placement) => _placement = placement;

    //ApplyTableOcclusion fills in the occlude= section of the embedded block table during registration
    internal void ApplyTableOcclusion(bool canOcclude) => _canOcclude = canOcclude;

    //ApplyTablePushReaction fills in the push= section of the embedded block table during registration
    internal void ApplyTablePushReaction(NetCraft.Registry.Enums.PushReaction reaction)
        => _pushReaction = reaction;

    //ApplyTableRedstoneConductor fills in the conductor= section of the embedded block table during registration
    internal void ApplyTableRedstoneConductor(bool? conductor) => _redstoneConductor = conductor;

    //PushReaction reaction when pushed by a piston, unlisted blocks fall back to the base class normal
    public override NetCraft.Registry.Enums.PushReaction PushReaction => _pushReaction;

    //CanOcclude whether it occludes light, maps to vanilla Properties.noOcclusion
    //Blocks with it off treat the occlusion shape as empty, so light attenuation drops to 0 or 1 instead of a full 15
    public override bool CanOcclude => _canOcclude;

    //StateDefinition block state definition, built lazily on first access, it builds all possible states
    public BlockStateDefinition StateDefinition
        => _stateDefinition ??= BuildStateDefinition();

    //DefaultBlockState default state of the block, the first state of StateDefinition
    public override BlockState DefaultBlockState
        => _defaultState ??= CreateDefaultState();

    //CreateDefaultState takes any() as the default then applies each DefaultStateValues override
    //In vanilla the registerDefaultState of most blocks is any(), but 647 blocks still change it explicitly
    //BooleanProperty values are ordered true,false and the enum takes declaration order; without the overrides you land on wrong values like piston extended=true
    protected virtual BlockState CreateDefaultState()
    {
        var state = StateDefinition.PossibleStates[0];
        if (DefaultStateValues is not { Count: > 0 } overrides) return state;
        foreach (var (name, valueName) in overrides)
        {
            if (StateDefinition.GetProperty(name) is not { } property) continue;
            if (property.GetValueForName(valueName) is not { } value) continue;
            state = state.SetValue(property, value);
        }
        return state;
    }

    //AllStates all possible states expanded from the state definition
    public override IReadOnlyList<BlockState> AllStates => StateDefinition.PossibleStates;

    //OnPlace callback after the block is placed, maps to vanilla onPlace, no behavior by default
    public virtual void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
        bool movedByPiston) { }

    //PlayerDestroy callback after a player breaks the block, no behavior by default
    public virtual void PlayerDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //PlayerWillDestroy callback before a player breaks the block, maps to vanilla playerWillDestroy
    //Called before drops and clearing, multi-block blocks use it to remove the other half without drops
    //Otherwise breaking a door or piston head in creative drops the other half as an item; the vanilla path is guarded by preventsBlockDrops
    public virtual void PlayerWillDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //IsInTag whether this block belongs to a block tag, maps to vanilla BlockState.is(TagKey)
    //"What vegetation and crops can be planted on" relies entirely on this; tag data is bound by TagsReloadListener at startup
    //When tags are not bound it counts as not belonging, matching the empty-set semantics of vanilla
    public bool IsInTag(NetCraft.Registry.TagKey<NetCraft.Registry.Block> tag)
    {
        var self = NetCraft.Registry.BuiltInRegistries.BLOCK.Get(Id);
        return self is not null
            && NetCraft.Registry.BuiltInRegistries.BLOCK.Get(tag)?.Contains(self) == true;
    }

    //OnAttack callback when a player starts mining the block, maps to vanilla Block.attack
    //Note blocks use it for left-click previewing, placed before the destroy progress like vanilla
    public virtual void OnAttack(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //SetPlacedBy callback after a player places the block, maps to vanilla setPlacedBy, no behavior by default
    //Repeaters rely on it to schedule the first tick; if the input already has a signal at placement it cannot wait for a neighbor change
    public virtual void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player) { }

    //GetDrops drops on break, defaults to the block's own block item, maps to vanilla blocks without a special loot table
    //Differences like grass dropping dirt or stone dropping cobblestone are handled by overriding this per block later
    public virtual IEnumerable<ItemStack> GetDrops(ServerLevel level, ServerPlayer? player, BlockPos pos, BlockState state)
    {
        //A block's loot table sits at blocks/<id> in its own namespace; a block without one drops its own block item
        var table = NetCraft.Game.World.Loot.LootTables.Active.Get(
            Identifier.FromNamespaceAndPath(Id.Namespace, $"blocks/{Id.Path}"));
        if (table is not null)
        {
            var parameters = new NetCraft.Game.World.Loot.LootParams.Builder(level)
                .WithParameter(NetCraft.Game.World.Loot.LootContextParams.Origin,
                    new Vec3(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5))
                .WithParameter(NetCraft.Game.World.Loot.LootContextParams.Tool, ItemStack.Empty)
                .WithParameter(NetCraft.Game.World.Loot.LootContextParams.BlockState, state)
                .Create(NetCraft.Game.World.Loot.LootContextParamSets.Block);
            return table.GetRandomItems(parameters);
        }
        //The namespace segment Items clashes with the registry class, it must be fully qualified or it resolves to the namespace
        var item = NetCraft.Game.World.Items.Items.ItemForBlock(this);
        if (item is null || ReferenceEquals(item, NetCraft.Game.World.Items.Items.AIR))
            return Array.Empty<ItemStack>();
        return new[] { new ItemStack(item.BuiltInRegistryHolder, 1, DataComponentPatch.Empty) };
    }

    //UseOn a player uses the block on the given face, returns whether the interaction was consumed, unhandled by default
    public virtual bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        => false;

    //CanBeReplaced whether the block at this position can be replaced directly by placing a held block, maps to vanilla canBeReplaced
    //Coverable blocks such as air and water return true
    public virtual bool CanBeReplaced => false;

    //GetStateForPlacement computes the placement state from the context, null means it cannot be placed on this face
    //face is the side the player clicked and horizontalFacing is the player's horizontal facing, maps to vanilla getStateForPlacement
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing) => PlacementState(face, horizontalFacing, horizontalFacing);

    //GetStateForPlacement the six-direction looking version, blocks with up/down facing like observers override it
    //The directional/looking table-driven facings need the looking direction, so only they are handled at this level
    //Everything else is delegated to the four-argument version; levers and trapdoors override that one, so without delegation their implementations would never be called
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing, Direction lookingDirection)
        => _placement is PlacementKind.SixFacing or PlacementKind.LookingFacing
            ? PlacementState(face, horizontalFacing, lookingDirection)
            : GetStateForPlacement(level, pos, face, horizontalFacing);

    //GetStateForPlacement the hit-point version; trapdoors pick top or bottom from the hit height and doors pick the hinge side from the horizontal hit position
    //hitLocal is the hit point within the block with all three components between 0 and 1, maps to vanilla BlockPlaceContext.getClickLocation
    //The base class forwards to the five-argument version by default so existing blocks need no change
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing, Direction lookingDirection, Vec3 hitLocal)
        => GetStateForPlacement(level, pos, face, horizontalFacing, lookingDirection);

    //PlacementState applies the three generic vanilla facings from the category in the embedded block table
    //In 26.2 the facing is no longer provided by the parent class but implemented per block class, only these three patterns can be copied verbatim
    //Other blocks override this method themselves, a category of None falls back to the default state
    private BlockState PlacementState(Direction face, Direction horizontalFacing, Direction lookingDirection)
        => _placement switch
        {
            PlacementKind.HorizontalFacing => DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing,
                ToPropertyDirection(horizontalFacing.Opposite)),
            PlacementKind.SixFacing => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty,
                ToPropertyDirection(lookingDirection.Opposite)),
            //The observer writes a double negation, equivalent to using the looking direction directly
            PlacementKind.LookingFacing => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty,
                ToPropertyDirection(lookingDirection)),
            PlacementKind.PillarAxis => DefaultBlockState.SetValue(BlockStateProperties.AxisProperty,
                ToPropertyAxis(face.AxisValue)),
            _ => DefaultBlockState,
        };

    //ToPropertyDirection converts a vanilla Direction into the enum member used by block properties
    //Property enum member names are serialized names, they happen to share the order of Primitives Id3D but no numeric assumption is made
    private static NetCraft.Registry.Enums.Direction ToPropertyDirection(Direction direction)
        => direction.Id3D switch
        {
            Direction.DownId => NetCraft.Registry.Enums.Direction.down,
            Direction.UpId => NetCraft.Registry.Enums.Direction.up,
            Direction.NorthId => NetCraft.Registry.Enums.Direction.north,
            Direction.SouthId => NetCraft.Registry.Enums.Direction.south,
            Direction.WestId => NetCraft.Registry.Enums.Direction.west,
            _ => NetCraft.Registry.Enums.Direction.east,
        };

    private static NetCraft.Registry.Enums.Axis ToPropertyAxis(Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => NetCraft.Registry.Enums.Axis.x,
        Direction.Axis.Y => NetCraft.Registry.Enums.Axis.y,
        _ => NetCraft.Registry.Enums.Axis.z,
    };

    //CanSurvive whether the state can stay at the current position, checked on shape updates, maps to vanilla canSurvive which always survives by default
    public virtual bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state) => true;

    //IsFaceSturdy whether a face is sturdy enough to attach to, used by attached blocks like torches and levers, maps to vanilla isFaceSturdy
    //Must use the real world given by the caller: a moving piston's shape comes from its block entity and fences/walls read neighbor chunks
    //Using an empty world view would judge these blocks as having no shape, and attachments above a retracting piston would be dropped the moment it retracts
    //A persistent level implements BlockGetter and is used directly, in-memory levels that cannot give shapes fall back to the empty view
    public virtual bool IsFaceSturdy(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => IsFaceSturdy(level as BlockGetter ?? EmptyBlockGetter.Instance, pos, state, direction, SupportType.Full);

    public virtual bool IsFaceSturdy(BlockGetter level, BlockPos pos, BlockState state, Direction direction,
        SupportType supportType)
        => supportType.IsSupporting(state, level, pos, direction);

    //HasCollision whether it takes part in collisions, air and fluids turn it off, maps to vanilla Properties.hasCollision
    public virtual bool HasCollision => true;

    //GetShape visual shape, a full block by default, maps to vanilla getShape
    public virtual VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
        => Shapes.Block();

    //GetCollisionShape collision shape defaults to the visual shape, maps to vanilla getCollisionShape
    public virtual VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => HasCollision ? state.GetShape(level, pos) : Shapes.Empty();

    //GetInteractionShape crosshair ray hit box, empty by default, maps to vanilla getInteractionShape
    public virtual VoxelShape GetInteractionShape(BlockState state, BlockGetter level, BlockPos pos)
        => Shapes.Empty();

    //GetBlockSupportShape shape used for attachment checks, defaults to the collision shape, maps to vanilla getBlockSupportShape
    public virtual VoxelShape GetBlockSupportShape(BlockState state, BlockGetter level, BlockPos pos)
        => GetCollisionShape(state, level, pos, CollisionContext.Empty);

    //GetOcclusionShape occlusion shape defaults to the visual shape, maps to vanilla getOcclusionShape
    public virtual VoxelShape GetOcclusionShape(BlockState state, BlockGetter level, BlockPos pos)
        => state.GetShape(level, pos);

    //GetOcclusionShape light checks only need the shape itself, taken from the visual shape under the empty world view like vanilla
    public override VoxelShape GetOcclusionShape(BlockState state)
        => state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero);

    //PropagatesSkylightDown whether sky light passes straight down through the state, maps to vanilla propagatesSkylightDown
    //Vanilla defaults to checking whether the visual shape fills the block and there is no fluid
    public override bool PropagatesSkylightDown(BlockState state)
        => !IsShapeFullBlock(state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero)) && state.FluidState.IsEmpty;

    //GetVisualShape visual shape defaults to the collision shape, maps to vanilla getVisualShape
    public virtual VoxelShape GetVisualShape(BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => GetCollisionShape(state, level, pos, context);

    //IsCollisionShapeFullBlock whether the collision shape fills the block, maps to vanilla isCollisionShapeFullBlock
    public virtual bool IsCollisionShapeFullBlock(BlockState state, BlockGetter level, BlockPos pos)
        => NetCraft.Registry.Block.IsShapeFullBlock(state.GetCollisionShape(level, pos));

    //IsAir whether it is an air block, maps to vanilla BlockState.isAir, surface rules use it to detect empty columns
    public override bool IsAir => false;

    //HasFluidState whether it has a fluid, the negation of vanilla getFluidState().isEmpty(), water/lava return true
    public virtual bool HasFluidState => false;

    //NeighborChanged callback after a neighboring block changes, used by redstone and supporting blocks, maps to vanilla neighborChanged, no behavior by default
    //changedBlock is the block that changed, not this block
    public virtual void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston) { }

    //UpdateShape recomputes itself after a neighbor's shape changes, maps to vanilla updateShape, returns the same state by default
    //directionToNeighbour is the direction from this block toward the neighbor
    public virtual BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState) => state;

    //UpdateIndirectNeighbourShapes indirect shape update, maps to vanilla updateIndirectNeighbourShapes, no behavior by default
    public virtual void UpdateIndirectNeighbourShapes(ServerLevel level, BlockPos pos, BlockState state,
        int updateFlags, int updateLimit) { }

    //AffectNeighborsAfterRemoval extra effects on neighbors after this block is removed, maps to the vanilla method of the same name, no behavior by default
    public virtual void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
        bool movedByPiston) { }

    //Destroy callback after a player breaks this block, maps to vanilla Block.destroy, no behavior by default
    //Vanilla calls it only after the block is already cleared and only when it was actually replaced
    public virtual void Destroy(ServerLevel level, BlockPos pos, BlockState state) { }

    //Tick scheduled tick callback, maps to vanilla Block.tick, no behavior by default
    public virtual void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //OnEntityInside an entity entered this block's cell, maps to vanilla entityInside, no behavior by default
    public virtual void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state) { }

    //OnProjectileHit callback when hit by a projectile, maps to vanilla onProjectileHit, no behavior by default
    //hit carries the hit point and entered face; blocks like targets use the hit point to compute the output strength
    public virtual void OnProjectileHit(ServerLevel level, BlockState state, BlockHitResult hit,
        NetCraft.Game.World.Entity.Projectile projectile) { }

    //TriggerEvent block event callback, maps to vanilla Block.triggerEvent, unhandled by default
    public virtual bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB)
        => false;

    //IsSignalSource whether it is a redstone signal source, maps to vanilla isSignalSource, false by default
    public virtual bool IsSignalSource => false;

    //IsDiode whether it is a diode, that is a repeater or comparator, maps to vanilla DiodeBlock.isDiode
    public virtual bool IsDiode => false;

    //CreateBlockEntity creates the block entity when the block is placed, null for blocks without one, maps to vanilla EntityBlock.newBlockEntity
    public virtual BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => null;

    //HasBlockEntity whether this block has a block entity, maps to vanilla BlockState.hasBlockEntity
    //Piston pushability checks use it; block entity blocks are never pushable in vanilla
    public virtual bool HasBlockEntity => false;

    //OwnSignal the block's own signal strength, maps to vanilla ownSignal, 0 by default
    public virtual int OwnSignal(ServerLevel level, BlockPos pos, BlockState state) => 0;

    //GetSignal signal strength output toward the given direction, defaults to its own strength, maps to vanilla getSignal
    //direction is the direction from the receiver toward this block
    public virtual int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => OwnSignal(level, pos, state);

    //GetDirectSignal direct signal, this is what conductive blocks transmit, maps to vanilla getDirectSignal, 0 by default
    public virtual int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction) => 0;

    //IsRedstoneConductor whether it is a redstone conductor, maps to vanilla isRedstoneConductor
    //Vanilla defaults to whether the collision shape fills the block; glass, leaves, observers and TNT have a full shape but are explicitly set to never, while soul sand and mud have a partial shape but are set to always
    //These two exceptions come from the conductor= section of the embedded block table, unlisted ones use the default shape check
    public virtual bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state)
        => _redstoneConductor ?? IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);

    //HasAnalogOutputSignal whether it has an analog output, comparators use it to decide whether to read, maps to vanilla hasAnalogOutputSignal
    public virtual bool HasAnalogOutputSignal => false;

    //GetAnalogOutputSignal analog output strength, maps to vanilla getAnalogOutputSignal, 0 by default
    public virtual int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => 0;

    //DestroySpeed block hardness, the divisor of destroy progress, negative means unbreakable, maps to vanilla destroyTime
    public virtual float DestroySpeed => 1f;

    //RequiresCorrectToolForDrops whether the correct tool is needed to mine it properly, maps to vanilla requiresCorrectToolForDrops
    //This project has no tool system, so blocks that need one are treated as bare-handed and take several times longer than mineable blocks
    public virtual bool RequiresCorrectToolForDrops => false;

    //GetDestroyProgress destroy progress per tick, maps to vanilla BlockBehaviour.getDestroyProgress
    //Progress grows linearly as "player mining speed / hardness / tool divisor", reaching 1 breaks the block
    //Bare-handed mining speed is 1, blocks that need a tool divide by 100 and the rest by 30, unbreakable blocks are always 0
    public static float GetDestroyProgress(BlockState state)
        => state.Owner is not BlockBehaviour behaviour || behaviour.DestroySpeed < 0
            ? 0f
            : 1f / behaviour.DestroySpeed / (behaviour.RequiresCorrectToolForDrops ? 100f : 30f);

    //RandomTicks whether it takes part in random ticks, maps to vanilla Properties.randomTicks, off by default
    public override bool RandomTicks => false;

    //RandomTick random tick callback, only called for blocks with RandomTicks true, sampled by randomTickSpeed
    public virtual void RandomTick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //BuildStateDefinition builds the state definition from Properties
    private BlockStateDefinition BuildStateDefinition()
        => new(this, Properties);
}

//PlacementKind generic implementation categories for placement facing, maps to the facing expressions in each vanilla block's getStateForPlacement
//Only the three patterns that can be copied verbatim are generalized; other blocks have neighbor or double-layer checks and are ported individually
public enum PlacementKind
{
    None,

    //facing is the opposite of the player's horizontal facing, maps to context.getHorizontalDirection().getOpposite()
    HorizontalFacing,

    //facing is the opposite of the player's nearest looking direction, maps to context.getNearestLookingDirection().getOpposite()
    SixFacing,

    //facing is the player's nearest looking direction directly; vanilla writes the observer as a double negation, maps to getNearestLookingDirection().getOpposite().getOpposite()
    LookingFacing,

    //axis is the axis of the clicked face, maps to context.getClickedFace().getAxis()
    PillarAxis,
}

internal static class PlacementKindExtensions
{
    //ParsePlacementKind parses the category name given by the place= section of the embedded block table
    public static PlacementKind ParsePlacementKind(string? text) => text switch
    {
        "horizontal" => PlacementKind.HorizontalFacing,
        "directional" => PlacementKind.SixFacing,
        "looking" => PlacementKind.LookingFacing,
        "pillar" => PlacementKind.PillarAxis,
        _ => PlacementKind.None,
    };
}
