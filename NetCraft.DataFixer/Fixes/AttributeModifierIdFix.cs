using System.Globalization;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using NetCraft.Util;

namespace NetCraft.DataFixer.Fixes;

//属性修饰符UUID/Name转ID修复对应原版AttributeModifierIdFix
//1.21把item_stack的attribute_modifiers和entity/player的Attributes中的UUID/Name替换为命名空间ID
public class AttributeModifierIdFix : DataFix
{
    //34个原版硬编码UUID到命名空间ID的映射
    private static readonly Dictionary<Guid, string> IdMap = new()
    {
        { Guid.Parse("736565d2-e1a7-403d-a3f8-1aeb3e302542"), "minecraft:creative_mode_block_range" },
        { Guid.Parse("98491ef6-97b1-4584-ae82-71a8cc85cf73"), "minecraft:creative_mode_entity_range" },
        { Guid.Parse("91AEAA56-376B-4498-935B-2F7F68070635"), "minecraft:effect.speed" },
        { Guid.Parse("7107DE5E-7CE8-4030-940E-514C1F160890"), "minecraft:effect.slowness" },
        { Guid.Parse("AF8B6E3F-3328-4C0A-AA36-5BA2BB9DBEF3"), "minecraft:effect.haste" },
        { Guid.Parse("55FCED67-E92A-486E-9800-B47F202C4386"), "minecraft:effect.mining_fatigue" },
        { Guid.Parse("648D7064-6A60-4F59-8ABE-C2C23A6DD7A9"), "minecraft:effect.strength" },
        { Guid.Parse("C0105BF3-AEF8-46B0-9EBC-92943757CCBE"), "minecraft:effect.jump_boost" },
        { Guid.Parse("22653B89-116E-49DC-9B6B-9971489B5BE5"), "minecraft:effect.weakness" },
        { Guid.Parse("5D6F0BA2-1186-46AC-B896-C61C5CEE99CC"), "minecraft:effect.health_boost" },
        { Guid.Parse("EAE29CF0-701E-4ED6-883A-96F798F3DAB5"), "minecraft:effect.absorption" },
        { Guid.Parse("03C3C89D-7037-4B42-869F-B146BCB64D2E"), "minecraft:effect.luck" },
        { Guid.Parse("CC5AF142-2BD2-4215-B636-2605AED11727"), "minecraft:effect.unluck" },
        { Guid.Parse("6555be74-63b3-41f1-a245-77833b3c2562"), "minecraft:evil" },
        { Guid.Parse("1eaf83ff-7207-4596-b37a-d7a07b3ec4ce"), "minecraft:powder_snow" },
        { Guid.Parse("662A6B8D-DA3E-4C1C-8813-96EA6097278D"), "minecraft:sprinting" },
        { Guid.Parse("020E0DFB-87AE-4653-9556-831010E291A0"), "minecraft:attacking" },
        { Guid.Parse("766bfa64-11f3-11ea-8d71-362b9e155667"), "minecraft:baby" },
        { Guid.Parse("7E0292F2-9434-48D5-A29F-9583AF7DF27F"), "minecraft:covered" },
        { Guid.Parse("9e362924-01de-4ddd-a2b2-d0f7a405a174"), "minecraft:suffocating" },
        { Guid.Parse("5CD17E52-A79A-43D3-A529-90FDE04B181E"), "minecraft:drinking" },
        { Guid.Parse("B9766B59-9566-4402-BC1F-2EE2A276D836"), "minecraft:baby" },
        { Guid.Parse("49455A49-7EC5-45BA-B886-3B90B23A1718"), "minecraft:attacking" },
        { Guid.Parse("845DB27C-C624-495F-8C9F-6020A9A58B6B"), "minecraft:armor.boots" },
        { Guid.Parse("D8499B04-0E66-4726-AB29-64469D734E0D"), "minecraft:armor.leggings" },
        { Guid.Parse("9F3D476D-C118-4544-8365-64846904B48E"), "minecraft:armor.chestplate" },
        { Guid.Parse("2AD3F246-FEE1-4E67-B886-69FD380BB150"), "minecraft:armor.helmet" },
        { Guid.Parse("C1C72771-8B8E-BA4A-ACE0-81A93C8928B2"), "minecraft:armor.body" },
        { Guid.Parse("b572ecd2-ac0c-4071-abde-9594af072a37"), "minecraft:enchantment.fire_protection" },
        { Guid.Parse("40a9968f-5c66-4e2f-b7f4-2ec2f4b3e450"), "minecraft:enchantment.blast_protection" },
        { Guid.Parse("07a65791-f64d-4e79-86c7-f83932f007ec"), "minecraft:enchantment.respiration" },
        { Guid.Parse("60b1b7db-fffd-4ad0-817c-d6c6a93d8a45"), "minecraft:enchantment.aqua_affinity" },
        { Guid.Parse("11dc269a-4476-46c0-aff3-9e17d7eb6801"), "minecraft:enchantment.depth_strider" },
        { Guid.Parse("87f46a96-686f-4796-b035-22b16ee9e038"), "minecraft:enchantment.soul_speed" },
        { Guid.Parse("b9716dbd-50df-4080-850e-70347d24e687"), "minecraft:enchantment.soul_speed" },
        { Guid.Parse("92437d00-c3a7-4f2e-8f6c-1f21585d5dd0"), "minecraft:enchantment.swift_sneak" },
        { Guid.Parse("5d3d087b-debe-4037-b53e-d84f3ff51f17"), "minecraft:enchantment.sweeping_edge" },
        { Guid.Parse("3ceb37c0-db62-46b5-bd02-785457b01d96"), "minecraft:enchantment.efficiency" },
        { Guid.Parse("CB3F55D3-645C-4F38-A497-9C13A33DB5CF"), "minecraft:base_attack_damage" },
        { Guid.Parse("FA233E1C-4180-4865-B01B-BCCE9785ACA3"), "minecraft:base_attack_speed" }
    };

