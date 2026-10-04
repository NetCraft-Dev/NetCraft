using NetCraft.Primitives;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//Rotation 绕 Y 轴的旋转 对应原版 net.minecraft.world.level.block.Rotation
//四个值分别对应 0/90/180/270 度 结构模板整体旋转按它走
public enum Rotation
{
    None,
    Clockwise90,
    Clockwise180,
    Counterclockwise90,
}

//Mirror 沿水平轴的镜像 对应原版 net.minecraft.world.level.block.Mirror
//LeftRight 沿 X 轴左右翻转 东西互换 FrontBack 沿 Z 轴前后翻转 南北互换
public enum Mirror
{
    None,
    LeftRight,
    FrontBack,
}

//StructureTransforms 旋转与镜像的名字映射与朝向换算
//变换恒为「先镜像后旋转」 顺序颠倒会让结构朝向与原版对不上
public static class StructureTransforms
{
    //Name 取 JSON 名 180 度的序列化名就是 "180" 不是 clockwise_180
    public static string Name(this Rotation rotation) => rotation switch
    {
        Rotation.Clockwise90 => "clockwise_90",
        Rotation.Clockwise180 => "180",
        Rotation.Counterclockwise90 => "counterclockwise_90",
        _ => "none",
    };

    //Name 取 JSON 名
    public static string Name(this Mirror mirror) => mirror switch
    {
        Mirror.LeftRight => "left_right",
        Mirror.FrontBack => "front_back",
        _ => "none",
    };

    //TryParse 按 JSON 名解析旋转 非法返回 null
    //另外接受枚举名形态供旧数据与结构方块 NBT 使用
    public static Rotation? TryParseRotation(string name) => name switch
    {
        "none" => Rotation.None,
        "clockwise_90" => Rotation.Clockwise90,
        "180" or "clockwise_180" or "CLOCKWISE_180" => Rotation.Clockwise180,
        "counterclockwise_90" => Rotation.Counterclockwise90,
        _ => null,
    };

    //TryParse 按 JSON 名解析镜像 非法返回 null
    public static Mirror? TryParseMirror(string name) => name switch
    {
        "none" => Mirror.None,
        "left_right" => Mirror.LeftRight,
        "front_back" => Mirror.FrontBack,
        _ => null,
    };

    //GetRandomRotation 四个旋转里随机取一个 对应原版 Rotation.getRandom
    public static Rotation GetRandomRotation(RandomSource random) => (Rotation)random.NextInt(4);

    //GetShuffledRotations 四个旋转的洗牌副本 对应原版 Rotation.getShuffled
    //拼图装配对每个候选元素逐个试旋转 试的顺序也由随机源决定
    public static Rotation[] GetShuffledRotations(RandomSource random)
    {
        var rotations = new[]
        {
            Rotation.None,
            Rotation.Clockwise90,
            Rotation.Clockwise180,
            Rotation.Counterclockwise90,
        };
        RandomCollections.Shuffle(rotations, random);
        return rotations;
    }

    //Rotate 旋转一个世界朝向 对应原版 Rotation.rotate
    //竖直方向不参与旋转
    public static Direction Rotate(this Rotation rotation, Direction direction)
    {
        if (direction.GetAxis() == Direction.Axis.Y) return direction;
        return rotation switch
        {
            Rotation.Clockwise90 => direction.ClockWise,
            Rotation.Clockwise180 => direction.Opposite,
            Rotation.Counterclockwise90 => direction.CounterClockWise,
            _ => direction,
        };
    }

    //MirrorDirection 镜像一个世界朝向 对应原版 Mirror.mirror
    //FrontBack 是 INVERT_X 只翻转东西 LeftRight 是 INVERT_Z 只翻转南北
    public static Direction MirrorDirection(this Mirror mirror, Direction direction)
    {
        if (mirror == Mirror.FrontBack && direction.GetAxis() == Direction.Axis.X) return direction.Opposite;
        if (mirror == Mirror.LeftRight && direction.GetAxis() == Direction.Axis.Z) return direction.Opposite;
        return direction;
    }

    //GetRotation 镜像等效于哪个旋转 对应原版 Mirror.getRotation
    //沿 Z 镜像遇到南北朝向 与直接转 180 度等效 沿 X 镜像遇到东西朝向同理
    public static Rotation GetRotation(this Mirror mirror, Direction direction)
    {
        var axis = direction.GetAxis();
        return (mirror == Mirror.LeftRight && axis == Direction.Axis.Z)
            || (mirror == Mirror.FrontBack && axis == Direction.Axis.X)
            ? Rotation.Clockwise180
            : Rotation.None;
    }

    //RotateSteps 把 0..steps 的步进按旋转折算 对应原版 Rotation.rotate(int,int)
    //steps 是整圈步数 朝向属性是 4 十六分之一圆属性是 16
    public static int RotateSteps(this Rotation rotation, int current, int steps)
    {
        var total = rotation switch
        {
            Rotation.Clockwise90 => current + steps / 4,
            Rotation.Clockwise180 => current + steps / 2,
            Rotation.Counterclockwise90 => current + steps * 3 / 4,
            _ => current,
        };
        return Mod(total, steps);
    }

    //MirrorSteps 把 0..steps 的步进按镜像折算 对应原版 Mirror.mirror(int,int)
    //先把超过半圈的步进折回 再按镜像方向取补
    public static int MirrorSteps(this Mirror mirror, int current, int steps)
    {
        var halfSteps = steps / 2;
        var corrected = current > halfSteps ? current - steps : current;
        var result = mirror switch
        {
            Mirror.FrontBack => steps - corrected,
            Mirror.LeftRight => halfSteps - corrected + steps,
            _ => current,
        };
        return Mod(result, steps);
    }

    //Mod 取非负余数 Java 的 % 会返回负数 这里必须归一到 0..m-1
    public static int Mod(int value, int m)
    {
        var r = value % m;
        return r < 0 ? r + m : r;
    }
}
