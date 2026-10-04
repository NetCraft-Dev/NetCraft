using System.Collections;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.DataFixer.Fixes;

//审判台配置转注册表ID修复对应原版TrialSpawnerConfigInRegistryFix
//1.21把trial_spawner实体的normal_config/ominous_config两个内联CompoundTag匹配到原版预设并替换为注册表字符串ID
public class TrialSpawnerConfigInRegistryFix : NamedEntityFix
{
    public TrialSpawnerConfigInRegistryFix(Schema outputSchema)
        : base(outputSchema, false, "TrialSpawnerConfigInRegistryFix",
            References.BlockEntity, "minecraft:trial_spawner") { }

    //fixTag按normal_config/ominous_config查找注册表ID命中则替换为字符串ID加/normal或/ominous后缀
    public Dynamic<Tag> FixTag(Dynamic<Tag> input)
    {
        var normalConfigOpt = input.Get("normal_config").Result();
        if (!normalConfigOpt.IsPresent) return input;
        var ominousConfigOpt = input.Get("ominous_config").Result();
        if (!ominousConfigOpt.IsPresent) return input;
        var key = new Pair<Dynamic<Tag>, Dynamic<Tag>>(normalConfigOpt.Get(), ominousConfigOpt.Get());
        if (!VanillaTrialChambers.ConfigsToKey.TryGetValue(key, out var registryLocation))
            return input;
        return input.Set("normal_config", input.CreateString(registryLocation.WithSuffix("/normal").ToString()))
                    .Set("ominous_config", input.CreateString(registryLocation.WithSuffix("/ominous").ToString()));
    }

