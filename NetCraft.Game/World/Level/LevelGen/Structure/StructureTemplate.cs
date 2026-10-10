using System.Runtime.CompilerServices;
using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using FrontAndTopEnum = NetCraft.Registry.Enums.FrontAndTop;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplate structure template, maps to vanilla net.minecraft.world.level.levelgen.structure.templatesystem.StructureTemplate
//An NBT template = size + palettes + entity list; placement writes blocks into the world with mirror and rotation applied
public sealed class StructureTemplate
{
    public const string PaletteTag = "palette";
    public const string PaletteListTag = "palettes";
    public const string EntitiesTag = "entities";
    public const string BlocksTag = "blocks";
    public const string BlockTagPos = "pos";
    public const string BlockTagState = "state";
    public const string BlockTagNbt = "nbt";
    public const string EntityTagPos = "pos";
    public const string EntityTagBlockPos = "blockPos";
    public const string EntityTagNbt = "nbt";
    public const string SizeTag = "size";

    //JigsawBlockId jigsaw block registry name; jigsaw collection filters blocks by it
    public static readonly Identifier JigsawBlockId = Identifier.WithDefaultNamespace("jigsaw");

    private readonly List<StructureTemplatePalette> _palettes = new();
    private readonly List<StructureEntityInfo> _entityInfoList = new();

    //BlockEntitySink block entity sink injected by the Game layer during chunk generation; without it a block with nbt writes only its state
    public Action<BlockPos, BlockState, CompoundTag>? BlockEntitySink { get; set; }

    public Vec3i Size { get; private set; } = Vec3i.Zero;

    public string Author { get; private set; } = "?";

    public IReadOnlyList<StructureTemplatePalette> Palettes => _palettes;

    public IReadOnlyList<StructureEntityInfo> EntityInfoList => _entityInfoList;

    //GetSize footprint after rotation; a 90-degree rotation swaps X and Z
    public Vec3i GetSize(Rotation rotation)
        => rotation is Rotation.Clockwise90 or Rotation.Counterclockwise90
            ? new Vec3i(Size.Z, Size.Y, Size.X)
            : Size;

    //Load reads the template NBT; prefer palettes when present, otherwise fall back to a single palette
    public void Load(CompoundTag tag)
    {
        _palettes.Clear();
        _entityInfoList.Clear();

        var sizeTag = tag.GetListOrEmpty(SizeTag);
        Size = new Vec3i(ListInt(sizeTag, 0), ListInt(sizeTag, 1), ListInt(sizeTag, 2));

        var blockList = tag.GetListOrEmpty(BlocksTag);
        var paletteListList = tag.GetList(PaletteListTag);
        if (paletteListList is not null)
        {
            for (var i = 0; i < paletteListList.Count; i++)
                LoadPalette(paletteListList.GetList(i) ?? new ListTag(), blockList);
        }
        else
        {
            LoadPalette(tag.GetListOrEmpty(PaletteTag), blockList);
        }

        //An entity entry missing nbt is dropped entirely, matching vanilla
        foreach (var element in tag.GetListOrEmpty(EntitiesTag))
        {
            if (element is not CompoundTag entityTag) continue;
            var posTag = entityTag.GetListOrEmpty(EntityTagPos);
            var pos = new Vec3i((int)ListDouble(posTag, 0), (int)ListDouble(posTag, 1), (int)ListDouble(posTag, 2));
            var blockPosTag = entityTag.GetListOrEmpty(EntityTagBlockPos);
            var blockPos = new BlockPos(ListInt(blockPosTag, 0), ListInt(blockPosTag, 1), ListInt(blockPosTag, 2));
            if (entityTag.GetCompound(EntityTagNbt) is not { } nbt) continue;
            _entityInfoList.Add(new StructureEntityInfo(pos, blockPos, nbt));
        }
    }

