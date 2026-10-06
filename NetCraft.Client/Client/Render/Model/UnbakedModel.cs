namespace NetCraft.Game.Client.Render.Model;

//UnbakedModel unbaked model, maps to vanilla UnbakedModel/BlockModel
//JSON deserialization result containing the parent reference, textures variable dictionary, and elements geometry list
//parent resolution: BlockModelLoader recursively merges the parent's elements/textures
//textures dictionary: key is the variable name (such as all/down/up), value is the texture path (such as minecraft:block/stone) or a variable reference (such as #all)
public sealed class UnbakedModel
{
    //Parent parent model reference such as minecraft:block/cube_all; null means no parent model
    public string? Parent { get; set; }
    //Textures texture variable dictionary: key=variable name, value=texture path or #variable reference
    //At bake time BlockModelBaker resolves the # reference chain to get the final texture path
    public Dictionary<string, string> Textures { get; set; } = new();
    //Elements geometry element list; the parent model's elements are inherited
    public List<ModelElement> Elements { get; set; } = new();
    //Whether it has been resolved (parent merged, parent's elements/textures combined)
    //Set to true after BlockModelLoader.Resolve to avoid re-resolution
    public bool IsResolved { get; set; }
}
