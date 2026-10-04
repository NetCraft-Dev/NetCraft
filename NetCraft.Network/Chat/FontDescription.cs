namespace NetCraft.Network.Chat;

using NetCraft.Registry;

//字体描述对应原版net.minecraft.network.chat.FontDescription
//描述文本渲染时使用的字体资源或图集精灵
public interface FontDescription
{
    //默认字体对应原版DEFAULT
    public static readonly Resource Default = new(Identifier.WithDefaultNamespace("default"));

    //Resource 类型对应原版FontDescription.Resource表示资源路径形式的字体描述
    public sealed class Resource(Identifier id) : FontDescription
    {
        public Identifier Id { get; } = id;

        public override bool Equals(object? obj) => obj is Resource other && Id.Equals(other.Id);

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => Id.ToString();
    }

    //AtlasSprite 类型对应原版FontDescription.AtlasSprite表示图集精灵形式的字体描述
    public sealed class AtlasSprite(Identifier atlasId, Identifier spriteId) : FontDescription
    {
        public Identifier AtlasId { get; } = atlasId;
        public Identifier SpriteId { get; } = spriteId;

        public override bool Equals(object? obj)
        {
            return obj is AtlasSprite other && AtlasId.Equals(other.AtlasId) && SpriteId.Equals(other.SpriteId);
        }

        public override int GetHashCode() => HashCode.Combine(AtlasId, SpriteId);

        public override string ToString() => $"{AtlasId}:{SpriteId}";
    }
}