    //LoadPalette decodes one palette and its corresponding block list
    private void LoadPalette(ListTag paletteList, ListTag blockList)
    {
        var states = new List<BlockState>(paletteList.Count);
        for (var i = 0; i < paletteList.Count; i++)
            states.Add(ReadBlockState(paletteList.GetCompound(i) ?? new CompoundTag()));

        var fullBlocks = new List<StructureBlockInfo>();
        var blockEntities = new List<StructureBlockInfo>();
        var otherBlocks = new List<StructureBlockInfo>();
        foreach (var element in blockList)
        {
            if (element is not CompoundTag blockTag) continue;
            var posTag = blockTag.GetListOrEmpty(BlockTagPos);
            var pos = new BlockPos(ListInt(posTag, 0), ListInt(posTag, 1), ListInt(posTag, 2));
            var index = blockTag.GetIntOr(BlockTagState, 0);
            //An out-of-range palette index is treated as air, matching the out-of-range branch of vanilla stateFor
            var state = index >= 0 && index < states.Count ? states[index] : Blocks.AIR.DefaultBlockState;
            var info = new StructureBlockInfo(pos, state, blockTag.GetCompound(BlockTagNbt));
            if (info.Nbt is not null) blockEntities.Add(info);
            else if (info.State.Owner.CanOcclude) fullBlocks.Add(info);
            else otherBlocks.Add(info);
        }
        _palettes.Add(new StructureTemplatePalette(BuildInfoList(fullBlocks, otherBlocks, blockEntities)));
    }

    //BuildInfoList sorts the three groups by y/x/z then concatenates; block entities go last so they appear after their supporting blocks
    private static List<StructureBlockInfo> BuildInfoList(List<StructureBlockInfo> fullBlocks,
        List<StructureBlockInfo> otherBlocks, List<StructureBlockInfo> blockEntities)
    {
        Comparison<StructureBlockInfo> comparison = (a, b) =>
        {
            var byY = a.Pos.Y.CompareTo(b.Pos.Y);
            if (byY != 0) return byY;
            var byX = a.Pos.X.CompareTo(b.Pos.X);
            return byX != 0 ? byX : a.Pos.Z.CompareTo(b.Pos.Z);
        };
        fullBlocks.Sort(comparison);
        otherBlocks.Sort(comparison);
        blockEntities.Sort(comparison);
        var result = new List<StructureBlockInfo>(fullBlocks.Count + otherBlocks.Count + blockEntities.Count);
        result.AddRange(fullBlocks);
        result.AddRange(otherBlocks);
        result.AddRange(blockEntities);
        return result;
    }

    //ReadBlockState reads one block state NBT block, maps to vanilla NbtUtils.readBlockState
    //An unknown block name returns air; an invalid property name or value keeps the current state without erroring
    public static BlockState ReadBlockState(CompoundTag tag)
    {
        var id = Identifier.TryParse(tag.GetStringValue("Name"));
        if (id is null || !BuiltInRegistries.BLOCK.ContainsKey(id.Value)) return Blocks.AIR.DefaultBlockState;
        var state = BuiltInRegistries.BLOCK.GetValue(id.Value)!.DefaultBlockState;
        if (tag.GetCompound("Properties") is not { } properties) return state;
        foreach (var key in properties.Keys)
        {
            PropertyBase? property = null;
            foreach (var candidate in state.GetProperties())
            {
                if (candidate.Name != key) continue;
                property = candidate;
                break;
            }
            if (property is null) continue;
            var value = property.GetValueForName(properties.GetStringValue(key));
            if (value is null) continue;
            state = StructureBlockTransforms.SetIfAllowed(state, property, value);
        }
        return state;
    }

