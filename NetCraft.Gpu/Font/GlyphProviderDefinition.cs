namespace NetCraft.Gpu.Font;

//IFontResourceAccessor 字体资源访问抽象替代原版 ResourceManager
//Loader 加载 provider 时通过此接口读取 PNG/TTF/hex 资源
//F4 的 FontManager 提供从 assets 目录读取的实现
public interface IFontResourceAccessor
{
    //OpenResource 按 namespace:path 打开资源流无则 null
    //如 minecraft:font/ascii.png → assets/minecraft/textures/font/ascii.png
    Stream? OpenResource(string identifier);
}

//IGlyphProviderDefinition 字形提供器定义对标原版 GlyphProviderDefinition
//font.json 的 providers[] 解析为 definition+filter(Conditional)
//definition 要么是 Loader(直接加载) 要么是 Reference(引用其他 provider)
public interface IGlyphProviderDefinition
{
    GlyphProviderType Type { get; }

    //IsReference 区分 Loader 和 Reference 原版用 Either<Loader,Reference>
    //true 时 AsReference 返回引用 false 时 AsLoader 返回加载器
    bool IsReference { get; }
    IGlyphProviderDefinition.ILoader? AsLoader => null;
    IGlyphProviderDefinition.Reference? AsReference => null;

    //ILoader 加载器加载 provider 需要资源访问失败返回 null
    public interface ILoader
    {
        IGlyphProvider? Load(IFontResourceAccessor resources);
    }

    //Reference 引用其他 provider 按 Identifier 查找对标原版 GlyphProviderDefinition.Reference
    public sealed class Reference
    {
        public string Id { get; }
        public Reference(string id) => Id = id;
    }

    //Conditional 条件 definition 持有 definition+filter 解析阶段产物
    //对标原版 GlyphProviderDefinition.Conditional
    public sealed class Conditional
    {
        public IGlyphProviderDefinition Definition { get; }
        public FontOptionFilter Filter { get; }

        public Conditional(IGlyphProviderDefinition definition, FontOptionFilter filter)
        {
            Definition = definition;
            Filter = filter;
        }
    }
}
