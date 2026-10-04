using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Commands.Data;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//DataCommand /data 命令对应原版 net.minecraft.server.commands.data.DataCommands
//四组动作 get/merge/remove/modify 乘三类目标 block/entity/storage 共 12 棵子树
//modify 的值来源分三种 from(另一个目标) string(按字符串取并截取) value(直接给标签)
public static class DataCommand
{
    //ErrorMergeUnchanged 写完跟原来一样 对应原版 ERROR_MERGE_UNCHANGED
    private static readonly SimpleCommandExceptionType ErrorMergeUnchanged =
        new(new LiteralMessage("目标数据没有发生变化"));

    private static readonly DynamicCommandExceptionType ErrorGetNotNumber =
        new(arg => new LiteralMessage($"路径 {arg} 上的值不是数字"));

    private static readonly DynamicCommandExceptionType ErrorGetNonExistent =
        new(arg => new LiteralMessage($"路径 {arg} 上没有任何标签"));

    private static readonly SimpleCommandExceptionType ErrorMultipleTags =
        new(new LiteralMessage("该路径命中多个标签 请给出唯一路径"));

    private static readonly DynamicCommandExceptionType ErrorExpectedObject =
        new(arg => new LiteralMessage($"期望复合标签但拿到 {arg}"));

    private static readonly DynamicCommandExceptionType ErrorExpectedValue =
        new(arg => new LiteralMessage($"该标签不能当字符串使用: {arg}"));

    private static readonly Dynamic2CommandExceptionType ErrorInvalidSubstring =
        new((start, end) => new LiteralMessage($"子串范围不合法 {start}..{end}"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var root = LiteralArgumentBuilder<CommandSourceStack>.Literal("data")
            .Requires(s => s.HasPermission(2));

        //目标与来源各一套 差别只在定位参数名 来源参数名按原版固定 source 与 sourcePos
        var blockTarget = BlockProvider("blockPos");
        var entityTarget = EntityProvider("entity");
        var storageTarget = StorageProvider("storage");
        var blockSource = BlockProvider("sourcePos");
        var entitySource = EntityProvider("source");
        var storageSource = StorageProvider("source");

        AddTarget(root, blockTarget, blockSource, entitySource, storageSource);
        AddTarget(root, entityTarget, blockSource, entitySource, storageSource);
        AddTarget(root, storageTarget, blockSource, entitySource, storageSource);
        dispatcher.Register(root);
    }

    //AddTarget 为一个目标挂上 merge/get/remove/modify 四棵子树 对应原版 register 里的循环
    //动作字面量在外层 目标在内层 即 /data get block <位置> <路径>
    private static void AddTarget<TPos>(LiteralArgumentBuilder<CommandSourceStack> root, TargetProvider<TPos> target,
        TargetProvider<BlockCoordinates> blockSource, TargetProvider<EntitySelector> entitySource,
        TargetProvider<Identifier> storageSource)
    {
        //merge <目标> <nbt>
        var mergeNbt = RequiredArgumentBuilder<CommandSourceStack, CompoundTag>
            .Argument("nbt", CompoundTagArgument.CompoundTag());
        mergeNbt.Executes(context => MergeData(context, target.Access(context),
            CompoundTagArgument.GetCompoundTag(context, "nbt")));
        root.Then(Literal("merge").Then(target.Node(position => position.Then(mergeNbt))));

        //get <目标> [<路径> [<倍率>]]
        var getScale = RequiredArgumentBuilder<CommandSourceStack, double>
            .Argument("scale", DoubleArgumentType.DoubleArg());
        getScale.Executes(context => GetNumeric(context, target.Access(context),
            NbtPathArgument.GetPath(context, "path"), context.GetArgument<double>("scale")));

        var getPath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>
            .Argument("path", NbtPathArgument.NbtPathArg());
        getPath.Executes(context => GetData(context, target.Access(context), NbtPathArgument.GetPath(context, "path")));
        getPath.Then(getScale);

        root.Then(Literal("get").Then(target.Node(position => position
            .Executes(context => GetData(context, target.Access(context)))
            .Then(getPath))));

        //remove <目标> <路径>
        var removePath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>
            .Argument("path", NbtPathArgument.NbtPathArg());
        removePath.Executes(context => RemoveData(context, target.Access(context),
            NbtPathArgument.GetPath(context, "path")));
        root.Then(Literal("remove").Then(target.Node(position => position.Then(removePath))));

        //modify <目标> <目标路径> <动作> <来源>
        //目标路径节点先建好再挂动作 动作要把它当父节点继续挂子树
        var targetPathNode = RequiredArgumentBuilder<CommandSourceStack, NbtPath>
            .Argument("targetPath", NbtPathArgument.NbtPathArg());
        AddActions(targetPathNode, target, blockSource, entitySource, storageSource);
        root.Then(Literal("modify").Then(target.Node(position => position.Then(targetPathNode))));
    }

    private static LiteralArgumentBuilder<CommandSourceStack> Literal(string name)
        => LiteralArgumentBuilder<CommandSourceStack>.Literal(name);

    //AddActions 挂上 modify 的五个动作 每个动作下面再挂三种取值来源
    private static TParent AddActions<TParent, TPos>(TParent parent, TargetProvider<TPos> target,
        TargetProvider<BlockCoordinates> blockSource, TargetProvider<EntitySelector> entitySource,
        TargetProvider<Identifier> storageSource)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //insert <索引> <来源> 索引为负按原版从尾部算
        var insert = AddSources(
            RequiredArgumentBuilder<CommandSourceStack, int>.Argument("index", IntegerArgumentType.Integer()),
            target, (context, data, path, source) => path.Insert(context.GetArgument<int>("index"), data, source),
            blockSource, entitySource, storageSource);
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("insert").Then(insert));