    //Transform maps in-template coordinates to target coordinates by mirror and rotation, maps to vanilla StructureTemplate.transform
    //Always mirror then rotate; reversing the order gives wrong results
    public static BlockPos Transform(BlockPos pos, Mirror mirror, Rotation rotation, BlockPos pivot)
    {
        var x = pos.X;
        var y = pos.Y;
        var z = pos.Z;
        var wasMirrored = true;
        switch (mirror)
        {
            case Mirror.LeftRight:
                z = -z;
                break;
            case Mirror.FrontBack:
                x = -x;
                break;
            default:
                wasMirrored = false;
                break;
        }
        var pivotX = pivot.X;
        var pivotZ = pivot.Z;
        return rotation switch
        {
            Rotation.Clockwise180 => new BlockPos(pivotX + pivotX - x, y, pivotZ + pivotZ - z),
            Rotation.Counterclockwise90 => new BlockPos(pivotX - pivotZ + z, y, pivotX + pivotZ - x),
            Rotation.Clockwise90 => new BlockPos(pivotX + pivotZ - z, y, pivotZ - pivotX + x),
            _ => wasMirrored ? new BlockPos(x, y, z) : pos,
        };
    }

    //CalculateRelativePosition returns the transformed relative position of an in-template coordinate, maps to vanilla calculateRelativePosition
    public static BlockPos CalculateRelativePosition(StructurePlaceSettings settings, BlockPos pos)
        => Transform(pos, settings.Mirror, settings.Rotation, settings.RotationPivot);

    //GetZeroPositionWithTransform finds where the template origin lands, maps to vanilla getZeroPositionWithTransform
    //The size-minus-one participates on the mirrored side; without it a mirrored structure shifts by one block
    public static BlockPos GetZeroPositionWithTransform(BlockPos zeroPos, Mirror mirror, Rotation rotation,
        int sizeX, int sizeZ)
    {
        var mirrorDeltaX = mirror == Mirror.FrontBack ? --sizeX : 0;
        var mirrorDeltaZ = mirror == Mirror.LeftRight ? --sizeZ : 0;
        return rotation switch
        {
            Rotation.Clockwise90 => zeroPos.Offset(sizeZ - mirrorDeltaZ, 0, mirrorDeltaX),
            Rotation.Clockwise180 => zeroPos.Offset(sizeX - mirrorDeltaX, 0, sizeZ - mirrorDeltaZ),
            Rotation.Counterclockwise90 => zeroPos.Offset(mirrorDeltaZ, 0, sizeX - mirrorDeltaX),
            _ => zeroPos.Offset(mirrorDeltaX, 0, mirrorDeltaZ),
        };
    }

    //GetBoundingBox integer bounding box the template occupies after placing with the settings, maps to vanilla getBoundingBox
    public BoundingBoxInt GetBoundingBox(StructurePlaceSettings settings, BlockPos position)
    {
        var delta = Size.Offset(-1, -1, -1);
        var corner1 = Transform(BlockPos.Zero, settings.Mirror, settings.Rotation, settings.RotationPivot);
        var corner2 = Transform(BlockPos.Zero.Offset(delta.X, delta.Y, delta.Z),
            settings.Mirror, settings.Rotation, settings.RotationPivot);
        var minX = Math.Min(corner1.X, corner2.X) + position.X;
        var minY = Math.Min(corner1.Y, corner2.Y) + position.Y;
        var minZ = Math.Min(corner1.Z, corner2.Z) + position.Z;
        var maxX = Math.Max(corner1.X, corner2.X) + position.X;
        var maxY = Math.Max(corner1.Y, corner2.Y) + position.Y;
        var maxZ = Math.Max(corner1.Z, corner2.Z) + position.Z;
        return new BoundingBoxInt(minX, minY, minZ, maxX, maxY, maxZ);
    }

