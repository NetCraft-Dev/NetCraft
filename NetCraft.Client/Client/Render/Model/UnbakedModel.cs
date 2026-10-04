namespace NetCraft.Game.Client.Render.Model;

//UnbakedModel 未烘焙模型对标原版 UnbakedModel/BlockModel
//JSON 反序列化结果含 parent 引用、textures 变量字典、elements 几何列表
//parent 解析由 BlockModelLoader 递归合并 parent 的 elements/textures
//textures 字典 key 是变量名（如 all/down/up）value 是纹理路径（如 minecraft:block/stone）或变量引用（如 #all）
public sealed class UnbakedModel
{
    //Parent 父模型引用如 minecraft:block/cube_all null 表示无父模型
    public string? Parent { get; set; }
    //Textures 纹理变量字典 key=变量名 value=纹理路径或 #变量引用
    //烘焙时 BlockModelBaker 解析 # 引用链得到最终纹理路径
    public Dictionary<string, string> Textures { get; set; } = new();
    //Elements 几何元素列表父模型的 elements 会被继承
    public List<ModelElement> Elements { get; set; } = new();
    //是否已解析 parent 合并完父模型 elements/textures
    //BlockModelLoader.Resolve 后置 true 避免重复解析
    public bool IsResolved { get; set; }
}