    //5个Name到命名空间ID的映射对应原版NAME_MAP
    private static readonly Dictionary<string, string> NameMap = new()
    {
        { "Random spawn bonus", "minecraft:random_spawn_bonus" },
        { "Random zombie-spawn bonus", "minecraft:zombie_random_spawn_bonus" },
        { "Leader zombie bonus", "minecraft:leader_zombie_bonus" },
        { "Zombie reinforcement callee charge", "minecraft:reinforcement_callee_charge" },
        { "Zombie reinforcement caller charge", "minecraft:reinforcement_caller_charge" }
    };

    public AttributeModifierIdFix(Schema outputSchema) : base(outputSchema, false) { }

    protected override TypeRewriteRule MakeRule()
    {
        var itemStackType = GetInputSchema().GetType(References.ItemStack);
        var componentsFinder = itemStackType.FindField(FixConstants.ItemInstanceComponents);
        return TypeRewriteRule.Seq(
            FixTypeEverywhereTyped("AttributeIdFix (ItemStack)", itemStackType,
                itemStack => itemStack.UpdateTyped(componentsFinder,
                    components => components.Update(DSL.RemainderFinder(), FixItemStackComponents))),
            FixTypeEverywhereTyped("AttributeIdFix (Entity)",
                GetInputSchema().GetType(References.Entity), FixEntity),
            FixTypeEverywhereTyped("AttributeIdFix (Player)",
                GetInputSchema().GetType(References.Player), FixEntity));
    }

