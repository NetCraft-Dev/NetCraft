using System.Numerics;
using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.Model;

//BlockModelBaker 模型烘焙器对标原版 ModelBakery 烘焙阶段
//把 UnbakedModel + ITextureAtlas 烘焙成 BakedModel
//遍历 elements 的 faces 解析纹理变量→sprite→图集 UV 生成 BakedQuad
//顶点位置由 element from/to 计算 UV 由 face.uv 归一化后用 sprite.MapU/MapV 映射
//首版所有 quad 放 Solid layer 后续按 renderType 分 Cutout/Translucent
//依赖 ITextureAtlas 接口不耦合 GpuDevice 测试可用 stub
public sealed class BlockModelBaker
{
    private readonly ITextureAtlas _atlas;

    public BlockModelBaker(ITextureAtlas atlas)
    {
        _atlas = atlas;
    }

    //Bake 烘焙 UnbakedModel 为 BakedModel
    //model 必须已 ResolveParent（elements/textures 已合并）
    public BakedModel Bake(UnbakedModel model) => Bake(model, 0, 0);

    //Bake 带变体旋转的烘焙
    //rotationX/rotationY 是 blockstates 变体的 x/y 绕方块中心旋转 对应原版 Variant 的模型变换
    //不带上它们贴面方块只会渲染出模型文件的基准朝向 拉杆按钮火把四处都朝同一个方向
    public BakedModel Bake(UnbakedModel model, int rotationX, int rotationY)
    {
        var baked = new BakedModel();
        foreach (var element in model.Elements)
        {
            foreach (var face in element.Faces)
            {
                var quad = BakeFace(element, face, model, rotationX, rotationY);
                if (quad.HasValue)
                    baked.AddQuad(RenderLayer.Solid, quad.Value, RotateFace(face.Cullface, rotationX, rotationY));
            }
        }
        return baked;
    }

    //BakeFace 烘焙单个面为 BakedQuad
    //解析纹理变量得到 sprite name 查图集 sprite 计算图集 UV
    //顶点位置由 from/to 按 Direction 计算 4 个角顶点
    //返回 null 表示纹理未找到（sprite 不在图集中）
    private BakedQuad? BakeFace(ModelElement element, ModelFace face, UnbakedModel model,
        int rotationX, int rotationY)
    {
        var textureName = BlockModelLoader.ResolveTexture(model, face.Texture);
        var sprite = _atlas.GetSprite(textureName);
        if (sprite is null) return null;
        //face UV 是 [u0,v0,u1,v1] 像素坐标 0-16 范围归一化到 [0,1] 再映射图集
        var u0 = face.UV.X / 16f;
        var v0 = face.UV.Y / 16f;
        var u1 = face.UV.Z / 16f;
        var w1 = face.UV.W / 16f;
        //图集 UV 映射
        var auv0 = new Vector2(sprite.MapU(u0), sprite.MapV(v0));
        var auv1 = new Vector2(sprite.MapU(u1), sprite.MapV(v0));
        var auv2 = new Vector2(sprite.MapU(u1), sprite.MapV(w1));
        var auv3 = new Vector2(sprite.MapU(u0), sprite.MapV(w1));
        //顶点位置按 Direction 从 from/to 计算 再套上变体旋转
        var (p0, p1, p2, p3) = ComputeFaceVertices(element.From, element.To, face.Direction);
        p0 = RotateVertex(p0, rotationX, rotationY);
        p1 = RotateVertex(p1, rotationX, rotationY);
        p2 = RotateVertex(p2, rotationX, rotationY);
        p3 = RotateVertex(p3, rotationX, rotationY);
        //UV 跟着顶点索引走 等价于原版默认的 uvlock=false 纹理随面一起转
        var direction = RotateDirection(face.Direction, rotationX, rotationY);
        return new BakedQuad(p0, p1, p2, p3, auv0, auv1, auv2, auv3, direction, face.TintIndex);
    }

