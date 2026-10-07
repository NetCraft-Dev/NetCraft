using System.Collections;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Trial spawner config to registry ID fix, maps to vanilla TrialSpawnerConfigInRegistryFix
//1.21 matches the trial_spawner entity's normal_config/ominous_config inline CompoundTags against vanilla presets and replaces them with registry string IDs
public class TrialSpawnerConfigInRegistryFix : NamedEntityFix
{
    public TrialSpawnerConfigInRegistryFix(Schema outputSchema)
        : base(outputSchema, false, "TrialSpawnerConfigInRegistryFix",
            References.BlockEntity, "minecraft:trial_spawner") { }

    //fixTag looks up registry IDs by normal_config/ominous_config; on a hit it replaces with the string ID plus a /normal or /ominous suffix
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

    //fix converts the input Dynamic to NbtOps, calls FixTag then converts back to the original ops, aligned with the vanilla dual-ops conversion
    protected override Typed<object> Fix(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), input =>
        {
            var inputOps = input.Ops;
            var result = FixTag(input.Convert(NbtOps.Instance));
            return result.Convert(inputOps);
        });

    //VanillaTrialChambers the vanilla preset trial spawner config set
    //On registration it parses each location's normal/ominous SNBT into CompoundTag and stores 3 variants into ConfigsToKey
    private static class VanillaTrialChambers
    {
        //ConfigsToKey uses a custom equality comparer comparing Tag content recursively to avoid depending on field order
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

        //register parses normal/ominous SNBT, builds 3 variants and stores them into ConfigsToKey
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

        //parse throws IllegalArgumentException when TagParser.ParseCompoundFully fails
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

        //removeDefaults removes fields equal to the vanilla default values, maps to vanilla removeDefaults
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

    //PairDynamicEqualityComparer compares Dynamic Tag content recursively to avoid depending on field order
    private sealed class PairDynamicEqualityComparer : IEqualityComparer<Pair<Dynamic<Tag>, Dynamic<Tag>>>
    {
        public static readonly PairDynamicEqualityComparer Instance = new();
        private PairDynamicEqualityComparer() { }

        public bool Equals(Pair<Dynamic<Tag>, Dynamic<Tag>> x, Pair<Dynamic<Tag>, Dynamic<Tag>> y)
            => TagEquals(x.First.Value, y.First.Value) && TagEquals(x.Second.Value, y.Second.Value);

        public int GetHashCode(Pair<Dynamic<Tag>, Dynamic<Tag>> obj)
            => HashCode.Combine(TagHash(obj.First.Value), TagHash(obj.Second.Value));

        //tagEquals first distinguishes by Tag.Id then compares content by type
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

        //listEquals compares element by element recursively by length
        private static bool ListEquals(ListTag a, ListTag b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!TagEquals(a[i], b[i])) return false;
            }
            return true;
        }

        //compoundEquals compares key by key recursively after the key sets match
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

        //tagHash mixes Tag.Id + content to avoid hash collisions across different types
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
