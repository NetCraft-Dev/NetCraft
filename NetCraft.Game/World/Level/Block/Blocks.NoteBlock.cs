using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//Note block, maps to vanilla net.minecraft.world.level.block.NoteBlock
//The instrument comes from the block below, a mob head above switches to the head instrument; powered plays a note, right click raises the pitch and left click previews
//Sound and note particles are driven by block events; after the server computes the event the client runs triggerEvent itself
public static partial class Blocks
{
    public static readonly NoteBlock NOTE_BLOCK = new();

    //RegisterNoteBlock registers the note block into the real block table
    private static void RegisterNoteBlock(Dictionary<string, BlockBehaviour> real)
        => real[NOTE_BLOCK.Id.Path] = NOTE_BLOCK;

    public sealed class NoteBlock : BlockBehaviour
    {
        //NoteVolume vanilla fixed volume, maps to NOTE_VOLUME
        private const float NoteVolume = 3f;

        public override Identifier Id => Identifier.WithDefaultNamespace("note_block");

        //Vanilla note block hardness 0.8
        public override float DestroySpeed => 0.8f;

        //Properties states follow instrument|note|powered in blocks.txt
        //The one in the table is a separately built property instance that GetValue cannot find; it must be declared here, and changing the order shifts the global state ids
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["instrument"] = BlockStateProperties.NoteBlockInstrumentProperty,
            ["note"] = BlockStateProperties.Note,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetStateForPlacement the instrument is decided by the blocks above and below at placement, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
            => SetInstrument(level, pos, DefaultBlockState);

        //UpdateShape a vertical neighbor change re-decides the instrument, maps to vanilla updateShape which only acts on the Y axis
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
            => directionToNeighbour.AxisValue == Direction.Axis.Y ? SetInstrument(level, pos, state) : state;

        //NeighborChanged plays the note before writing the state when the powered state flips, maps to vanilla neighborChanged
        //flags 3 notifies neighbors and the client at once; the client also emits its own particles on the block event
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var signal = level.HasNeighborSignal(pos);
            if (signal == state.GetValue(BlockStateProperties.Powered)) return;
            if (signal) PlayNote(level, pos, state);
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //UseOn right click raises one note, maps to vanilla useWithoutItem, the pitch wraps on overflow
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            var newState = state.Cycle(BlockStateProperties.Note);
            level.SetBlock(pos, newState, BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            PlayNote(level, pos, newState);
            return true;
        }

        //OnAttack left click previews once, maps to vanilla attack
        public override void OnAttack(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state)
            => PlayNote(level, pos, state);

        //TriggerEvent plays the note, maps to vanilla triggerEvent
        //Base instruments compute the pitch from note, mob heads have a fixed pitch and custom heads read the sound from the head above; this project has no head block entity so it is discarded
        public override bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB)
        {
            var instrument = state.GetValue(BlockStateProperties.NoteBlockInstrumentProperty);
            if (instrument.HasCustomSound()) return false;
            var pitch = instrument.IsTunable() ? PitchFromNote(state.GetValue(BlockStateProperties.Note)) : 1f;
            level.PlaySound(instrument.GetSoundEvent(), SoundSource.Records, pos, NoteVolume, pitch);
            return true;
        }

        //PitchFromNote pitch conversion, doubles every twelve semitones, maps to vanilla getPitchFromNote
        public static float PitchFromNote(int note) => (float)Math.Pow(2.0, (note - 12) / 12.0);

        //PlayNote fires the sound event, maps to vanilla playNote
        //Mob heads and the like check whether a block blocks the top; only air above lets it sound
        private static void PlayNote(ServerLevel level, BlockPos pos, BlockState state)
        {
            var instrument = state.GetValue(BlockStateProperties.NoteBlockInstrumentProperty);
            var above = level.GetBlockState(pos.Offset(Direction.Up));
            if (!instrument.WorksAboveNoteBlock() && above is { Owner.IsAir: false }) return;
            level.BlockEvent(pos, NOTE_BLOCK, 0, 0);
        }

        //SetInstrument uses the block above when it is an instrument, otherwise the block below; a head-like instrument below falls back to the harp
        //Matches vanilla setInstrument, air's instrument is the harp and a missing block also counts as the harp
        private static BlockState SetInstrument(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (InstrumentOf(level.GetBlockState(pos.Offset(Direction.Up))) is { } above
                && above.WorksAboveNoteBlock())
                return state.SetValue(BlockStateProperties.NoteBlockInstrumentProperty, above);
            var below = InstrumentOf(level.GetBlockState(pos.Offset(Direction.Down))) ?? NoteBlockInstrument.harp;
            var instrument = below.WorksAboveNoteBlock() ? NoteBlockInstrument.harp : below;
            return state.SetValue(BlockStateProperties.NoteBlockInstrumentProperty, instrument);
        }

        //InstrumentOf returns the instrument a block gives as a base, air and unimplemented blocks count as the harp
        private static NoteBlockInstrument? InstrumentOf(BlockState? state)
            => state?.Owner is BlockBehaviour behaviour ? behaviour.Instrument : null;
    }
}