    //FilterBlocks filters the template blocks by block, maps to vanilla filterBlocks
    //When absolute is false the coordinates stay in-template; when true they are rotated and translated to the target position
    public List<StructureBlockInfo> FilterBlocks(BlockPos position, StructurePlaceSettings settings, RegBlock block,
        bool absolute)
    {
        var result = new List<StructureBlockInfo>();
        if (_palettes.Count == 0) return result;
        var boundingBox = settings.BoundingBox;
        foreach (var blockInfo in settings.GetRandomPalette(_palettes, position).Blocks)
        {
            if (blockInfo.State.Owner != block) continue;
            var blockPos = absolute
                ? CalculateRelativePosition(settings, blockInfo.Pos).Offset(position.X, position.Y, position.Z)
                : blockInfo.Pos;
            if (boundingBox is not null && !Contains(boundingBox, blockPos)) continue;
            result.Add(new StructureBlockInfo(blockPos,
                StructureBlockTransforms.ApplyRotation(blockInfo.State, settings.Rotation), blockInfo.Nbt));
        }
        return result;
    }

    //FilterBlocks filters by block only, coordinates stay in-template by vanilla default
    public List<StructureBlockInfo> FilterBlocks(BlockPos position, StructurePlaceSettings settings, RegBlock block)
        => FilterBlocks(position, settings, block, true);

    //GetJigsaws returns the jigsaw blocks in the template, rotated and translated to the target position, maps to vanilla getJigsaws
    public List<JigsawBlockInfo> GetJigsaws(BlockPos position, Rotation rotation)
    {
        if (_palettes.Count == 0) return new List<JigsawBlockInfo>();
        var settings = new StructurePlaceSettings().SetRotation(rotation);
        var source = JigsawsOf(settings.GetRandomPalette(_palettes, position));
        //Sized up front since the result always holds one entry per cached jigsaw; growing instead cost a resize chain and
        //the discarded arrays on every placement attempt
        var result = new List<JigsawBlockInfo>(source.Count);
        foreach (var jigsaw in source)
        {
            var info = jigsaw.Info;
            result.Add(jigsaw.WithInfo(new StructureBlockInfo(
                CalculateRelativePosition(settings, info.Pos).Offset(position.X, position.Y, position.Z),
                StructureBlockTransforms.ApplyRotation(info.State, settings.Rotation), info.Nbt)));
        }
        return result;
    }

    //JigsawsOf returns the jigsaw blocks in a palette, skipping entries without nbt; vanilla throws a null pointer right here
    //Cached per palette: a palette is immutable once the template is loaded, so the scan and every JigsawBlockInfo.Of call
    //were being redone for each placement. A chunk landing on a large jigsaw structure spent seconds in here
    //ConditionalWeakTable so a dropped template takes its entries with it, and it is thread safe for the generation pool
    private static readonly ConditionalWeakTable<StructureTemplatePalette, List<JigsawBlockInfo>> JigsawCache = new();

    private static List<JigsawBlockInfo> JigsawsOf(StructureTemplatePalette palette)
        => JigsawCache.GetValue(palette, ScanJigsaws);

    //ScanJigsaws builds the cached list once per palette; callers only read it, they never mutate it
    private static List<JigsawBlockInfo> ScanJigsaws(StructureTemplatePalette palette)
    {
        var result = new List<JigsawBlockInfo>();
        foreach (var info in palette.Blocks)
        {
            if (info.State.Owner.Id != JigsawBlockId || info.Nbt is null) continue;
            result.Add(JigsawBlockInfo.Of(info));
        }
        return result;
    }

    //GetJointType reads the jigsaw block's joint type, falling back to the block facing when the joint field is missing, maps to vanilla getJointType
    public static JointType GetJointType(CompoundTag nbt, BlockState state)
    {
        var text = nbt.GetString("joint")?.Value;
        var parsed = text is null ? null : JointTypes.TryParse(text);
        return parsed ?? GetDefaultJointType(state);
    }