    //fix把输入Dynamic转NbtOps调用FixTag后再转回原ops对齐原版双ops转换
    protected override Typed<object> Fix(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), input =>
        {
            var inputOps = input.Ops;
            var result = FixTag(input.Convert(NbtOps.Instance));
            return result.Convert(inputOps);
        });

    //VanillaTrialChambers原版预设审判台配置集合
    //注册时把每个location的normal/ominous SNBT解析成CompoundTag存入3个变体到ConfigsToKey
    private static class VanillaTrialChambers
    {
        //ConfigsToKey用自定义相等比较器按Tag内容递归比较避免依赖字段顺序
        public static readonly Dictionary<Pair<Dynamic<Tag>, Dynamic<Tag>>, Identifier> ConfigsToKey
            = new(PairDynamicEqualityComparer.Instance);

        static VanillaTrialChambers()
        {
            Register(Identifier.WithDefaultNamespace("trial_chamber/breeze"),
                "{simultaneous_mobs: 1.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:breeze\"}}, weight: 1}], ticks_between_spawn: 20, total_mobs: 2.0f, total_mobs_added_per_player: 1.0f}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], simultaneous_mobs: 2.0f, total_mobs: 4.0f}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/melee/husk"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:husk\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:husk\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_melee\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/melee/spider"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:spider\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], simultaneous_mobs: 4.0f, total_mobs: 12.0f}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/melee/zombie"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:zombie\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:zombie\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_melee\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/ranged/poison_skeleton"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:bogged\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:bogged\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/ranged/skeleton"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:skeleton\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:skeleton\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/ranged/stray"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:stray\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:stray\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/slow_ranged/poison_skeleton"),
                "{simultaneous_mobs: 4.0f, simultaneous_mobs_added_per_player: 2.0f, spawn_potentials: [{data: {entity: {id: \"minecraft:bogged\"}}, weight: 1}], ticks_between_spawn: 160}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:bogged\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/slow_ranged/skeleton"),
                "{simultaneous_mobs: 4.0f, simultaneous_mobs_added_per_player: 2.0f, spawn_potentials: [{data: {entity: {id: \"minecraft:skeleton\"}}, weight: 1}], ticks_between_spawn: 160}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:skeleton\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/slow_ranged/stray"),
                "{simultaneous_mobs: 4.0f, simultaneous_mobs_added_per_player: 2.0f, spawn_potentials: [{data: {entity: {id: \"minecraft:stray\"}}, weight: 1}], ticks_between_spawn: 160}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {id: \"minecraft:stray\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_ranged\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/small_melee/baby_zombie"),
                "{simultaneous_mobs: 2.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {IsBaby: 1b, id: \"minecraft:zombie\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], spawn_potentials: [{data: {entity: {IsBaby: 1b, id: \"minecraft:zombie\"}, equipment: {loot_table: \"minecraft:equipment/trial_chamber_melee\", slot_drop_chances: 0.0f}}, weight: 1}]}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/small_melee/cave_spider"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:cave_spider\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], simultaneous_mobs: 4.0f, total_mobs: 12.0f}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/small_melee/silverfish"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {id: \"minecraft:silverfish\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], simultaneous_mobs: 4.0f, total_mobs: 12.0f}");
            Register(Identifier.WithDefaultNamespace("trial_chamber/small_melee/slime"),
                "{simultaneous_mobs: 3.0f, simultaneous_mobs_added_per_player: 0.5f, spawn_potentials: [{data: {entity: {Size: 1, id: \"minecraft:slime\"}}, weight: 3}, {data: {entity: {Size: 2, id: \"minecraft:slime\"}}, weight: 1}], ticks_between_spawn: 20}",
                "{loot_tables_to_eject: [{data: \"minecraft:spawners/ominous/trial_chamber/key\", weight: 3}, {data: \"minecraft:spawners/ominous/trial_chamber/consumables\", weight: 7}], simultaneous_mobs: 4.0f, total_mobs: 12.0f}");
        }

        //register解析normal/ominous SNBT构造3个变体存入ConfigsToKey
        private static void Register(Identifier location, string normalNbt, string ominousNbt)
        {
            try
            {
                var normalTag = Parse(normalNbt);
                var ominousTag = Parse(ominousNbt);
                var ominousMergedTag = (CompoundTag)normalTag.Copy();
                ominousMergedTag.Merge((CompoundTag)ominousTag);
                var ominousMergedDefaultsOmitted = RemoveDefaults((CompoundTag)ominousMergedTag.Copy());
                var dynamicNormal = AsDynamic(normalTag);
                ConfigsToKey[new Pair<Dynamic<Tag>, Dynamic<Tag>>(dynamicNormal, AsDynamic(ominousTag))] = location;
                ConfigsToKey[new Pair<Dynamic<Tag>, Dynamic<Tag>>(dynamicNormal, AsDynamic(ominousMergedTag))] = location;
                ConfigsToKey[new Pair<Dynamic<Tag>, Dynamic<Tag>>(dynamicNormal, AsDynamic(ominousMergedDefaultsOmitted))] = location;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Failed to parse NBT for " + location, e);
            }
        }

        private static Dynamic<Tag> AsDynamic(CompoundTag tag) => new(NbtOps.Instance, tag);

        //parse用TagParser.ParseCompoundFully失败转IllegalArgumentException
        private static CompoundTag Parse(string nbt)
        {
            try
            {
                return TagParser<Tag>.ParseCompoundFully(nbt);
            }
            catch (CommandSyntaxException e)
            {
                throw new ArgumentException("Failed to parse Trial Spawner NBT config: " + nbt, e);
            }
        }

        //removeDefaults移除等于原版默认值的字段对应原版removeDefaults
        private static CompoundTag RemoveDefaults(CompoundTag tag)
        {
            if (tag.GetIntOr("spawn_range", 0) == 4) tag.Remove("spawn_range");
            if ((tag.GetFloat("total_mobs")?.Value ?? 0.0f) == 6.0f) tag.Remove("total_mobs");
            if ((tag.GetFloat("simultaneous_mobs")?.Value ?? 0.0f) == 2.0f) tag.Remove("simultaneous_mobs");
            if ((tag.GetFloat("total_mobs_added_per_player")?.Value ?? 0.0f) == 2.0f) tag.Remove("total_mobs_added_per_player");
            if ((tag.GetFloat("simultaneous_mobs_added_per_player")?.Value ?? 0.0f) == 1.0f) tag.Remove("simultaneous_mobs_added_per_player");
            if (tag.GetIntOr("ticks_between_spawn", 0) == 40) tag.Remove("ticks_between_spawn");
            return tag;
        }
    }

    //PairDynamicEqualityComparer按Dynamic的Tag内容递归比较避免依赖字段顺序
    private sealed class PairDynamicEqualityComparer : IEqualityComparer<Pair<Dynamic<Tag>, Dynamic<Tag>>>
    {
        public static readonly PairDynamicEqualityComparer Instance = new();
        private PairDynamicEqualityComparer() { }

        public bool Equals(Pair<Dynamic<Tag>, Dynamic<Tag>> x, Pair<Dynamic<Tag>, Dynamic<Tag>> y)
            => TagEquals(x.First.Value, y.First.Value) && TagEquals(x.Second.Value, y.Second.Value);

        public int GetHashCode(Pair<Dynamic<Tag>, Dynamic<Tag>> obj)
            => HashCode.Combine(TagHash(obj.First.Value), TagHash(obj.Second.Value));

        //tagEquals按Tag.Id先区分类型再按类型比较内容
        private static bool TagEquals(Tag? a, Tag? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (a.Id != b.Id) return false;
            return a switch
            {
                ByteTag ba => ba.Value == ((ByteTag)b).Value,
                ShortTag sa => sa.Value == ((ShortTag)b).Value,
                IntTag ia => ia.Value == ((IntTag)b).Value,
                LongTag la => la.Value == ((LongTag)b).Value,
                FloatTag fa => fa.Value == ((FloatTag)b).Value,
                DoubleTag da => da.Value == ((DoubleTag)b).Value,
                StringTag sa => sa.Value == ((StringTag)b).Value,
                ByteArrayTag ba => ba.Value.SequenceEqual(((ByteArrayTag)b).Value),
                IntArrayTag ia => ia.Value.SequenceEqual(((IntArrayTag)b).Value),
                LongArrayTag la => la.Value.SequenceEqual(((LongArrayTag)b).Value),
                ListTag la => ListEquals(la, (ListTag)b),
                CompoundTag ca => CompoundEquals(ca, (CompoundTag)b),
                EndTag => true,
                _ => false
            };
        }

        //listEquals按长度逐元素递归比较
        private static bool ListEquals(ListTag a, ListTag b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!TagEquals(a[i], b[i])) return false;
            }
            return true;
        }

        //compoundEquals按key集合一致后逐key递归比较
        private static bool CompoundEquals(CompoundTag a, CompoundTag b)
        {
            if (a.Count != b.Count) return false;
            foreach (var key in a.Keys)
            {
                if (!b.Contains(key)) return false;
                if (!TagEquals(a[key], b[key])) return false;
            }
            return true;
        }

        //tagHash按Tag.Id+内容混合避免不同类型同哈希冲突
        private static int TagHash(Tag? t)
        {
            if (t is null) return 0;
            unchecked
            {
                int hash = t.Id;
                switch (t)
                {
                    case ByteTag b: return hash * 31 + b.Value.GetHashCode();
                    case ShortTag s: return hash * 31 + s.Value.GetHashCode();
                    case IntTag i: return hash * 31 + i.Value.GetHashCode();
                    case LongTag l: return hash * 31 + l.Value.GetHashCode();
                    case FloatTag f: return hash * 31 + f.Value.GetHashCode();
                    case DoubleTag d: return hash * 31 + d.Value.GetHashCode();
                    case StringTag s: return hash * 31 + s.Value.GetHashCode();
                    case ByteArrayTag b: return hash * 31 + ((IStructuralEquatable)b.Value).GetHashCode(EqualityComparer<byte>.Default);
                    case IntArrayTag i: return hash * 31 + ((IStructuralEquatable)i.Value).GetHashCode(EqualityComparer<int>.Default);
                    case LongArrayTag l: return hash * 31 + ((IStructuralEquatable)l.Value).GetHashCode(EqualityComparer<long>.Default);
                    case ListTag l:
                        foreach (var item in l) hash = hash * 31 + TagHash(item);
                        return hash;
                    case CompoundTag c:
                        foreach (var key in c.Keys) hash = hash * 31 + key.GetHashCode() * 31 + TagHash(c[key]);
                        return hash;
                    default: return hash;
                }
            }
        }
    }
}
