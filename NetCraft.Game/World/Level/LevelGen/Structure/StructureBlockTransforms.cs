using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using GameDirection = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureBlockTransforms block state mirroring and rotation, maps to vanilla BlockState.mirror / BlockState.rotate
//Vanilla overrides these two methods per block; here direction-like properties are handled uniformly by property semantics
//Stair shape and multi-face connection properties are not covered yet and are kept as is
public static class StructureBlockTransforms
{
    //ApplyMirror mirrors a block state; facing properties flip by the mirror rule, and sixteenth-circle rotation is complemented
    //The method cannot be named Mirror or it would shadow the enum type of the same name
    public static BlockState ApplyMirror(BlockState state, Mirror mirror)
    {
        if (mirror == Mirror.None) return state;
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name == "facing" && entry.Value is Direction facing)
            {
                state = SetIfAllowed(state, entry.Property, mirror.MirrorDirection(facing.ToPrimitive()).ToState());
            }
            else if (entry.Property.Name == "orientation" && entry.Value is FrontAndTop orientation)
            {
                state = SetIfAllowed(state, entry.Property,
                    TransformOrientation(orientation, direction => mirror.MirrorDirection(direction)));
            }
            else if (entry.Property.Name == "rotation" && entry.Value is int steps && IsFullCircle(entry.Property, 16))
            {
                state = SetIfAllowed(state, entry.Property, mirror.MirrorSteps(steps, 16));
            }
        }
        return state;
    }

    //ApplyRotation rotates a block state; facing properties turn by the rotation rule, axes swap at 90 degrees, and sixteenth-circle rotation advances by steps
    public static BlockState ApplyRotation(BlockState state, Rotation rotation)
    {
        if (rotation == Rotation.None) return state;
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name == "facing" && entry.Value is Direction facing)
            {
                state = SetIfAllowed(state, entry.Property, rotation.Rotate(facing.ToPrimitive()).ToState());
            }
            else if (entry.Property.Name == "orientation" && entry.Value is FrontAndTop orientation)
            {
                state = SetIfAllowed(state, entry.Property,
                    TransformOrientation(orientation, direction => rotation.Rotate(direction)));
            }
            else if (entry.Property.Name == "axis" && entry.Value is Axis axis)
            {
                state = SetIfAllowed(state, entry.Property, RotateAxis(axis, rotation));
            }
            else if (entry.Property.Name == "rotation" && entry.Value is int steps && IsFullCircle(entry.Property, 16))
            {
                state = SetIfAllowed(state, entry.Property, rotation.RotateSteps(steps, 16));
            }
        }
        return state;
    }

    //SetIfAllowed returns the state unchanged when the value is not in the property's allowed set, avoiding a throw from registry SetValue
    public static BlockState SetIfAllowed(BlockState state, PropertyBase property, object value)
    {
        foreach (var candidate in property.PossibleValuesAsObjects)
        {
            if (Equals(candidate, value)) return state.SetValue(property, value);
        }
        return state;
    }

    //RotateAxis a 90-degree turn around Y swaps the X and Z axes; 180 degrees leaves the axis unchanged
    private static Axis RotateAxis(Axis axis, Rotation rotation) => rotation switch
    {
        Rotation.Clockwise90 or Rotation.Counterclockwise90 => axis switch
        {
            Axis.x => Axis.z,
            Axis.z => Axis.x,
            _ => axis,
        },
        _ => axis,
    };

    //TransformOrientation applies the direction transform to the front and top of an orientation, maps to vanilla OctahedralGroup.rotate(FrontAndTop)
    private static FrontAndTop TransformOrientation(FrontAndTop orientation,
        Func<GameDirection, GameDirection> transform)
        => JigsawBlock.FromFrontAndTop(transform(JigsawBlock.FrontOf(orientation)),
            transform(JigsawBlock.TopOf(orientation)));

    //IsFullCircle when the number of possible values equals the full-circle steps, the property is a circular angle property
    private static bool IsFullCircle(PropertyBase property, int steps)
        => property.PossibleValuesAsObjects.Count == steps;
}