    //中心 变体旋转绕方块中心转 对应原版 Transformation 以方块几何中心为轴心
    private static readonly Vector3 Center = new(8f, 8f, 8f);

    //RotateVertex 按变体 x/y 绕方块中心旋转一个顶点
    private static Vector3 RotateVertex(Vector3 position, int rotationX, int rotationY)
        => Center + RotateVector(position - Center, rotationX, rotationY);

    //RotateVector 按变体 x/y 旋转一个方向向量
    //原版 x 90 把 UP 转成 NORTH y 90 把 NORTH 转成 EAST 两者都是绕轴负 90 度
    //顺序是先 x 后 y 对应原版 OctahedralGroup 的矩阵组合
    private static Vector3 RotateVector(Vector3 vector, int rotationX, int rotationY)
    {
        if (rotationX != 0)
            vector = Vector3.Transform(vector, Quaternion.CreateFromAxisAngle(Vector3.UnitX,
                -MathF.PI / 2f * (rotationX / 90f)));
        if (rotationY != 0)
            vector = Vector3.Transform(vector, Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                -MathF.PI / 2f * (rotationY / 90f)));
        return vector;
    }

    //RotateFace 旋转面的朝向 用于 cullface 面剔除 空值表示不剔除保持原样
    private static Direction? RotateFace(Direction? face, int rotationX, int rotationY)
        => face is null ? null : RotateDirection(face.Value, rotationX, rotationY);

    //RotateDirection 旋转一个轴向 旋转量是 90 度的整数倍 结果必定落在六个轴上
    private static Direction RotateDirection(Direction direction, int rotationX, int rotationY)
        => rotationX == 0 && rotationY == 0
            ? direction
            : FromUnitVector(RotateVector(direction.UnitVector(), rotationX, rotationY));

    //FromUnitVector 把单位轴向量折回方向枚举
    private static Direction FromUnitVector(Vector3 vector)
    {
        if (vector.Y > 0.5f) return Direction.Up;
        if (vector.Y < -0.5f) return Direction.Down;
        if (vector.X > 0.5f) return Direction.East;
        if (vector.X < -0.5f) return Direction.West;
        return vector.Z > 0.5f ? Direction.South : Direction.North;
    }

    //ComputeFaceVertices 按 Direction 计算 4 顶点位置
    //返回 CCW 顺序（从面外侧看）顶点坐标 0-16 范围
    //chunk mesh 生成时再归一化到 0-1 世界坐标
    private static (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) ComputeFaceVertices(
        Vector3 from, Vector3 to, Direction dir)
    {
        var x0 = from.X;
        var y0 = from.Y;
        var z0 = from.Z;
        var x1 = to.X;
        var y1 = to.Y;
        var z1 = to.Z;
        return dir switch
        {
            //Down 朝下从外侧看（从 -Y 方向看上来）CCW 顺序
            Direction.Down => (new(x0, y0, z1), new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1)),
            //Up 朝上从外侧看（从 +Y 方向看下来）CCW 顺序
            Direction.Up => (new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0)),
            //North 朝 -Z CCW 顺序
            Direction.North => (new(x0, y1, z0), new(x0, y0, z0), new(x1, y0, z0), new(x1, y1, z0)),
            //South 朝 +Z CCW 顺序
            Direction.South => (new(x1, y1, z1), new(x1, y0, z1), new(x0, y0, z1), new(x0, y1, z1)),
            //West 朝 -X CCW 顺序
            Direction.West => (new(x0, y1, z1), new(x0, y0, z1), new(x0, y0, z0), new(x0, y1, z0)),
            //East 朝 +X CCW 顺序
            Direction.East => (new(x1, y1, z0), new(x1, y0, z0), new(x1, y0, z1), new(x1, y1, z1)),
            _ => throw new ArgumentOutOfRangeException(nameof(dir))
        };
    }
}
