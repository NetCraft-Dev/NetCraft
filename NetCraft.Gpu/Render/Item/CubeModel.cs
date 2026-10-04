using System.Numerics;

namespace NetCraft.Gpu;

//CubeModel 程序化立方体模型对标原版 ModelPart.Cube.compile
//生成 6 面 BakedQuad 供 ItemRenderer 渲染 PoC 不加载 JSON 模型用程序化几何
//每面 4 顶点 + UV + 面法线(Directon) 顶点顺序逆时针正面朝外
public static class CubeModel
{
    //Create 生成边长 size 的立方体 BakedQuad 列表中心在原点
    //UV 按 0..1 映射每面 PoC 不做纹理图集分块
    public static List<BakedQuad> Create(float size)
    {
        var s = size / 2f;
        var quads = new List<BakedQuad>(6);
        //Down -Y
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, s), new Vector3(s, -s, s),
            new Vector3(s, -s, -s), new Vector3(-s, -s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.Down));
        //Up +Y
        quads.Add(new BakedQuad(
            new Vector3(-s, s, -s), new Vector3(s, s, -s),
            new Vector3(s, s, s), new Vector3(-s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.Up));
        //North -Z
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, -s), new Vector3(s, -s, -s),
            new Vector3(s, s, -s), new Vector3(-s, s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.North));
        //South +Z
        quads.Add(new BakedQuad(
            new Vector3(s, -s, s), new Vector3(-s, -s, s),
            new Vector3(-s, s, s), new Vector3(s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.South));
        //West -X
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, -s), new Vector3(-s, -s, s),
            new Vector3(-s, s, s), new Vector3(-s, s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.West));
        //East +X
        quads.Add(new BakedQuad(
            new Vector3(s, -s, s), new Vector3(s, -s, -s),
            new Vector3(s, s, -s), new Vector3(s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.East));
        return quads;
    }
}
