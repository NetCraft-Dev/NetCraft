namespace NetCraft.Registry;

//SoundEvents built-in sound constants, maps to vanilla net.minecraft.sounds.SoundEvents
//Values are the vanilla sound ids; the server uses these constants directly when sending inline holders
public static class SoundEvents
{
    //PlayerAttackStrong player strong attack hit
    public static readonly SoundEvent PlayerAttackStrong = Create("entity.player.attack.strong");

    //PlayerAttackWeak player weak attack hit
    public static readonly SoundEvent PlayerAttackWeak = Create("entity.player.attack.weak");

    //PlayerAttackCrit player critical hit
    public static readonly SoundEvent PlayerAttackCrit = Create("entity.player.attack.crit");

    //PlayerAttackKnockback player knockback attack hit
    public static readonly SoundEvent PlayerAttackKnockback = Create("entity.player.attack.knockback");

    //PlayerAttackSweep player sweep attack
    public static readonly SoundEvent PlayerAttackSweep = Create("entity.player.attack.sweep");

    //PlayerAttackNodamage player attack dealt no damage
    public static readonly SoundEvent PlayerAttackNodamage = Create("entity.player.attack.nodamage");

    //PlayerHurt player hurt
    public static readonly SoundEvent PlayerHurt = Create("entity.player.hurt");

    //PlayerHurtDrowning player drowning hurt
    public static readonly SoundEvent PlayerHurtDrowning = Create("entity.player.hurt_drowning");

    //PlayerDeath player death
    public static readonly SoundEvent PlayerDeath = Create("entity.player.death");

    //PlayerLevelup player level up
    public static readonly SoundEvent PlayerLevelup = Create("entity.player.levelup");

    //PlayerBurp player finished eating
    public static readonly SoundEvent PlayerBurp = Create("entity.player.burp");

    //ItemPickup item picked up
    public static readonly SoundEvent ItemPickup = Create("entity.item.pickup");

    //ItemBreak item broken
    public static readonly SoundEvent ItemBreak = Create("entity.item.break");

    //GenericHurt generic hurt
    public static readonly SoundEvent GenericHurt = Create("entity.generic.hurt");

    //GenericDeath generic death
    public static readonly SoundEvent GenericDeath = Create("entity.generic.death");

    //PistonExtend piston extend
    public static readonly SoundEvent PistonExtend = Create("block.piston.extend");

    //PistonContract piston contract
    public static readonly SoundEvent PistonContract = Create("block.piston.contract");

    //LeverClick lever toggle; powered and unpowered have different pitches
    public static readonly SoundEvent LeverClick = Create("block.lever.click");

    //Note block: twenty base instruments plus six mob-head imitations; the last one is the custom-head fallback
    public static readonly SoundEvent NoteBlockHarp = Create("block.note_block.harp");
    public static readonly SoundEvent NoteBlockBasedrum = Create("block.note_block.basedrum");
    public static readonly SoundEvent NoteBlockSnare = Create("block.note_block.snare");
    public static readonly SoundEvent NoteBlockHat = Create("block.note_block.hat");
    public static readonly SoundEvent NoteBlockBass = Create("block.note_block.bass");
    public static readonly SoundEvent NoteBlockFlute = Create("block.note_block.flute");
    public static readonly SoundEvent NoteBlockBell = Create("block.note_block.bell");
    public static readonly SoundEvent NoteBlockGuitar = Create("block.note_block.guitar");
    public static readonly SoundEvent NoteBlockChime = Create("block.note_block.chime");
    public static readonly SoundEvent NoteBlockXylophone = Create("block.note_block.xylophone");
    public static readonly SoundEvent NoteBlockIronXylophone = Create("block.note_block.iron_xylophone");
    public static readonly SoundEvent NoteBlockCowBell = Create("block.note_block.cow_bell");
    public static readonly SoundEvent NoteBlockDidgeridoo = Create("block.note_block.didgeridoo");
    public static readonly SoundEvent NoteBlockBit = Create("block.note_block.bit");
    public static readonly SoundEvent NoteBlockBanjo = Create("block.note_block.banjo");
    public static readonly SoundEvent NoteBlockPling = Create("block.note_block.pling");
    public static readonly SoundEvent NoteBlockTrumpet = Create("block.note_block.trumpet");
    public static readonly SoundEvent NoteBlockTrumpetExposed = Create("block.note_block.trumpet_exposed");
    public static readonly SoundEvent NoteBlockTrumpetOxidized = Create("block.note_block.trumpet_oxidized");
    public static readonly SoundEvent NoteBlockTrumpetWeathered = Create("block.note_block.trumpet_weathered");
    public static readonly SoundEvent NoteBlockImitateZombie = Create("block.note_block.imitate.zombie");
    public static readonly SoundEvent NoteBlockImitateSkeleton = Create("block.note_block.imitate.skeleton");
    public static readonly SoundEvent NoteBlockImitateCreeper = Create("block.note_block.imitate.creeper");
    public static readonly SoundEvent NoteBlockImitateEnderDragon = Create("block.note_block.imitate.ender_dragon");
    public static readonly SoundEvent NoteBlockImitateWitherSkeleton = Create("block.note_block.imitate.wither_skeleton");
    public static readonly SoundEvent NoteBlockImitatePiglin = Create("block.note_block.imitate.piglin");

