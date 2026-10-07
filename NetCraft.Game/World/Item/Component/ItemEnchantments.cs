using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Game.World.Items.Component;

//ItemEnchantments item enchantment map from enchantment reference to level, level limit 255
//Maps to vanilla net.minecraft.world.item.enchantment.ItemEnchantments
public sealed class ItemEnchantments : IEquatable<ItemEnchantments>
{
    //MaxLevel level limit, the 1..255 range of vanilla LEVEL_CODEC
    public const int MaxLevel = 255;

    //Empty empty enchantment map, maps to vanilla EMPTY
    public static readonly ItemEnchantments Empty = new(new Dictionary<Holder<Enchantment>, int>());

    //LevelCodec level codec, maps to vanilla LEVEL_CODEC
    private static readonly Codec<int> LevelCodec = ExtraCodecs.IntRange(1, MaxLevel);

    //Codec persistence codec, a map from enchantment to level, maps to vanilla CODEC
    public static readonly Codec<ItemEnchantments> Codec =
        Codecs.UnboundedMap(HolderSetCodecs.EnchantmentRef, LevelCodec).ComapFlatMap(
            map =>
            {
                foreach (var level in map.Values)
                    if (level < 0 || level > MaxLevel)
                        return DataResult<ItemEnchantments>.Error(() => $"enchantment level {level} out of range");
                return DataResult<ItemEnchantments>.Success(new ItemEnchantments(map));
            },
            enchantments => enchantments.Levels);

    //StreamCodec network codec, a count prefix followed by each enchantment and level, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemEnchantments> StreamCodec =
        new ItemEnchantmentsStreamCodec();

    public ItemEnchantments(IReadOnlyDictionary<Holder<Enchantment>, int> levels)
    {
        Levels = new Dictionary<Holder<Enchantment>, int>(levels);
        foreach (var level in Levels.Values)
            if (level < 0 || level > MaxLevel) throw new ArgumentException($"enchantment level {level} out of range");
    }

    public Dictionary<Holder<Enchantment>, int> Levels { get; }

    //GetLevel returns the level of an enchantment, default 0, maps to vanilla getLevel
    public int GetLevel(Holder<Enchantment> enchantment)
        => Levels.TryGetValue(enchantment, out var level) ? level : 0;

    public IReadOnlyDictionary<Holder<Enchantment>, int> EntrySet => Levels;

    public int Size => Levels.Count;

    public bool IsEmpty => Levels.Count == 0;

    //Mutable mutable enchantment map, maps to vanilla Mutable
    public sealed class Mutable
    {
        private readonly Dictionary<Holder<Enchantment>, int> _levels;

        public Mutable(ItemEnchantments enchantments) => _levels = new Dictionary<Holder<Enchantment>, int>(enchantments.Levels);

        //Set a level of 0 or less removes the entry, the limit is clamped to 255
        public void Set(Holder<Enchantment> enchantment, int level)
        {
            if (level <= 0) _levels.Remove(enchantment);
            else _levels[enchantment] = Math.Min(level, MaxLevel);
        }

        //Upgrade overwrites only when the level is higher
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

//ItemEnchantmentsStreamCodec count prefix followed by each enchantment and level, maps to vanilla STREAM_CODEC
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
