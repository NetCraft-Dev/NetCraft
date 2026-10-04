using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using GameDirection = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureBlockTransforms 方块状态的镜像与旋转 对应原版 BlockState.mirror / BlockState.rotate
//原版每个方块自己重写这两个方法 这里按属性语义统一处理方向类属性
//台阶的 shape 与多面连接类属性暂未覆盖 原样保留
public static class StructureBlockTransforms
{
    //ApplyMirror 镜像方块状态 朝向属性按镜像规则翻转 十六分之一圆的 rotation 取补
    //方法名不能叫 Mirror 否则会遮蔽同名枚举类型
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

    //ApplyRotation 旋转方块状态 朝向属性按旋转规则转向 轴属性在 90 度时互换 十六分之一圆的 rotation 加步进
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

    //SetIfAllowed 值不在属性允许集里时原样返回 避免注册表 SetValue 抛异常
    public static BlockState SetIfAllowed(BlockState state, PropertyBase property, object value)
    {
        foreach (var candidate in property.PossibleValuesAsObjects)
        {
            if (Equals(candidate, value)) return state.SetValue(property, value);
        }
        return state;
    }

    //RotateAxis 绕 Y 轴 90 度会让 X 轴与 Z 轴互换 180 度轴不变
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

    //TransformOrientation 朝向组合的正面与顶面各过一次方向变换 对应原版 OctahedralGroup.rotate(FrontAndTop)
    private static FrontAndTop TransformOrientation(FrontAndTop orientation,
        Func<GameDirection, GameDirection> transform)
        => JigsawBlock.FromFrontAndTop(transform(JigsawBlock.FrontOf(orientation)),
            transform(JigsawBlock.TopOf(orientation)));

    //IsFullCircle 属性取值个数等于整圈步数说明它是环形角度属性
    private static bool IsFullCircle(PropertyBase property, int steps)
        => property.PossibleValuesAsObjects.Count == steps;
}
