using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//音符盒 对应原版 net.minecraft.world.level.block.NoteBlock
//音色按下方方块决定 上方放生物头则改用头颅音色 通电发声 右键升调 左键试听
//发声与音符粒子都由方块事件驱动 服务端算出事件后客户端自己再跑一遍 triggerEvent
public static partial class Blocks
{
    public static readonly NoteBlock NOTE_BLOCK = new();

    //RegisterNoteBlock 音符盒登记进真实方块表
    private static void RegisterNoteBlock(Dictionary<string, BlockBehaviour> real)
        => real[NOTE_BLOCK.Id.Path] = NOTE_BLOCK;

    public sealed class NoteBlock : BlockBehaviour
    {
        //NoteVolume 原版固定音量 对应 NOTE_VOLUME
        private const float NoteVolume = 3f;

        public override Identifier Id => Identifier.WithDefaultNamespace("note_block");

        //原版音符盒硬度 0.8
        public override float DestroySpeed => 0.8f;

        //Properties 状态按 blocks.txt 的 instrument|note|powered 走
        //表里那份是另建的属性实例 GetValue 取不到 必须自己声明 顺序变了全局状态 id 会错位
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["instrument"] = BlockStateProperties.NoteBlockInstrumentProperty,
            ["note"] = BlockStateProperties.Note,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetStateForPlacement 落位时按上下方块定音色 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
            => SetInstrument(level, pos, DefaultBlockState);

        //UpdateShape 上下邻居变了要重定音色 对应原版 updateShape 只在 Y 轴上动作
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
            => directionToNeighbour.AxisValue == Direction.Axis.Y ? SetInstrument(level, pos, state) : state;

        //NeighborChanged 通电态翻转时先发声再写状态 对应原版 neighborChanged
        //flags 3 同时通知邻居与客户端 客户端收到方块事件也会自己出一遍粒子
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var signal = level.HasNeighborSignal(pos);
            if (signal == state.GetValue(BlockStateProperties.Powered)) return;
            if (signal) PlayNote(level, pos, state);
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //UseOn 右键升一个音 对应原版 useWithoutItem 音高溢出时自己回绕
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            var newState = state.Cycle(BlockStateProperties.Note);
            level.SetBlock(pos, newState, BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            PlayNote(level, pos, newState);
            return true;
        }

        //OnAttack 左键敲一下试听 对应原版 attack
        public override void OnAttack(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state)
            => PlayNote(level, pos, state);

        //TriggerEvent 发声 对应原版 triggerEvent
        //基础乐器按 note 算音高 生物头固定音高 自定义头要读上方头颅的音效 本作没有头颅方块实体直接作废
        public override bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB)
        {
            var instrument = state.GetValue(BlockStateProperties.NoteBlockInstrumentProperty);
            if (instrument.HasCustomSound()) return false;
            var pitch = instrument.IsTunable() ? PitchFromNote(state.GetValue(BlockStateProperties.Note)) : 1f;
            level.PlaySound(instrument.GetSoundEvent(), SoundSource.Records, pos, NoteVolume, pitch);
            return true;
        }

        //PitchFromNote 音高换算 每十二个半音翻一倍 对应原版 getPitchFromNote
        public static float PitchFromNote(int note) => (float)Math.Pow(2.0, (note - 12) / 12.0);

        //PlayNote 触发发声事件 对应原版 playNote
        //生物头这类要看上方是否有方块挡住 上方是空气才发得出来
        private static void PlayNote(ServerLevel level, BlockPos pos, BlockState state)
        {
            var instrument = state.GetValue(BlockStateProperties.NoteBlockInstrumentProperty);
            var above = level.GetBlockState(pos.Offset(Direction.Up));
            if (!instrument.WorksAboveNoteBlock() && above is { Owner.IsAir: false }) return;
            level.BlockEvent(pos, NOTE_BLOCK, 0, 0);
        }

        //SetInstrument 上方是乐器就用上方 否则看下方 下方的乐器若是头颅那一类则退成竖琴
        //与原版 setInstrument 一致 空气的乐器是竖琴 取不到方块时也按竖琴处理
        private static BlockState SetInstrument(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (InstrumentOf(level.GetBlockState(pos.Offset(Direction.Up))) is { } above
                && above.WorksAboveNoteBlock())
                return state.SetValue(BlockStateProperties.NoteBlockInstrumentProperty, above);
            var below = InstrumentOf(level.GetBlockState(pos.Offset(Direction.Down))) ?? NoteBlockInstrument.harp;
            var instrument = below.WorksAboveNoteBlock() ? NoteBlockInstrument.harp : below;
            return state.SetValue(BlockStateProperties.NoteBlockInstrumentProperty, instrument);
        }

        //InstrumentOf 取方块作为底座时给出的乐器 空气与未实现方块按竖琴
        private static NoteBlockInstrument? InstrumentOf(BlockState? state)
            => state?.Owner is BlockBehaviour behaviour ? behaviour.Instrument : null;
    }
}
