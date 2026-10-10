using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;

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
                state = SetIfAllowed(state, entry.Property, MirrorOrientation(orientation, mirror));
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
                state = SetIfAllowed(state, entry.Property, RotateOrientation(orientation, rotation));
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
    //Indexed rather than foreach over PossibleValuesAsObjects: that is typed IReadOnlyList<object>, so enumerating it goes
    //through IEnumerable<object> and allocates an array enumerator on every call
    //This form stays for the callers that already hold a boxed value taken straight out of PropertyValue.Value
    public static BlockState SetIfAllowed(BlockState state, PropertyBase property, object value)
    {
        var candidates = property.PossibleValuesAsObjects;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (Equals(candidates[i], value)) return state.SetValue(property, value);
        }
        return state;
    }

    //SetIfAllowed typed form, taken by the rotation and mirror path where the value was just computed and its type is known
    //Membership is tested through the typed property, so the value never has to be boxed just to be compared, and the
    //lookup also replaces the equals walk over the boxed candidate list
    public static BlockState SetIfAllowed<T>(BlockState state, PropertyBase property, T value)
        where T : IComparable
        => property is Property<T> typed && typed.GetInternalIndex(value) >= 0
            ? state.SetValue(typed, value)
            : state;

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

    //RotateOrientation turns the front and top of an orientation, maps to vanilla OctahedralGroup.rotate(FrontAndTop)
    //Two concrete forms instead of one taking a Func: the Func form made every call site allocate a closure and a delegate
    private static FrontAndTop RotateOrientation(FrontAndTop orientation, Rotation rotation)
        => JigsawBlock.FromFrontAndTop(rotation.Rotate(JigsawBlock.FrontOf(orientation)),
            rotation.Rotate(JigsawBlock.TopOf(orientation)));

    //MirrorOrientation mirrors the front and top of an orientation, maps to vanilla OctahedralGroup.mirror(FrontAndTop)
    private static FrontAndTop MirrorOrientation(FrontAndTop orientation, Mirror mirror)
        => JigsawBlock.FromFrontAndTop(mirror.MirrorDirection(JigsawBlock.FrontOf(orientation)),
            mirror.MirrorDirection(JigsawBlock.TopOf(orientation)));

    //IsFullCircle when the number of possible values equals the full-circle steps, the property is a circular angle property
    private static bool IsFullCircle(PropertyBase property, int steps)
        => property.PossibleValuesAsObjects.Count == steps;
}
