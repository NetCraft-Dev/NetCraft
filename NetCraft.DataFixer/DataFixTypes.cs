namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer.Fixes;

//MC data fix type maps to vanilla net.minecraft.util.datafix.DataFixTypes
//holds a DSL.ITypeReference identifier; delegates version upgrades to DataFixer by type
//vanilla uses an enum; here a sealed class + static instances emulate enum semantics
//the CompoundTag overload relies on NbtOps; the caller wraps Dynamic<Tag> itself, keeping DFU cross-ops pure
public sealed class DataFixTypes
{
    public static readonly DataFixTypes Level = new(References.Level);
    public static readonly DataFixTypes LevelSummary = new(References.LightweightLevel);
    public static readonly DataFixTypes Player = new(References.Player);
    public static readonly DataFixTypes Chunk = new(References.Chunk);
    public static readonly DataFixTypes Hotbar = new(References.Hotbar);
    public static readonly DataFixTypes Options = new(References.Options);
    public static readonly DataFixTypes Structure = new(References.Structure);
    public static readonly DataFixTypes Stats = new(References.Stats);
    public static readonly DataFixTypes SavedDataCommandStorage = new(References.SavedDataCommandStorage);
    public static readonly DataFixTypes SavedDataCustomBossEvents = new(References.SavedDataCustomBossEvents);
    public static readonly DataFixTypes SavedDataEnderDragonFight = new(References.SavedDataEnderDragonFight);
    public static readonly DataFixTypes SavedDataGameRules = new(References.SavedDataGameRules);
    public static readonly DataFixTypes SavedDataForcedChunks = new(References.SavedDataTickets);
    public static readonly DataFixTypes SavedDataMapData = new(References.SavedDataMapData);
    public static readonly DataFixTypes SavedDataMapIndex = new(References.SavedDataMapIndex);
    public static readonly DataFixTypes SavedDataRaids = new(References.SavedDataRaids);
    public static readonly DataFixTypes SavedDataRandomSequences = new(References.SavedDataRandomSequences);
    public static readonly DataFixTypes SavedDataScheduledEvents = new(References.SavedDataScheduledEvents);
    public static readonly DataFixTypes SavedDataScoreboard = new(References.SavedDataScoreboard);
    public static readonly DataFixTypes SavedDataStopwatches = new(References.SavedDataStopwatches);
    public static readonly DataFixTypes SavedDataStructureFeatureIndices = new(References.SavedDataStructureFeatureIndices);
    public static readonly DataFixTypes SavedDataWanderingTrader = new(References.SavedDataWanderingTrader);
    public static readonly DataFixTypes SavedDataWeather = new(References.SavedDataWeather);
    public static readonly DataFixTypes SavedDataWorldBorder = new(References.SavedDataWorldBorder);
    public static readonly DataFixTypes SavedDataWorldClocks = new(References.SavedDataWorldClocks);
    public static readonly DataFixTypes SavedDataWorldGenSettings = new(References.SavedDataWorldGenSettings);
    public static readonly DataFixTypes Advancements = new(References.Advancements);
    public static readonly DataFixTypes PoiChunk = new(References.PoiChunk);
    public static readonly DataFixTypes WorldGenSettings = new(References.WorldGenSettings);
    public static readonly DataFixTypes EntityChunk = new(References.EntityChunk);
    public static readonly DataFixTypes DebugProfile = new(References.DebugProfile);

    //TYPES_FOR_LEVEL_LIST: vanilla uses Set.of(LEVEL_SUMMARY.type)
    public static readonly IReadOnlySet<DSL.ITypeReference> TypesForLevelList = new HashSet<DSL.ITypeReference> { References.LightweightLevel };

    public DSL.ITypeReference Type { get; }

    private DataFixTypes(DSL.ITypeReference type) => Type = type;

    //currentVersion: the current world data version
    private static int CurrentVersion() => SharedConstants.WorldDataVersion;

    //update applies updates to a Dynamic by version range, delegating to DataFixer.update
    public Dynamic<T> Update<T>(DataFixer fixerUpper, Dynamic<T> input, int fromVersion, int toVersion)
        => fixerUpper.Update(Type, input, fromVersion, toVersion);

    //updateToCurrentVersion upgrades to the current version
    public Dynamic<T> UpdateToCurrentVersion<T>(DataFixer fixerUpper, Dynamic<T> input, int dataVersion)
        => Update(fixerUpper, input, dataVersion, CurrentVersion());
}
