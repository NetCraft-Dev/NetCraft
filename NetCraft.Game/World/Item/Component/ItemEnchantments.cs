using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Game.World.Items.Component;

//ItemEnchantments 物品附魔表 附魔引用到等级的映射 等级上限 255
//对应原版 net.minecraft.world.item.enchantment.ItemEnchantments
public sealed class ItemEnchantments : IEquatable<ItemEnchantments>
{
    //MaxLevel 等级上限 对应原版 LEVEL_CODEC 的 1..255
    public const int MaxLevel = 255;

    //Empty 空附魔表 对应原版 EMPTY
    public static readonly ItemEnchantments Empty = new(new Dictionary<Holder<Enchantment>, int>());

    //LevelCodec 等级编解码 对应原版 LEVEL_CODEC
    private static readonly Codec<int> LevelCodec = ExtraCodecs.IntRange(1, MaxLevel);

    //Codec 持久化编解码 附魔到等级的映射 对应原版 CODEC
    public static readonly Codec<ItemEnchantments> Codec =
        Codecs.UnboundedMap(HolderSetCodecs.EnchantmentRef, LevelCodec).ComapFlatMap(
            map =>
            {
                foreach (var level in map.Values)
                    if (level < 0 || level > MaxLevel)
                        return DataResult<ItemEnchantments>.Error(() => $"附魔等级 {level} 超出范围");
                return DataResult<ItemEnchantments>.Success(new ItemEnchantments(map));
            },
            enchantments => enchantments.Levels);

    //StreamCodec 网络编解码 数量前缀加逐条附魔与等级 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemEnchantments> StreamCodec =
        new ItemEnchantmentsStreamCodec();

    public ItemEnchantments(IReadOnlyDictionary<Holder<Enchantment>, int> levels)
    {
        Levels = new Dictionary<Holder<Enchantment>, int>(levels);
        foreach (var level in Levels.Values)
            if (level < 0 || level > MaxLevel) throw new ArgumentException($"附魔等级 {level} 超出范围");
    }

    public Dictionary<Holder<Enchantment>, int> Levels { get; }

    //GetLevel 取某条附魔的等级 缺省 0 对应原版 getLevel
    public int GetLevel(Holder<Enchantment> enchantment)
        => Levels.TryGetValue(enchantment, out var level) ? level : 0;

    public IReadOnlyDictionary<Holder<Enchantment>, int> EntrySet => Levels;

    public int Size => Levels.Count;

    public bool IsEmpty => Levels.Count == 0;

    //Mutable 可变附魔表 对应原版 Mutable
    public sealed class Mutable
    {
        private readonly Dictionary<Holder<Enchantment>, int> _levels;

        public Mutable(ItemEnchantments enchantments) => _levels = new Dictionary<Holder<Enchantment>, int>(enchantments.Levels);

        //Set 等级不大于 0 视为移除 上限截断到 255
        public void Set(Holder<Enchantment> enchantment, int level)
        {
            if (level <= 0) _levels.Remove(enchantment);
            else _levels[enchantment] = Math.Min(level, MaxLevel);
        }

        //Upgrade 只在更高时覆盖
        public void Upgrade(Holder<Enchantment> enchantment, int level)
        {
            if (level <= 0) return;
            var capped = Math.Min(level, MaxLevel);
            if (!_levels.TryGetValue(enchantment, out var current) || capped > current) _levels[enchantment] = capped;
        }

        public int GetLevel(Holder<Enchantment> enchantment)
            => _levels.TryGetValue(enchantment, out var level) ? level : 0;

        public ItemEnchantments ToImmutable() => new(_levels);
    }

    public bool Equals(ItemEnchantments? other)
    {
        if (other is null || other.Levels.Count != Levels.Count) return false;
        foreach (var kv in Levels)
            if (!other.Levels.TryGetValue(kv.Key, out var level) || level != kv.Value) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ItemEnchantments);

    public override int GetHashCode() => Levels.Count;

    public override string ToString() => $"ItemEnchantments[{Levels.Count} entries]";
}

//ItemEnchantmentsStreamCodec 数量前缀加逐条附魔与等级 对应原版 STREAM_CODEC
internal sealed class ItemEnchantmentsStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemEnchantments>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<Enchantment>> EnchantmentCodec =
        ByteBufCodecs.Holder(Registries.ENCHANTMENT);

    public ItemEnchantments Decode(RegistryFriendlyByteBuf buf)
    {
        var size = buf.ReadVarInt();
        var levels = new Dictionary<Holder<Enchantment>, int>();
        for (var i = 0; i < size; i++)
        {
            var enchantment = EnchantmentCodec.Decode(buf);
            levels[enchantment] = buf.ReadVarInt();
        }
        return new ItemEnchantments(levels);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemEnchantments value)
    {
        buf.WriteVarInt(value.Levels.Count);
        foreach (var kv in value.Levels)
        {
            EnchantmentCodec.Encode(buf, kv.Key);
            buf.WriteVarInt(kv.Value);
        }
    }
}
