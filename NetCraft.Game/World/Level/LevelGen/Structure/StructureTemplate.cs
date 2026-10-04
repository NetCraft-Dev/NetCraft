using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using FrontAndTopEnum = NetCraft.Registry.Enums.FrontAndTop;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplate 结构模板 对应原版 net.minecraft.world.level.levelgen.structure.templatesystem.StructureTemplate
//一份 NBT 模板 = 尺寸 + 若干调色板 + 实体表 放置时按镜像与旋转把方块写进世界
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

    //JigsawBlockId 拼图方块的注册名 拼图收集按它筛方块
    public static readonly Identifier JigsawBlockId = Identifier.WithDefaultNamespace("jigsaw");

    private readonly List<StructureTemplatePalette> _palettes = new();
    private readonly List<StructureEntityInfo> _entityInfoList = new();

    //BlockEntitySink 方块实体接收者 区块生成期由 Game 层注入 未注入时带 nbt 的方块只写状态
    public Action<BlockPos, BlockState, CompoundTag>? BlockEntitySink { get; set; }

    public Vec3i Size { get; private set; } = Vec3i.Zero;

    public string Author { get; private set; } = "?";

    public IReadOnlyList<StructureTemplatePalette> Palettes => _palettes;

    public IReadOnlyList<StructureEntityInfo> EntityInfoList => _entityInfoList;

    //GetSize 旋转后的占用尺寸 90 度旋转会让 X 与 Z 互换
    public Vec3i GetSize(Rotation rotation)
        => rotation is Rotation.Clockwise90 or Rotation.Counterclockwise90
            ? new Vec3i(Size.Z, Size.Y, Size.X)
            : Size;

    //Load 读取模板 NBT palettes 存在时优先 否则退回单个 palette
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

        //实体表缺失 nbt 的整条丢弃 与原版一致
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

    //LoadPalette 解一个调色板与它对应的方块列表
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
            //调色板索引越界按空气处理 与原版 stateFor 的越界分支一致
            var state = index >= 0 && index < states.Count ? states[index] : Blocks.AIR.DefaultBlockState;
            var info = new StructureBlockInfo(pos, state, blockTag.GetCompound(BlockTagNbt));
            if (info.Nbt is not null) blockEntities.Add(info);
            else if (info.State.Owner.CanOcclude) fullBlocks.Add(info);
            else otherBlocks.Add(info);
        }
        _palettes.Add(new StructureTemplatePalette(BuildInfoList(fullBlocks, otherBlocks, blockEntities)));
    }

    //BuildInfoList 三段各自按 y/x/z 排序后拼接 实体方块排最后避免方块实体先于支撑方块出现
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

    //ReadBlockState 读一个方块状态 NBT 块 对应原版 NbtUtils.readBlockState
    //方块名未知返回空气 属性名或属性值非法时保留当前状态不报错
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

    //Transform 把模板内坐标按镜像与旋转变到目标坐标 对应原版 StructureTemplate.transform
    //恒为先镜像后旋转 顺序颠倒结果会错
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

    //CalculateRelativePosition 取模板内坐标变换后的相对坐标 对应原版 calculateRelativePosition
    public static BlockPos CalculateRelativePosition(StructurePlaceSettings settings, BlockPos pos)
        => Transform(pos, settings.Mirror, settings.Rotation, settings.RotationPivot);

    //GetZeroPositionWithTransform 求模板原点落在哪 对应原版 getZeroPositionWithTransform
    //尺寸减一在镜像侧参与 少了它镜像后的结构会整体偏一格
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

    //GetBoundingBox 模板按设置放置后占据的整数包围盒 对应原版 getBoundingBox
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

    //FilterBlocks 按方块筛模板里的方块 对应原版 filterBlocks
    //absolute 为假时坐标留在模板内 为真时按设置旋转平移到目标位置
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

    //FilterBlocks 只按方块筛 坐标按原版默认留在模板内
    public List<StructureBlockInfo> FilterBlocks(BlockPos position, StructurePlaceSettings settings, RegBlock block)
        => FilterBlocks(position, settings, block, true);

    //GetJigsaws 取模板里的拼图方块 坐标按旋转平移到目标位置 对应原版 getJigsaws
    public List<JigsawBlockInfo> GetJigsaws(BlockPos position, Rotation rotation)
    {
        if (_palettes.Count == 0) return new List<JigsawBlockInfo>();
        var settings = new StructurePlaceSettings().SetRotation(rotation);
        var result = new List<JigsawBlockInfo>();
        foreach (var jigsaw in JigsawsOf(settings.GetRandomPalette(_palettes, position)))
        {
            var info = jigsaw.Info;
            result.Add(jigsaw.WithInfo(new StructureBlockInfo(
                CalculateRelativePosition(settings, info.Pos).Offset(position.X, position.Y, position.Z),
                StructureBlockTransforms.ApplyRotation(info.State, settings.Rotation), info.Nbt)));
        }
        return result;
    }

    //JigsawsOf 取调色板里的拼图方块 缺 nbt 的条目跳过 原版此处直接抛空指针
    private static List<JigsawBlockInfo> JigsawsOf(StructureTemplatePalette palette)
    {
        var result = new List<JigsawBlockInfo>();
        foreach (var info in palette.Blocks)
        {
            if (info.State.Owner.Id != JigsawBlockId || info.Nbt is null) continue;
            result.Add(JigsawBlockInfo.Of(info));
        }
        return result;
    }

    //GetJointType 读拼图方块的关节类型 缺 joint 字段时按方块朝向决定 对应原版 getJointType
    public static JointType GetJointType(CompoundTag nbt, BlockState state)
    {
        var text = nbt.GetString("joint")?.Value;
        var parsed = text is null ? null : JointTypes.TryParse(text);
        return parsed ?? GetDefaultJointType(state);
    }

    //GetDefaultJointType 朝向水平用对齐 竖直用可滚动 对应原版 JigsawBlock.getFrontFacing 的轴判定
    public static JointType GetDefaultJointType(BlockState state)
    {
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name != "orientation" || entry.Value is not FrontAndTopEnum orientation) continue;
            return JointTypes.IsVerticalFront(orientation) ? JointType.Rollable : JointType.Aligned;
        }
        return JointType.Rollable;
    }

    //PlaceInWorld 把模板按设置写进世界 对应原版 placeInWorld
    //返回 false 表示模板为空或尺寸非法
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
            //状态变换恒为先镜像后旋转 与坐标变换同一顺序
            var state = StructureBlockTransforms.ApplyRotation(
                StructureBlockTransforms.ApplyMirror(info.State, settings.Mirror), settings.Rotation);
            level.SetBlockState(info.Pos.X, info.Pos.Y, info.Pos.Z, state);
            if (info.Nbt is null) continue;
            BlockEntitySink?.Invoke(info.Pos, state, info.Nbt);
        }
        //实体生成要等实体工厂与刷怪接线就绪 模板已解析好实体表待阶段 4 接入
        return true;
    }

    //ProcessBlockInfos 逐块跑处理器链 对应原版 processBlockInfos
    //处理器返回 null 即丢弃该格 全部处理完再依次做 finalizeProcessing
    public static List<StructureBlockInfo> ProcessBlockInfos(WorldGenRegion? level, BlockPos position,
        BlockPos referencePos, StructurePlaceSettings settings, IReadOnlyList<StructureBlockInfo> blockInfoList)
    {
        var originalBlockInfoList = new List<StructureBlockInfo>();
        var processedBlockInfoList = new List<StructureBlockInfo>();
        var processOnlyInCurrentChunk = true;
        foreach (var processor in settings.Processors)
        {
            if (!processor.EvaluatesEntirePieceState()) continue;
            processOnlyInCurrentChunk = false;
            break;
        }
        var chunkBox = settings.BoundingBox;
        foreach (var info in blockInfoList)
        {
            var relative = CalculateRelativePosition(settings, info.Pos);
            var blockPos = relative.Offset(position.X, position.Y, position.Z);
            if (processOnlyInCurrentChunk && chunkBox is not null && !Contains(chunkBox, blockPos)) continue;
            StructureBlockInfo? current = new StructureBlockInfo(blockPos, info.State,
                info.Nbt is null ? null : (CompoundTag)info.Nbt.Copy());
            foreach (var processor in settings.Processors)
            {
                if (current is null) break;
                current = processor.ProcessBlock(level, position, referencePos, info.Pos, current, settings);
            }
            if (current is null) continue;
            processedBlockInfoList.Add(current);
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

    //Contains 闭区间判定 原版 BoundingBox.isInside 两端都含
    private static bool Contains(BoundingBoxInt box, BlockPos pos)
        => pos.X >= box.MinX && pos.X <= box.MaxX
            && pos.Y >= box.MinY && pos.Y <= box.MaxY
            && pos.Z >= box.MinZ && pos.Z <= box.MaxZ;

    //JigsawBlockInfo 模板里一个拼图方块的信息 对应原版 StructureTemplate.JigsawBlockInfo
    //name/target/pool 决定它能和谁连 两个优先级决定连接顺序
    public sealed record JigsawBlockInfo(StructureBlockInfo Info, JointType JointType, Identifier Name,
        Identifier Pool, Identifier Target, int PlacementPriority, int SelectionPriority)
    {
        //Of 从方块信息与它的 nbt 解出拼图信息 缺字段按原版默认值
        public static JigsawBlockInfo Of(StructureBlockInfo info)
        {
            var nbt = info.Nbt!;
            return new JigsawBlockInfo(info, GetJointType(nbt, info.State),
                ReadIdentifier(nbt, "name"), ReadIdentifier(nbt, "pool"), ReadIdentifier(nbt, "target"),
                nbt.GetIntOr("placement_priority", 0), nbt.GetIntOr("selection_priority", 0));
        }

        //WithInfo 换掉方块信息 其余字段保留
        public JigsawBlockInfo WithInfo(StructureBlockInfo info) => this with { Info = info };

        //ReadIdentifier 读一个标识符字段 缺省按 minecraft:empty
        private static Identifier ReadIdentifier(CompoundTag nbt, string key)
        {
            var text = nbt.GetString(key)?.Value;
            var id = text is null ? null : Identifier.TryParse(text);
            return id ?? Identifier.WithDefaultNamespace("empty");
        }
    }

    //ListInt 取整数列表第 index 项 缺项按 0
    private static int ListInt(ListTag tag, int index)
        => index < tag.Count ? tag.GetInt(index)?.Value ?? 0 : 0;

    //ListDouble 取浮点列表第 index 项 缺项按 0
    private static double ListDouble(ListTag tag, int index)
        => index < tag.Count ? tag.GetDouble(index)?.Value ?? 0.0 : 0.0;
}

//JointType 拼图关节类型 对应原版 JigsawBlockEntity.JointType
//rollable 连接处能沿轴滚动对齐 aligned 固定对齐
public enum JointType
{
    Rollable,
    Aligned,
}

//JointTypes 关节类型的名字映射与朝向判定
public static class JointTypes
{
    //Name 取序列化名 对应原版 getSerializedName
    public static string Name(JointType jointType) => jointType == JointType.Aligned ? "aligned" : "rollable";

    //TryParse 按序列化名解析 非法返回 null
    public static JointType? TryParse(string name) => name switch
    {
        "rollable" => JointType.Rollable,
        "aligned" => JointType.Aligned,
        _ => null,
    };

    //IsVerticalFront 朝向朝上下的是竖直 其余四条边是水平 对应原版 front 方向的轴判定
    public static bool IsVerticalFront(FrontAndTopEnum orientation) => orientation switch
    {
        FrontAndTopEnum.down_east or FrontAndTopEnum.down_north or FrontAndTopEnum.down_south
            or FrontAndTopEnum.down_west or FrontAndTopEnum.up_east or FrontAndTopEnum.up_north
            or FrontAndTopEnum.up_south or FrontAndTopEnum.up_west => true,
        _ => false,
    };
}