    //UiButtonClick used when a custom head has no dedicated sound, maps to vanilla CUSTOM_HEAD's sound
    public static readonly SoundEvent UiButtonClick = Create("ui.button.click");

    //Trapdoor open/close; wood is split into six groups by material, maps to trapdoorOpen/trapdoorClose for each vanilla BlockSetType
    public static readonly SoundEvent WoodenTrapdoorOpen = Create("block.wooden_trapdoor.open");
    public static readonly SoundEvent WoodenTrapdoorClose = Create("block.wooden_trapdoor.close");
    public static readonly SoundEvent IronTrapdoorOpen = Create("block.iron_trapdoor.open");
    public static readonly SoundEvent IronTrapdoorClose = Create("block.iron_trapdoor.close");
    public static readonly SoundEvent CopperTrapdoorOpen = Create("block.copper_trapdoor.open");
    public static readonly SoundEvent CopperTrapdoorClose = Create("block.copper_trapdoor.close");
    public static readonly SoundEvent CherryWoodTrapdoorOpen = Create("block.cherry_wood_trapdoor.open");
    public static readonly SoundEvent CherryWoodTrapdoorClose = Create("block.cherry_wood_trapdoor.close");
    public static readonly SoundEvent BambooWoodTrapdoorOpen = Create("block.bamboo_wood_trapdoor.open");
    public static readonly SoundEvent BambooWoodTrapdoorClose = Create("block.bamboo_wood_trapdoor.close");
    public static readonly SoundEvent NetherWoodTrapdoorOpen = Create("block.nether_wood_trapdoor.open");
    public static readonly SoundEvent NetherWoodTrapdoorClose = Create("block.nether_wood_trapdoor.close");

    //Fence gate open/close; default wood is one group with cherry, bamboo and nether wood each their own, maps to fenceGateOpen/fenceGateClose of vanilla WoodType
    public static readonly SoundEvent FenceGateOpen = Create("block.fence_gate.open");
    public static readonly SoundEvent FenceGateClose = Create("block.fence_gate.close");
    public static readonly SoundEvent CherryWoodFenceGateOpen = Create("block.cherry_wood_fence_gate.open");
    public static readonly SoundEvent CherryWoodFenceGateClose = Create("block.cherry_wood_fence_gate.close");
    public static readonly SoundEvent BambooWoodFenceGateOpen = Create("block.bamboo_wood_fence_gate.open");
    public static readonly SoundEvent BambooWoodFenceGateClose = Create("block.bamboo_wood_fence_gate.close");
    public static readonly SoundEvent NetherWoodFenceGateOpen = Create("block.nether_wood_fence_gate.open");
    public static readonly SoundEvent NetherWoodFenceGateClose = Create("block.nether_wood_fence_gate.close");

    //Door open/close; materials split the same as trapdoors, maps to doorOpen/doorClose for each vanilla BlockSetType
    public static readonly SoundEvent WoodenDoorOpen = Create("block.wooden_door.open");
    public static readonly SoundEvent WoodenDoorClose = Create("block.wooden_door.close");
    public static readonly SoundEvent IronDoorOpen = Create("block.iron_door.open");
    public static readonly SoundEvent IronDoorClose = Create("block.iron_door.close");
    public static readonly SoundEvent CopperDoorOpen = Create("block.copper_door.open");
    public static readonly SoundEvent CopperDoorClose = Create("block.copper_door.close");
    public static readonly SoundEvent CherryWoodDoorOpen = Create("block.cherry_wood_door.open");
    public static readonly SoundEvent CherryWoodDoorClose = Create("block.cherry_wood_door.close");
    public static readonly SoundEvent BambooWoodDoorOpen = Create("block.bamboo_wood_door.open");
    public static readonly SoundEvent BambooWoodDoorClose = Create("block.bamboo_wood_door.close");
    public static readonly SoundEvent NetherWoodDoorOpen = Create("block.nether_wood_door.open");
    public static readonly SoundEvent NetherWoodDoorClose = Create("block.nether_wood_door.close");

    //Create builds an event under the default namespace from the vanilla sound path
    private static SoundEvent Create(string path) => new(Identifier.WithDefaultNamespace(path));
}
