using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Particle;

//ParticleTypes 内置粒子类型注册与粒子选项流编解码 对应原版 net.minecraft.core.particles.ParticleTypes
//注册顺序严格按原版静态字段声明顺序 注册表 id 即客户端注册表 id 错位客户端会认错粒子
//参数形态未移植的类型仍占 id 但参数编解码抛异常 见 UnsupportedParticleType
public static class ParticleTypes
{
    private static bool _bootstrapped;

    //StreamCodec 粒子选项编解码 对应原版 ParticleTypes.STREAM_CODEC
    //先写 VarInt 类型注册表 id 再交类型自身写参数 无参数类型只写 id
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ParticleOptions> StreamCodec = new ParticleOptionsCodec();

    //Bootstrap 按原版声明顺序登记全部内置粒子类型 幂等
    public static void Bootstrap()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;
        Simple("angry_villager", false);                                  //0
        BlockType("block", false);                                        //1
        BlockType("block_marker", true);                                  //2
        Simple("bubble", false);                                          //3
        Simple("sulfur_bubbles", false);                                  //4
        Simple("noxious_gas", false);                                     //5
        Simple("noxious_gas_cloud", false);                               //6
        //geyser 系列参数是向量与目标点 依赖不存在的间歇泉子系统
        Unsupported("geyser", true);                                      //7
        Unsupported("geyser_base", true);                                 //8
        Unsupported("geyser_poof", true);                                 //9
        Unsupported("geyser_plume", true);                                //10
        Simple("cloud", false);                                           //11
        Simple("copper_fire_flame", false);                               //12
        Simple("crit", false);                                            //13
        Simple("damage_indicator", true);                                 //14
        //dragon_breath 的 PowerParticleOption 参数为 float 本包范围外 见报告
        Unsupported("dragon_breath", false);                              //15
        Simple("dripping_lava", false);                                   //16
        Simple("falling_lava", false);                                    //17
        Simple("landing_lava", false);                                    //18
        Simple("dripping_water", false);                                  //19
        Simple("falling_water", false);                                   //20
        DustType("dust", false);                                          //21
        DustColorTransitionType("dust_color_transition", false);           //22
        //effect/instant_effect 的 SpellParticleOption 参数为颜色与强度 本包范围外
        Unsupported("effect", false);                                     //23
        Simple("elder_guardian", true);                                   //24
        Simple("enchanted_hit", false);                                   //25
        Simple("enchant", false);                                         //26
        Simple("end_rod", false);                                         //27
        ColorType("entity_effect", false);                                //28
        Simple("explosion_emitter", true);                                //29
        Simple("explosion", true);                                        //30
        Simple("gust", true);                                             //31
        Simple("small_gust", false);                                      //32
        Simple("gust_emitter_large", true);                               //33
        Simple("gust_emitter_small", true);                               //34
        Simple("sonic_boom", true);                                       //35
        BlockType("falling_dust", false);                                 //36
        Simple("firework", false);                                        //37
        Simple("fishing", false);                                         //38
        Simple("flame", false);                                           //39
        Simple("infested", false);                                        //40
        Simple("cherry_leaves", false);                                   //41
        Simple("pale_oak_leaves", false);                                 //42
        ColorType("tinted_leaves", false);                                //43
        Simple("sculk_soul", false);                                      //44
        //sculk_charge 参数是 roll 角度 未在本次范围
        Unsupported("sculk_charge", true);                                //45
        Simple("sculk_charge_pop", true);                                 //46
        Simple("soul_fire_flame", false);                                 //47
        Simple("soul", false);                                            //48
        ColorType("flash", false);                                        //49
        Simple("happy_villager", false);                                  //50
        Simple("composter", false);                                       //51
        Simple("heart", false);                                           //52
        Unsupported("instant_effect", false);                             //53
        ItemType("item", false);                                          //54
        //vibration 参数是位置来源 依赖不存在的 PositionSource
        Unsupported("vibration", true);                                   //55
        //trail 参数是目标坐标与颜色 依赖不存在的向量序列化
        Unsupported("trail", false);                                      //56
        Simple("pause_mob_growth", false);                                //57
        Simple("reset_mob_growth", false);                                //58
        Simple("item_slime", false);                                      //59
        Simple("item_cobweb", false);                                     //60
        Simple("item_snowball", false);                                   //61
        Simple("large_smoke", false);                                     //62
        Simple("lava", false);                                            //63
        Simple("mycelium", false);                                        //64
        Simple("note", false);                                            //65
        Simple("poof", true);                                             //66
        Simple("portal", false);                                          //67
        Simple("rain", false);                                            //68
        Simple("smoke", false);                                           //69
        Simple("white_smoke", false);                                     //70
        Simple("sneeze", false);                                          //71
        Simple("spit", true);                                             //72
        Simple("squid_ink", true);                                        //73
        Simple("sweep_attack", true);                                     //74
        Simple("totem_of_undying", false);                                //75
        Simple("underwater", false);                                      //76
        Simple("splash", false);                                          //77
        Simple("witch", false);                                           //78
        Simple("bubble_pop", false);                                      //79
        Simple("current_down", false);                                    //80
        Simple("bubble_column_up", false);                                //81
        Simple("nautilus", false);                                        //82
        Simple("dolphin", false);                                         //83
        Simple("campfire_cosy_smoke", true);                              //84
        Simple("campfire_signal_smoke", true);                            //85
        Simple("dripping_honey", false);                                  //86
        Simple("falling_honey", false);                                   //87
        Simple("landing_honey", false);                                   //88
        Simple("falling_nectar", false);                                  //89
        Simple("falling_spore_blossom", false);                           //90
        Simple("ash", false);                                             //91
        Simple("crimson_spore", false);                                   //92
        Simple("warped_spore", false);                                    //93
        Simple("spore_blossom_air", false);                               //94
        Simple("dripping_obsidian_tear", false);                          //95
        Simple("falling_obsidian_tear", false);                           //96
        Simple("landing_obsidian_tear", false);                           //97
        Simple("reverse_portal", false);                                  //98
        Simple("white_ash", false);                                       //99
        Simple("small_flame", false);                                     //100
        Simple("snowflake", false);                                       //101
        Simple("dripping_dripstone_lava", false);                         //102
        Simple("falling_dripstone_lava", false);                          //103
        Simple("dripping_dripstone_water", false);                        //104
        Simple("falling_dripstone_water", false);                         //105
        Simple("glow_squid_ink", true);                                   //106
        Simple("glow", true);                                             //107
        Simple("wax_on", true);                                           //108
        Simple("wax_off", true);                                          //109
        Simple("electric_spark", true);                                   //110
        Simple("scrape", true);                                           //111
        //shriek 参数是延迟刻数 未在本次范围
        Unsupported("shriek", false);                                     //112
        Simple("egg_crack", false);                                       //113
        Simple("dust_plume", false);                                      //114
        Simple("trial_spawner_detection", true);                          //115
        Simple("trial_spawner_detection_ominous", true);                  //116
        Simple("vault_connection", true);                                 //117
        BlockType("dust_pillar", false);                                  //118
        Simple("ominous_spawning", true);                                 //119
        Simple("raid_omen", false);                                       //120
        Simple("trial_omen", false);                                      //121
        BlockType("block_crumble", false);                                //122
        Simple("firefly", false);                                         //123
        Simple("sulfur_cube_goo", false);                                 //124
    }

    //Find 按注册名取粒子类型 未注册或非本子系统实现返回 null
    public static ParticleType? Find(Identifier id)
        => BuiltInRegistries.PARTICLE_TYPE.GetValue(id) as ParticleType;

    private static SimpleParticleType Simple(string path, bool overrideLimiter)
        => Register(path, new SimpleParticleType(overrideLimiter));

    private static BlockParticleType BlockType(string path, bool overrideLimiter)
        => Register(path, new BlockParticleType(overrideLimiter));

    private static ItemParticleType ItemType(string path, bool overrideLimiter)
        => Register(path, new ItemParticleType(overrideLimiter));

    private static DustParticleType DustType(string path, bool overrideLimiter)
        => Register(path, new DustParticleType(overrideLimiter));

    private static DustColorTransitionParticleType DustColorTransitionType(string path, bool overrideLimiter)
        => Register(path, new DustColorTransitionParticleType(overrideLimiter));

    private static ColorParticleType ColorType(string path, bool overrideLimiter)
        => Register(path, new ColorParticleType(overrideLimiter));

    private static UnsupportedParticleType Unsupported(string path, bool overrideLimiter)
        => Register(path, new UnsupportedParticleType(path, overrideLimiter));

    //Register 按调用顺序登记 注册表 id 由登记顺序决定
    private static T Register<T>(string path, T type) where T : ParticleType
    {
        var id = Identifier.WithDefaultNamespace(path);
        BuiltInRegistries.PARTICLE_TYPE.Register(
            ResourceKey<NetCraft.Registry.ParticleType<object>>.Create(Registries.PARTICLE_TYPE, id),
            type, RegistrationInfo.BuiltIn);
        return type;
    }

    //ParticleOptionsCodec 粒子选项编解码对应原版 ByteBufCodecs.registry + dispatch 组合
    private sealed class ParticleOptionsCodec : StreamCodec<RegistryFriendlyByteBuf, ParticleOptions>
    {
        public ParticleOptions Decode(RegistryFriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var type = BuiltInRegistries.PARTICLE_TYPE.ById(id) as ParticleType
                ?? throw new InvalidOperationException($"未知粒子类型 id {id}");
            return type.ReadParameters(buf);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ParticleOptions value)
        {
            var type = value.Type;
            var id = BuiltInRegistries.PARTICLE_TYPE.GetId(type);
            if (id < 0) throw new InvalidOperationException($"粒子类型未注册: {type}");
            buf.WriteVarInt(id);
            type.WriteParameters(buf, value);
        }
    }
}
