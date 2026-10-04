using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagLoader 标签加载器对应原版 net.minecraft.tags.TagLoader
//从数据包目录加载所有 TagFile 构建标签到元素集合的映射
//支持 replace 标志和标签引用递归构建
//实现 ITagLoader 让 TagManager 可以非泛型方式持有引用绕过泛型不变性
public sealed class TagLoader<T> : ITagLoader
{
    //directory 数据包标签目录路径（相对资源根如 data/<namespace>/tags/）
    private readonly string _directory;
    //elementGetter 元素 id 解析回调返回 Optional<T>
    private readonly Func<Identifier, Optional<T>> _elementGetter;
    //tagIdResolver 标签 id 到构建后元素集合的回调递归解析用
    private readonly Dictionary<Identifier, List<T>> _builtTags = new();

    public TagLoader(string directory, Func<Identifier, Optional<T>> elementGetter)
    {
        _directory = directory;
        _elementGetter = elementGetter;
    }

    //BuildSingle 构建单个 TagFile 的条目集合
    public List<T> BuildSingle(TagFile file)
    {
        //Log.Debug($"BuildSingle 入口 file={file}");
        var result = new List<T>();
        foreach (var entry in file.Entries)
        {
            entry.Build(_elementGetter, ResolveTag, result);
        }
        //Log.Debug($"BuildSingle 出口 result={result}");
        return result;
    }

    //ResolveTag 标签引用解析回调从已构建的 _builtTags 取
    private Optional<IEnumerable<T>> ResolveTag(Identifier tagId)
    {
        if (_builtTags.TryGetValue(tagId, out var values))
        {
            return Optional<IEnumerable<T>>.Of(values);
        }
        return Optional<IEnumerable<T>>.Empty();
    }

    //BuildAll 批量构建多个 TagFile 按 replace 标志合并
    //files 按 id -> List<TagFile> 组织同一 tag 多个数据包会合并
    public Dictionary<Identifier, List<T>> BuildAll(Dictionary<Identifier, List<TagFile>> files)
    {
        Log.Debug($"BuildAll entry files={files}");
        var result = new Dictionary<Identifier, List<T>>();
        foreach (var (tagId, fileList) in files)
        {
            var merged = new List<T>();
            //Log.Debug($"步骤1 构建标签 tagId={tagId} fileListCount={fileList.Count}");
            foreach (var file in fileList)
            {
                if (file.Replace)
                {
                    Log.Debug("Step 2 replace flag triggered, merged cleared");
                    merged.Clear();
                }
                merged.AddRange(BuildSingle(file));
            }
            _builtTags[tagId] = merged;
            result[tagId] = new List<T>(merged);
        }
        Log.Debug($"BuildAll exit result={result}");
        return result;
    }

    //LoadDirectory 加载目录下所有 TagFile 返回 id -> List<TagFile>
    //directoryPath 绝对路径如 data/<namespace>/tags/<category>
    public Dictionary<Identifier, List<TagFile>> LoadDirectory(string directoryPath)
    {
        Log.Debug($"LoadDirectory entry directoryPath={directoryPath}");
        var result = new Dictionary<Identifier, List<TagFile>>();
        if (!Directory.Exists(directoryPath))
        {
            Log.Debug($"LoadDirectory exit result={result}");
            return result;
        }
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directoryPath, file);
            var idPath = relative.Replace(Path.DirectorySeparatorChar, '/').Replace(".json", "");
            //解析 namespace:path 形式
            Identifier tagId;
            var slashIndex = idPath.IndexOf('/');
            if (slashIndex >= 0)
            {
                tagId = Identifier.FromNamespaceAndPath(idPath[..slashIndex], idPath[(slashIndex + 1)..]);
            }
            else
            {
                tagId = Identifier.WithDefaultNamespace(idPath);
            }
            var json = File.ReadAllText(file);
            var tagFile = TagFile.FromJson(json);
            if (!result.TryGetValue(tagId, out var list))
            {
                list = new List<TagFile>();
                result[tagId] = list;
            }
            list.Add(tagFile);
        }
        Log.Debug($"LoadDirectory exit result={result}");
        return result;
    }
}
