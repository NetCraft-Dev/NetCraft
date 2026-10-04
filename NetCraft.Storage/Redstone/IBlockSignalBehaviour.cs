using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Redstone;

//方块侧信号契约 对应原版 BlockBehaviour 里那几个 protected 信号方法
//放 Storage 层是为了让关卡不必反向依赖 Game 层的方块实现
public interface IBlockSignalBehaviour
{
    //IsSignalSource 是否信号源 对应原版 isSignalSource
    bool IsSignalSource { get; }

    //IsDiode 是否二极管也就是中继器与比较器 对应原版 DiodeBlock.isDiode
    //中继器侧向锁定与比较器侧输入都要按它筛
    bool IsDiode { get; }

    //HasAnalogOutputSignal 是否有模拟输出 比较器要读它 对应原版 hasAnalogOutputSignal
    bool HasAnalogOutputSignal { get; }

    //OwnSignal 方块自身的信号强度 对应原版 ownSignal
    int OwnSignal(ServerLevel level, BlockPos pos, BlockState state);

    //GetSignal 方块对指定方向输出的信号强度 默认取自身强度 对应原版 getSignal
    int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);

    //GetDirectSignal 直接信号 导体方块传导的就是它 对应原版 getDirectSignal
    int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);

    //IsRedstoneConductor 是否红石导体 是的话邻居的直接信号会被并进本位置的信号里
    bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state);

    //GetAnalogOutputSignal 模拟输出强度 供比较器读取
    int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);
}
