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

//DataCommand /data command, maps to vanilla net.minecraft.server.commands.data.DataCommands
//Four action groups get/merge/remove/modify times three target kinds block/entity/storage gives 12 subtrees
//modify has three value sources: from (another target), string (take as string and slice), value (give a tag directly)
public static class DataCommand
{
    //ErrorMergeUnchanged writing leaves it the same, maps to vanilla ERROR_MERGE_UNCHANGED
    private static readonly SimpleCommandExceptionType ErrorMergeUnchanged =
        new(new LiteralMessage("the target data did not change"));

    private static readonly DynamicCommandExceptionType ErrorGetNotNumber =
        new(arg => new LiteralMessage($"the value on path {arg} is not a number"));

    private static readonly DynamicCommandExceptionType ErrorGetNonExistent =
        new(arg => new LiteralMessage($"no tag on path {arg}"));

    private static readonly SimpleCommandExceptionType ErrorMultipleTags =
        new(new LiteralMessage("the path hits multiple tags; give a unique path"));

    private static readonly DynamicCommandExceptionType ErrorExpectedObject =
        new(arg => new LiteralMessage($"expected a compound tag but got {arg}"));

    private static readonly DynamicCommandExceptionType ErrorExpectedValue =
        new(arg => new LiteralMessage($"that tag cannot be used as a string: {arg}"));

    private static readonly Dynamic2CommandExceptionType ErrorInvalidSubstring =
        new((start, end) => new LiteralMessage($"invalid substring range {start}..{end}"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var root = LiteralArgumentBuilder<CommandSourceStack>.Literal("data")
            .Requires(s => s.HasPermission(2));

        //One set each for target and source; they differ only in the locating argument names; source argument names are fixed as source and sourcePos like vanilla
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

    //AddTarget hangs the four subtrees merge/get/remove/modify on a target, maps to the loop in vanilla register
    //Action literals on the outside, the target on the inside, i.e. /data get block <pos> <path>
    private static void AddTarget<TPos>(LiteralArgumentBuilder<CommandSourceStack> root, TargetProvider<TPos> target,
        TargetProvider<BlockCoordinates> blockSource, TargetProvider<EntitySelector> entitySource,
        TargetProvider<Identifier> storageSource)
    {
        //merge <target> <nbt>
        var mergeNbt = RequiredArgumentBuilder<CommandSourceStack, CompoundTag>
            .Argument("nbt", CompoundTagArgument.CompoundTag());
        mergeNbt.Executes(context => MergeData(context, target.Access(context),
            CompoundTagArgument.GetCompoundTag(context, "nbt")));
        root.Then(Literal("merge").Then(target.Node(position => position.Then(mergeNbt))));

        //get <target> [<path> [<scale>]]
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

        //remove <target> <path>
        var removePath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>
            .Argument("path", NbtPathArgument.NbtPathArg());
        removePath.Executes(context => RemoveData(context, target.Access(context),
            NbtPathArgument.GetPath(context, "path")));
        root.Then(Literal("remove").Then(target.Node(position => position.Then(removePath))));

        //modify <target> <target path> <action> <source>
        //The target path node is built first, then actions hang on it; an action treats it as a parent for further subtrees
        var targetPathNode = RequiredArgumentBuilder<CommandSourceStack, NbtPath>
            .Argument("targetPath", NbtPathArgument.NbtPathArg());
        AddActions(targetPathNode, target, blockSource, entitySource, storageSource);
        root.Then(Literal("modify").Then(target.Node(position => position.Then(targetPathNode))));
    }

    private static LiteralArgumentBuilder<CommandSourceStack> Literal(string name)
        => LiteralArgumentBuilder<CommandSourceStack>.Literal(name);

    //AddActions hangs modify's five actions; each action then hangs the three value sources
    private static TParent AddActions<TParent, TPos>(TParent parent, TargetProvider<TPos> target,
        TargetProvider<BlockCoordinates> blockSource, TargetProvider<EntitySelector> entitySource,
        TargetProvider<Identifier> storageSource)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //insert <index> <source>; a negative index counts from the end like vanilla
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

    //AddSources hangs the value/from/string sources under an action node, maps to vanilla decorateModification
    private static TParent AddSources<TParent, TPos>(TParent parent, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<BlockCoordinates> blockSource,
        TargetProvider<EntitySelector> entitySource, TargetProvider<Identifier> storageSource)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //value <tag> gives an SNBT tag directly
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("value")
            .Then(RequiredArgumentBuilder<CommandSourceStack, Tag>.Argument("value", NbtTagArgument.NbtTag())
                .Executes(context => ManipulateData(context, target, manipulator,
                    new List<Tag> { NbtTagArgument.GetNbtTag(context, "value") }))));

        AddSourceBranches(parent, target, manipulator, blockSource);
        AddSourceBranches(parent, target, manipulator, entitySource);
        AddSourceBranches(parent, target, manipulator, storageSource);
        return parent;
    }

