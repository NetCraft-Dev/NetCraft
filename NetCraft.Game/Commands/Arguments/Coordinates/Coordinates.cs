using NetCraft.Game.Commands;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands.Arguments;

//Coordinates coordinate argument parse result interface, maps to vanilla net.minecraft.commands.arguments.coordinates.Coordinates
//Resolves the absolute coordinate or rotation from the executor's current position; relative flags feed the Relative set
public interface Coordinates
{
    //GetPosition resolves the absolute coordinate relative to the executor's position
    Vec3 GetPosition(ServerCommandSource source);

    //GetRotation resolves the absolute rotation relative to the executor's facing, returns (yaw,pitch)
    (float Yaw, float Pitch) GetRotation(ServerCommandSource source);

    bool IsXRelative { get; }
    bool IsYRelative { get; }
    bool IsZRelative { get; }
}