        parent.Then(AddSources(LiteralArgumentBuilder<CommandSourceStack>.Literal("prepend"),
            target, (context, data, path, source) => path.Insert(0, data, source),
            blockSource, entitySource, storageSource));

        parent.Then(AddSources(LiteralArgumentBuilder<CommandSourceStack>.Literal("append"),
            target, (context, data, path, source) => path.Insert(-1, data, source),
            blockSource, entitySource, storageSource));

        parent.Then(AddSources(LiteralArgumentBuilder<CommandSourceStack>.Literal("set"),
            target, (context, data, path, source) => path.Set(data, source[^1]),
            blockSource, entitySource, storageSource));

        parent.Then(AddSources(LiteralArgumentBuilder<CommandSourceStack>.Literal("merge"),
            target, MergeIntoPath, blockSource, entitySource, storageSource));

        return parent;
    }

    //AddSources 在动作节点下挂 value/from/string 三类来源 对应原版 decorateModification
    private static TParent AddSources<TParent, TPos>(TParent parent, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<BlockCoordinates> blockSource,
        TargetProvider<EntitySelector> entitySource, TargetProvider<Identifier> storageSource)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //value <标签> 直接给一个 SNBT 标签
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("value")
            .Then(RequiredArgumentBuilder<CommandSourceStack, Tag>.Argument("value", NbtTagArgument.NbtTag())
                .Executes(context => ManipulateData(context, target, manipulator,
                    new List<Tag> { NbtTagArgument.GetNbtTag(context, "value") }))));

        AddSourceBranches(parent, target, manipulator, blockSource);
        AddSourceBranches(parent, target, manipulator, entitySource);
        AddSourceBranches(parent, target, manipulator, storageSource);
        return parent;
    }

    //AddSourceBranches 挂某个来源的 from 与 string 两棵子树 对应原版 sourceProvider.wrap 的两处调用
    private static void AddSourceBranches<TParent, TPos, TSrc>(TParent parent, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<TSrc> source)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //from <来源> [<来源路径>]
        var fromPath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>.Argument("sourcePath", NbtPathArgument.NbtPathArg());
        fromPath.Executes(context => ManipulateData(context, target, manipulator,
            NbtPathArgument.GetPath(context, "sourcePath").Get(source.Access(context).GetData())));
        var fromNode = source.Node(position => position
            .Executes(context => ManipulateData(context, target, manipulator, Singleton(source, context)))
            .Then(fromPath));
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("from").Then(fromNode));

        //string <来源> [<来源路径> [<起点> [<终点>]]]
        var stringEnd = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("end", IntegerArgumentType.Integer());
        stringEnd.Executes(context => ManipulateString(context, target, manipulator, source,
            context.GetArgument<int>("start"), context.GetArgument<int>("end")));

        var stringStart = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("start", IntegerArgumentType.Integer());
        stringStart.Executes(context => ManipulateString(context, target, manipulator, source,
            context.GetArgument<int>("start"), null)).Then(stringEnd);

        var stringPath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>.Argument("sourcePath", NbtPathArgument.NbtPathArg());
        stringPath.Executes(context => ManipulateString(context, target, manipulator, source, null, null))
            .Then(stringStart);

        var stringNode = source.Node(position => position
            .Executes(context => ManipulateData(context, target, manipulator,
                Stringify(Singleton(source, context), null, null)))
            .Then(stringPath));
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("string").Then(stringNode));
    }

    // ============ 动作实现 ============

    //ManipulateData 执行修改并写回 改动数为 0 按原版报错 对应原版 manipulateData
    private static int ManipulateData<TPos>(CommandContext<CommandSourceStack> context, TargetProvider<TPos> target,
        DataManipulator manipulator, List<Tag> source)
    {
        var commandSource = Source(context);
        var accessor = target.Access(context);
        var targetPath = NbtPathArgument.GetPath(context, "targetPath");
        var data = accessor.GetData();
        var result = manipulator(context, data, targetPath, source);
        if (result == 0) throw ErrorMergeUnchanged.Create();
        accessor.SetData(data);
        commandSource.SendSuccess(accessor.ModifiedSuccess);
        return result;
    }

    //ManipulateString 按 string 来源取值并做子串截取后再修改
    private static int ManipulateString<TPos, TSrc>(CommandContext<CommandSourceStack> context, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<TSrc> source, int? start, int? end)
    {
        var data = NbtPathArgument.GetPath(context, "sourcePath").Get(source.Access(context).GetData());
        return ManipulateData(context, target, manipulator, Stringify(data, start, end));
    }

    //MergeIntoPath 把来源里的复合标签合并进目标路径 对应原版 modify merge
    private static int MergeIntoPath(CommandContext<CommandSourceStack> context, CompoundTag target,
        NbtPath targetPath, List<Tag> source)
    {
        var combined = new CompoundTag();
        foreach (var tag in source)
        {
            if (NbtPath.IsTooDeep(tag, 0)) throw NbtPath.ErrorDataTooDeep.Create();
            if (tag is not CompoundTag compound) throw ErrorExpectedObject.Create(tag);
            combined.Merge(compound);
        }

        var targets = targetPath.GetOrCreate(target, () => new CompoundTag());
        var changedCount = 0;
        foreach (var tag in targets)
        {
            if (tag is not CompoundTag targetObject) throw ErrorExpectedObject.Create(tag);
            var original = (CompoundTag)targetObject.Copy();
            targetObject.Merge(combined);
            if (!NbtUtils.AreEqual(original, targetObject)) changedCount++;
        }
        return changedCount;
    }

    //RemoveData 删除路径命中的标签并写回 对应原版 removeData
    private static int RemoveData(CommandContext<CommandSourceStack> context, IDataAccessor accessor, NbtPath path)
    {
        var commandSource = Source(context);
        var data = accessor.GetData();
        var count = path.Remove(data);
        if (count == 0) throw ErrorMergeUnchanged.Create();
        accessor.SetData(data);
        commandSource.SendSuccess(accessor.ModifiedSuccess);
        return count;
    }

    //MergeData 整份合并并写回 对应原版 mergeData
    private static int MergeData(CommandContext<CommandSourceStack> context, IDataAccessor accessor, CompoundTag nbt)
    {
        var commandSource = Source(context);
        var old = accessor.GetData();
        if (NbtPath.IsTooDeep(nbt, 0)) throw NbtPath.ErrorDataTooDeep.Create();
        var result = old.Copy();
        ((CompoundTag)result).Merge(nbt);
        if (NbtUtils.AreEqual(old, result)) throw ErrorMergeUnchanged.Create();
        accessor.SetData((CompoundTag)result);
        commandSource.SendSuccess(accessor.ModifiedSuccess);
        return 1;
    }

    //GetData 整份查询 回执是格式化后的 NBT 对应原版 getData
    private static int GetData(CommandContext<CommandSourceStack> context, IDataAccessor accessor)
    {
        var commandSource = Source(context);
        commandSource.SendSuccess(accessor.PrintSuccess(accessor.GetData()));
        return 1;
    }

    //GetData 按路径查询 返回值按原版取数字取整/列表与复合大小/字符串长度
    private static int GetData(CommandContext<CommandSourceStack> context, IDataAccessor accessor, NbtPath path)
    {
        var commandSource = Source(context);
        var tag = GetSingleTag(path, accessor);
        var length = tag switch
        {
            NumericTag number => (int)Math.Floor(number.AsNumber()?.DoubleValue() ?? 0),
            ListTag list => list.Count,
            CompoundTag compound => compound.Count,
            StringTag text => text.Value.Length,
            _ => throw ErrorGetNonExistent.Create(path.AsString()),
        };
        commandSource.SendSuccess(accessor.PrintSuccess(tag));
        return length;
    }

    //GetNumeric 按路径取数字乘倍率 对应原版 getNumeric
    private static int GetNumeric(CommandContext<CommandSourceStack> context, IDataAccessor accessor,
        NbtPath path, double scale)
    {
        var commandSource = Source(context);
        var tag = GetSingleTag(path, accessor);
        if (tag is not NumericTag number) throw ErrorGetNotNumber.Create(path.AsString());
        var value = (int)Math.Floor((number.AsNumber()?.DoubleValue() ?? 0) * scale);
        commandSource.SendSuccess(accessor.PrintSuccess(path, scale, value));
        return value;
    }

    //GetSingleTag 取路径命中的唯一标签 命中多个按原版报错
    private static Tag GetSingleTag(NbtPath path, IDataAccessor accessor)
    {
        var tags = path.Get(accessor.GetData());
        if (tags.Count > 1) throw ErrorMultipleTags.Create();
        return tags[0];
    }

    // ============ 来源处理 ============

    //Singleton 取来源的整份数据 对应原版 getSingletonSource
    private static List<Tag> Singleton<TSrc>(TargetProvider<TSrc> source,
        CommandContext<CommandSourceStack> context)
        => new() { source.Access(context).GetData() };

    //Stringify 把来源标签转成字符串标签 可再按起止下标截取 对应原版 stringifyTagList
    //负数下标按原版从尾部算 越界报错
    private static List<Tag> Stringify(List<Tag> source, int? start, int? end)
    {
        var result = new List<Tag>(source.Count);
        foreach (var tag in source)
        {
            var text = GetAsText(tag);
            if (start is null)
            {
                result.Add(new StringTag(text));
                continue;
            }
            var from = Offset(start.Value, text.Length);
            var to = end is null ? text.Length : Offset(end.Value, text.Length);
            if (from < 0 || to > text.Length || from > to)
                throw ErrorInvalidSubstring.Create(from, to);
            result.Add(new StringTag(text[from..to]));
        }
        return result;
    }

    //GetAsText 取标签的文本形式 只有字符串与数字能转 对应原版 getAsText
    private static string GetAsText(Tag tag) => tag switch
    {
        StringTag text => text.Value,
        NumericTag number => number.ToString() ?? string.Empty,
        _ => throw ErrorExpectedValue.Create(tag),
    };

    private static int Offset(int index, int length) => index >= 0 ? index : length + index;

    // ============ 目标提供者 ============

    //DataManipulator 修改动作 对应原版 DataManipulator
    private delegate int DataManipulator(CommandContext<CommandSourceStack> context, CompoundTag target,
        NbtPath targetPath, List<Tag> source);

    //TargetProvider 目标提供者 对应原版 DataCommands.DataProvider
    //TPos 是定位参数类型 方块坐标/实体选择器/存储 id 定位节点每次新建与原本一致
    private sealed class TargetProvider<TPos>(
        string name,
        Func<RequiredArgumentBuilder<CommandSourceStack, TPos>> locator,
        Func<CommandContext<CommandSourceStack>, IDataAccessor> access)
    {
        public IDataAccessor Access(CommandContext<CommandSourceStack> context) => access(context);

        //Node 生成 <目标名> <定位参数> rest 拼出的一层 对应原版 wrap
        public LiteralArgumentBuilder<CommandSourceStack> Node(
            Func<RequiredArgumentBuilder<CommandSourceStack, TPos>, RequiredArgumentBuilder<CommandSourceStack, TPos>> rest)
            => LiteralArgumentBuilder<CommandSourceStack>.Literal(name).Then(rest(locator()));
    }

    //BlockProvider 方块实体目标 位置没有方块实体时报错
    private static TargetProvider<BlockCoordinates> BlockProvider(string parameterName) => new(
        "block",
        () => RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument(parameterName, BlockPosArgument.BlockPos()),
        context =>
        {
            var source = Source(context);
            var pos = BlockPosArgument.GetBlockPos(context, parameterName);
            if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
                throw BlockDataAccessor.ErrorNotBlockEntity.Create();
            if (source.Server.BlockEntities.Get(pos) is not { } entity)
                throw BlockDataAccessor.ErrorNotBlockEntity.Create();
            return new BlockDataAccessor(level, source.Server.PlayerList, entity, pos);
        });

    //EntityProvider 实体目标 玩家与关卡实体都能取
    private static TargetProvider<EntitySelector> EntityProvider(string parameterName) => new(
        "entity",
        () => RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument(parameterName, EntityArgument.Entity()),
        context => new EntityDataAccessor(EntityArgument.GetSingleTarget(context, parameterName)));

    //StorageProvider 命令存储目标 定位参数是存储 id
    private static TargetProvider<Identifier> StorageProvider(string parameterName) => new(
        "storage",
        () => RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument(parameterName, IdentifierArgument.Id()),
        context =>
        {
            var source = Source(context);
            return new StorageDataAccessor(source.Server.CommandStorage,
                IdentifierArgument.GetId(context, parameterName));
        });

    private static ServerCommandSource Source(CommandContext<CommandSourceStack> context)
        => (ServerCommandSource)context.GetSource();
}
