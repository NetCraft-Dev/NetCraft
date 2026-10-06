using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;
using StateDirection = NetCraft.Registry.Enums.Direction;

namespace NetCraft.Game.World.Level.Block;

//Blocks observer part; same class as Blocks.cs, split into a separate file to keep the main file short
public static partial class Blocks
{
    //ObserverBlock observer, maps to vanilla ObserverBlock
    //The eye watches the FACING side; any change there emits a two-tick pulse
    //Output is on the opposite of FACING, i.e. the back the eye looks into
    public sealed class ObserverBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("observer");

        //Vanilla observer hardness 3, requires a pickaxe
        public override float DestroySpeed => 3f;
        public override bool RequiresCorrectToolForDrops => true;

        //Observers are not conductors; vanilla explicitly passes isRedstoneConductor(Blocks::never) at registration
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.FacingProperty,
            ["powered"] = BlockStateProperties.Powered,
        };

        //Vanilla observer default state FACING=south POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.FacingProperty, StateDirection.south)
                .SetValue(BlockStateProperties.Powered, false);

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal same as its own signal, maps to the vanilla override
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => GetSignal(level, pos, state, direction);

        //GetSignal outputs only toward the opposite of FACING, maps to the vanilla override
        //The querier is on the far side of the direction, same semantics as a diode
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == direction
                ? OwnSignal(level, pos, state)
                : 0;

        //GetStateForPlacement the eye faces the direction nearest the player's view, maps to the result of vanilla's two getOpposite calls canceling out
        //Vanilla writes getNearestLookingDirection().getOpposite().getOpposite() here; two negations equal itself
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty, lookingDirection.ToState());

        //Tick when powered, turns off when due; when unpowered, turns on and schedules two ticks; both notify the output side, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Powered))
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false),
                    BlockUpdateFlags.Clients);
            }
            else
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true),
                    BlockUpdateFlags.Clients);
                level.ScheduleTick(pos, this, 2);
            }
            UpdateNeighborsInFront(level, pos, state);
        }

        //UpdateShape starts a pulse when the watched face changes shape, maps to vanilla updateShape
        //The observer senses changes in the watched block through the shape update channel
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == directionToNeighbour
                && !state.GetValue(BlockStateProperties.Powered))
                StartSignal(level, pos);
            return state;
        }

        //StartSignal schedules two ticks if not already scheduled, maps to vanilla startSignal
        private void StartSignal(ServerLevel level, BlockPos pos)
        {
            if (!level.HasScheduledTick(pos, this)) level.ScheduleTick(pos, this, 2);
        }

        //OnPlace an observer placed while powered with no scheduled tick is switched off and the output side notified, maps to vanilla onPlace
        //Write back with KnownShape so this step does not trigger a shape update, preventing it from powering itself right after placement
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (!state.GetValue(BlockStateProperties.Powered) || level.HasScheduledTick(pos, this)) return;
            var cleared = state.SetValue(BlockStateProperties.Powered, false);
            level.SetBlock(pos, cleared, BlockUpdateFlags.KnownShape | BlockUpdateFlags.Clients);
            UpdateNeighborsInFront(level, pos, cleared);
        }

        //AffectNeighborsAfterRemoval when removed before the pulse finishes, the output side must also drop to zero, maps to the vanilla method of the same name
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (state.GetValue(BlockStateProperties.Powered) && level.HasScheduledTick(pos, this))
                UpdateNeighborsInFront(level, pos, state.SetValue(BlockStateProperties.Powered, false));
        }

        //UpdateNeighborsInFront notifies the output-side block and its neighbors, maps to the vanilla method of the same name
        private void UpdateNeighborsInFront(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive();
            var backPos = pos.Offset(direction.Opposite);
            level.NeighborChanged(backPos, this);
            level.UpdateNeighborsAtExceptFromFacing(backPos, this, direction);
        }
    }

    public static readonly ObserverBlock OBSERVER = new();
}