    //GetDefaultJointType horizontal facing means aligned, vertical means rollable, maps to the axis check of vanilla JigsawBlock.getFrontFacing
    public static JointType GetDefaultJointType(BlockState state)
    {
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name != "orientation" || entry.Value is not FrontAndTopEnum orientation) continue;
            return JointTypes.IsVerticalFront(orientation) ? JointType.Rollable : JointType.Aligned;
        }
        return JointType.Rollable;
    }

    //PlaceInWorld writes the template into the world with the settings, maps to vanilla placeInWorld
    //Returning false means the template is empty or the size is invalid
    public bool PlaceInWorld(WorldGenRegion level, BlockPos position, BlockPos referencePos,
        StructurePlaceSettings settings, RandomSource random)
    {
        if (_palettes.Count == 0) return false;
        var blockInfoList = settings.GetRandomPalette(_palettes, position).Blocks;
        if (Size.X < 1 || Size.Y < 1 || Size.Z < 1) return false;

        var boundingBox = settings.BoundingBox;
        var processed = ProcessBlockInfos(level, position, referencePos, settings, blockInfoList);
        foreach (var info in processed)
        {
            if (boundingBox is not null && !Contains(boundingBox, info.Pos)) continue;
            //The state transform is always mirror then rotate, the same order as the coordinate transform
            var state = StructureBlockTransforms.ApplyRotation(
                StructureBlockTransforms.ApplyMirror(info.State, settings.Mirror), settings.Rotation);
            level.SetBlockState(info.Pos.X, info.Pos.Y, info.Pos.Z, state);
            if (info.Nbt is null) continue;
            BlockEntitySink?.Invoke(info.Pos, state, info.Nbt);
        }
        //Entity spawning waits for the entity factory and mob spawn wiring to be ready; the template already parsed its entity list, pending stage 4
        return true;
    }

    //ProcessBlockInfos runs the processor chain block by block, maps to vanilla processBlockInfos
    //A null return from a processor drops the cell; finalizeProcessing runs in order after everything is processed
    public static List<StructureBlockInfo> ProcessBlockInfos(WorldGenRegion? level, BlockPos position,
        BlockPos referencePos, StructurePlaceSettings settings, IReadOnlyList<StructureBlockInfo> blockInfoList)
    {
        //Sized to the block table up front: each list takes one entry per block at most, and growing them cost a resize
        //chain plus the discarded arrays for every placement of every piece
        var originalBlockInfoList = new List<StructureBlockInfo>(blockInfoList.Count);
        var processedBlockInfoList = new List<StructureBlockInfo>(blockInfoList.Count);
        var processOnlyInCurrentChunk = true;
        foreach (var processor in settings.Processors)
        {
            if (!processor.EvaluatesEntirePieceState()) continue;
            processOnlyInCurrentChunk = false;
            break;
        }
        //Only a processor that writes into the tag it is handed needs the template nbt isolated; the others either pass
        //it through or return a fresh one, so copying here would be thrown away for every block of every placement
        var copyNbt = false;
        var processors = settings.Processors;
        for (var i = 0; i < processors.Count; i++)
        {
            if (!processors[i].ModifiesBlockEntityData) continue;
            copyNbt = true;
            break;
        }
        var chunkBox = settings.BoundingBox;
        foreach (var info in blockInfoList)
        {
            var relative = CalculateRelativePosition(settings, info.Pos);
            var blockPos = relative.Offset(position.X, position.Y, position.Z);
            if (processOnlyInCurrentChunk && chunkBox is not null && !Contains(chunkBox, blockPos)) continue;
            StructureBlockInfo? current = new StructureBlockInfo(blockPos, info.State,
                info.Nbt is null || !copyNbt ? info.Nbt : (CompoundTag)info.Nbt.Copy());
            foreach (var processor in settings.Processors)
            {
                if (current is null) break;
                current = processor.ProcessBlock(level, position, referencePos, info.Pos, current.Value, settings);
            }
            if (current is null) continue;
            processedBlockInfoList.Add(current.Value);
            originalBlockInfoList.Add(info);
        }
        foreach (var processor in settings.Processors)
        {
            processedBlockInfoList = processor
                .FinalizeProcessing(level, position, referencePos, originalBlockInfoList, processedBlockInfoList, settings)
                .ToList();
        }
        return processedBlockInfoList;
    }

    //Contains inclusive containment test, matching vanilla BoundingBox.isInside with both ends included
    private static bool Contains(BoundingBoxInt box, BlockPos pos)
        => pos.X >= box.MinX && pos.X <= box.MaxX
            && pos.Y >= box.MinY && pos.Y <= box.MaxY
            && pos.Z >= box.MinZ && pos.Z <= box.MaxZ;

    //JigsawBlockInfo info of one jigsaw block in a template, maps to vanilla StructureTemplate.JigsawBlockInfo
    //name/target/pool decide what it can connect to; the two priorities decide connection order
    //Also a value type: GetJigsaws rebuilds one per jigsaw block on every placement and the structural pass allocates
    //tens of thousands of them, so the list keeps them inline instead of one object per entry
    public readonly record struct JigsawBlockInfo(StructureBlockInfo Info, JointType JointType, Identifier Name,
        Identifier Pool, Identifier Target, int PlacementPriority, int SelectionPriority)
    {
        //Of decodes jigsaw info from block info and its nbt, missing fields fall back to the vanilla defaults
        public static JigsawBlockInfo Of(StructureBlockInfo info)
        {
            var nbt = info.Nbt!;
            return new JigsawBlockInfo(info, GetJointType(nbt, info.State),
                ReadIdentifier(nbt, "name"), ReadIdentifier(nbt, "pool"), ReadIdentifier(nbt, "target"),
                nbt.GetIntOr("placement_priority", 0), nbt.GetIntOr("selection_priority", 0));
        }

        //WithInfo replaces the block info, keeping the other fields
        public JigsawBlockInfo WithInfo(StructureBlockInfo info) => this with { Info = info };

        //ReadIdentifier reads an identifier field, defaulting to minecraft:empty
        private static Identifier ReadIdentifier(CompoundTag nbt, string key)
        {
            var text = nbt.GetString(key)?.Value;
            var id = text is null ? null : Identifier.TryParse(text);
            return id ?? Identifier.WithDefaultNamespace("empty");
        }
    }

    //ListInt returns the index-th item of an int list, defaulting to 0
    private static int ListInt(ListTag tag, int index)
        => index < tag.Count ? tag.GetInt(index)?.Value ?? 0 : 0;

    //ListDouble returns the index-th item of a double list, defaulting to 0
    private static double ListDouble(ListTag tag, int index)
        => index < tag.Count ? tag.GetDouble(index)?.Value ?? 0.0 : 0.0;
}

//JointType jigsaw joint type, maps to vanilla JigsawBlockEntity.JointType
//rollable lets the joint slide along the axis to align; aligned fixes it in place
public enum JointType
{
    Rollable,
    Aligned,
}

//JointTypes joint type name mapping and facing checks
public static class JointTypes
{
    //Name returns the serialization name, maps to vanilla getSerializedName
    public static string Name(JointType jointType) => jointType == JointType.Aligned ? "aligned" : "rollable";

    //TryParse parses by serialization name, returns null when invalid
    public static JointType? TryParse(string name) => name switch
    {
        "rollable" => JointType.Rollable,
        "aligned" => JointType.Aligned,
        _ => null,
    };

    //IsVerticalFront up/down facings are vertical, the other four sides are horizontal, maps to the axis check of the vanilla front direction
    public static bool IsVerticalFront(FrontAndTopEnum orientation) => orientation switch
    {
        FrontAndTopEnum.down_east or FrontAndTopEnum.down_north or FrontAndTopEnum.down_south
            or FrontAndTopEnum.down_west or FrontAndTopEnum.up_east or FrontAndTopEnum.up_north
            or FrontAndTopEnum.up_south or FrontAndTopEnum.up_west => true,
        _ => false,
    };
}