    //AddSourceBranches hangs the from and string subtrees of a source, maps to the two calls of vanilla sourceProvider.wrap
    private static void AddSourceBranches<TParent, TPos, TSrc>(TParent parent, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<TSrc> source)
        where TParent : ArgumentBuilder<CommandSourceStack, TParent>
    {
        //from <source> [<source path>]
        var fromPath = RequiredArgumentBuilder<CommandSourceStack, NbtPath>.Argument("sourcePath", NbtPathArgument.NbtPathArg());
        fromPath.Executes(context => ManipulateData(context, target, manipulator,
            NbtPathArgument.GetPath(context, "sourcePath").Get(source.Access(context).GetData())));
        var fromNode = source.Node(position => position
            .Executes(context => ManipulateData(context, target, manipulator, Singleton(source, context)))
            .Then(fromPath));
        parent.Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("from").Then(fromNode));

        //string <source> [<source path> [<start> [<end>]]]
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

    // ============ action implementations ============

    //ManipulateData performs the change and writes back; a change count of 0 errors like vanilla, maps to vanilla manipulateData
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

    //ManipulateString takes a value from the string source, slices it, then modifies
    private static int ManipulateString<TPos, TSrc>(CommandContext<CommandSourceStack> context, TargetProvider<TPos> target,
        DataManipulator manipulator, TargetProvider<TSrc> source, int? start, int? end)
    {
        var data = NbtPathArgument.GetPath(context, "sourcePath").Get(source.Access(context).GetData());
        return ManipulateData(context, target, manipulator, Stringify(data, start, end));
    }

    //MergeIntoPath merges the source's compound tag into the target path, maps to vanilla modify merge
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

    //RemoveData deletes the tags hit by the path and writes back, maps to vanilla removeData
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

    //MergeData merges the whole data and writes back, maps to vanilla mergeData
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

    //GetData queries the whole data; the reply is the formatted NBT, maps to vanilla getData
    private static int GetData(CommandContext<CommandSourceStack> context, IDataAccessor accessor)
    {
        var commandSource = Source(context);
        commandSource.SendSuccess(accessor.PrintSuccess(accessor.GetData()));
        return 1;
    }

    //GetData queries by path; the return value is rounded for numbers / size for lists and compounds / length for strings like vanilla
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

    //GetNumeric takes a number by path times a scale, maps to vanilla getNumeric
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

    //GetSingleTag gets the single tag hit by the path; more than one errors like vanilla
    private static Tag GetSingleTag(NbtPath path, IDataAccessor accessor)
    {
        var tags = path.Get(accessor.GetData());
        if (tags.Count > 1) throw ErrorMultipleTags.Create();
        return tags[0];
    }

    // ============ source handling ============

    //Singleton takes the source's whole data, maps to vanilla getSingletonSource
    private static List<Tag> Singleton<TSrc>(TargetProvider<TSrc> source,
        CommandContext<CommandSourceStack> context)
        => new() { source.Access(context).GetData() };

    //Stringify converts the source tag to a string tag, optionally sliced by start/end, maps to vanilla stringifyTagList
    //Negative indices count from the end like vanilla; out of range errors
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

    //GetAsText takes the tag's text form; only strings and numbers convert, maps to vanilla getAsText
    private static string GetAsText(Tag tag) => tag switch
    {
        StringTag text => text.Value,
        NumericTag number => number.ToString() ?? string.Empty,
        _ => throw ErrorExpectedValue.Create(tag),
    };

    private static int Offset(int index, int length) => index >= 0 ? index : length + index;

    // ============ target providers ============

    //DataManipulator modification action, maps to vanilla DataManipulator
    private delegate int DataManipulator(CommandContext<CommandSourceStack> context, CompoundTag target,
        NbtPath targetPath, List<Tag> source);

    //TargetProvider target provider, maps to vanilla DataCommands.DataProvider
    //TPos is the locating argument type: block coordinate/entity selector/storage id; the locating node is rebuilt each time like vanilla
    private sealed class TargetProvider<TPos>(
        string name,
        Func<RequiredArgumentBuilder<CommandSourceStack, TPos>> locator,
        Func<CommandContext<CommandSourceStack>, IDataAccessor> access)
    {
        public IDataAccessor Access(CommandContext<CommandSourceStack> context) => access(context);

        //Node builds the layer from <target name> <locating argument> rest, maps to vanilla wrap
        public LiteralArgumentBuilder<CommandSourceStack> Node(
            Func<RequiredArgumentBuilder<CommandSourceStack, TPos>, RequiredArgumentBuilder<CommandSourceStack, TPos>> rest)
            => LiteralArgumentBuilder<CommandSourceStack>.Literal(name).Then(rest(locator()));
    }

    //BlockProvider block entity target; errors when there is no block entity at the position
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

    //EntityProvider entity target; both players and level entities can be taken
    private static TargetProvider<EntitySelector> EntityProvider(string parameterName) => new(
        "entity",
        () => RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument(parameterName, EntityArgument.Entity()),
        context => new EntityDataAccessor(EntityArgument.GetSingleTarget(context, parameterName)));

    //StorageProvider command storage target; the locating argument is the storage id
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
