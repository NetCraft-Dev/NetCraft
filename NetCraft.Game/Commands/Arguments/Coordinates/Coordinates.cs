using NetCraft.Game.Commands;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands.Arguments;

//Coordinates 坐标参数解析结果接口对应原版 net.minecraft.commands.arguments.coordinates.Coordinates
//按执行者当前位置求值绝对坐标或朝向 相对标志供 Relative 集合推导
public interface Coordinates
{
    //GetPosition 按执行者位置为基准求绝对坐标
    Vec3 GetPosition(ServerCommandSource source);

    //GetRotation 按执行者朝向为基准求绝对朝向 返回(yaw,pitch)
    (float Yaw, float Pitch) GetRotation(ServerCommandSource source);

    bool IsXRelative { get; }
    bool IsYRelative { get; }
    bool IsZRelative { get; }
}
