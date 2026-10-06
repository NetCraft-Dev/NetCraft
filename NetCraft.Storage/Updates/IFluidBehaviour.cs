using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Storage;

//IFluidBehaviour 流体里需要关卡的那部分行为 对应原版 Fluid 上带 Level 参数的那些方法
//原版这些直接挂在 Fluid 上 本作 Fluid 在注册表层不能反向依赖存档层 所以拆成接口由 Game 层的流体实现
public interface IFluidBehaviour
{
    //IsRandomlyTicking 是否参与随机刻 只有岩浆要 对应原版 isRandomlyTicking
    bool IsRandomlyTicking { get; }

    //Tick 流体计划刻 对应原版 Fluid.tick
    void Tick(ServerLevel level, BlockPos pos, BlockState state, FluidState fluidState);

    //RandomTick 流体随机刻 对应原版 Fluid.randomTick
    void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random);

    //GetTickDelay 两次流动之间隔多少刻 对应原版 getTickDelay
    int GetTickDelay(ServerLevel level);

    //GetHeight 该格流体的实际液面高度 上方压着同族流体时算满格 对应原版 getHeight
    float GetHeight(FluidState state, ServerLevel level, BlockPos pos);

    //GetFlow 该格流体的流向 对应原版 getFlow
    Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state);

    //CanBeReplacedWith 该格流体能不能被另一种流体顶掉 对应原版 canBeReplacedWith
    bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction);
}