    //fixModifiers按UUID或Name匹配后写入id字段移除uuid/name同名id累加amount
    private static IEnumerable<Dynamic<object>> FixModifiers(IEnumerable<Dynamic<object>> modifiers)
    {
        var result = new Dictionary<string, Dynamic<object>>();
        foreach (var modifier in modifiers)
        {
            var uuidArray = modifier.Get("uuid").AsStreamOpt()
                .Select(d => (int)d.AsNumber(0)).ToArray();
            var uuid = UuidFromIntArray(uuidArray);
            var name = modifier.Get(FixConstants.JigsawBlockEntityName).AsString("");
            var idFromUuid = uuid is Guid g && IdMap.TryGetValue(g, out var id1) ? id1 : null;
            var idFromName = NameMap.TryGetValue(name, out var id2) ? id2 : null;
            if (idFromUuid != null)
            {
                result[idFromUuid] = modifier.Set("id", modifier.CreateString(idFromUuid))
                    .Remove("uuid").Remove(FixConstants.JigsawBlockEntityName);
                continue;
            }
            if (idFromName != null)
            {
                if (!result.TryGetValue(idFromName, out var preExisting))
                {
                    result[idFromName] = modifier.Set("id", modifier.CreateString(idFromName))
                        .Remove("uuid").Remove(FixConstants.JigsawBlockEntityName);
                    continue;
                }
                var amount = preExisting.Get("amount").AsDouble(0.0);
                var added = modifier.Get("amount").AsDouble(0.0);
                result[idFromName] = preExisting.Set("amount", modifier.CreateDouble(amount + added));
                continue;
            }
            var fallbackId = "minecraft:" + (uuid is Guid gg
                ? gg.ToString("D").ToLowerInvariant()
                : "unknown");
            result[fallbackId] = modifier.Set("id", modifier.CreateString(fallbackId))
                .Remove("uuid").Remove(FixConstants.JigsawBlockEntityName);
        }
        return result.Values;
    }

    //convertModifierForEntity把实体modifier字段名重命名并把operation枚举转字符串
    private static Dynamic<object> ConvertModifierForEntity(Dynamic<object> modifier)
        => modifier.RenameField(FixConstants.EntityUuid, "uuid")
            .RenameField(FixConstants.StateHolderName, FixConstants.JigsawBlockEntityName)
            .RenameField("Amount", "amount")
            .RenameAndFixField("Operation", "operation", operation =>
            {
                var str = operation.AsInt(0) switch
                {
                    0 => "add_value",
                    1 => "add_multiplied_base",
                    2 => "add_multiplied_total",
                    _ => "invalid"
                };
                return operation.CreateString(str);
            });

    //fixItemStackComponents处理minecraft:attribute_modifiers.modifiers列表
    private static Dynamic<object> FixItemStackComponents(Dynamic<object> components)
        => components.Update("minecraft:attribute_modifiers", attributeModifiers =>
            attributeModifiers.Update("modifiers", modifiers =>
            {
                var streamOpt = modifiers.AsStream().Result();
                if (!streamOpt.IsPresent) return modifiers;
                return modifiers.CreateList(FixModifiers(streamOpt.Get()));
            }));

    //fixAttribute重命名单个attribute字段并把modifiers映射为convertModifierForEntity+fixModifiers
    private static Dynamic<object> FixAttribute(Dynamic<object> attribute)
        => attribute.RenameField(FixConstants.StateHolderName, "id")
            .RenameField("Base", "base")
            .RenameAndFixField("Modifiers", "modifiers", modifiers =>
            {
                var streamOpt = modifiers.AsStream().Result();
                if (!streamOpt.IsPresent) return modifiers;
                var mapped = streamOpt.Get().Select(ConvertModifierForEntity);
                return attribute.CreateList(FixModifiers(mapped));
            });

    //fixEntity处理实体Attributes列表中每个attribute
    private static Typed<object> FixEntity(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), tag =>
            tag.RenameAndFixField("Attributes", FixConstants.LivingEntityAttributes, attributeList =>
            {
                var streamOpt = attributeList.AsStream().Result();
                if (!streamOpt.IsPresent) return attributeList;
                var mapped = streamOpt.Get().Select(FixAttribute);
                return attributeList.CreateList(mapped);
            }));

    //uuidFromIntArray按int[4]拼mostSigBits/leastSigBits后用hex字符串构造Guid
    //保证与Java UUID.toString()输出格式一致对齐原版大端字节序
    public static Guid? UuidFromIntArray(int[] intArray)
    {
        if (intArray.Length != 4) return null;
        long mostSigBits = ((long)intArray[0] << 32) | ((long)intArray[1] & 0xFFFFFFFFL);
        long leastSigBits = ((long)intArray[2] << 32) | ((long)intArray[3] & 0xFFFFFFFFL);
        var hex = mostSigBits.ToString("x16", CultureInfo.InvariantCulture)
            + leastSigBits.ToString("x16", CultureInfo.InvariantCulture);
        var uuidStr = $"{hex.Substring(0, 8)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";
        return Guid.Parse(uuidStr);
    }
}
