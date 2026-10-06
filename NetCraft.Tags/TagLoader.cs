using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagLoader tag loader, maps to vanilla net.minecraft.tags.TagLoader
//Loads all TagFiles from the data pack directory and builds the tag-to-element mapping
//Supports the replace flag and recursive construction from tag references
//Implements ITagLoader so TagManager can hold a reference non-generically and work around generic invariance
public sealed class TagLoader<T> : ITagLoader
{
    //directory data pack tag directory path (relative to the resource root such as data/<namespace>/tags/)
    private readonly string _directory;
    //elementGetter resolves an element id, returns Optional<T>
    private readonly Func<Identifier, Optional<T>> _elementGetter;
    //tagIdResolver callback from a tag id to the built element collection, used for recursive resolution
    private readonly Dictionary<Identifier, List<T>> _builtTags = new();

    public TagLoader(string directory, Func<Identifier, Optional<T>> elementGetter)
    {
        _directory = directory;
        _elementGetter = elementGetter;
    }

    //BuildSingle builds the entry collection of a single TagFile
    public List<T> BuildSingle(TagFile file)
    {
        //Log.Debug($"BuildSingle entry file={file}");
        var result = new List<T>();
        foreach (var entry in file.Entries)
        {
            entry.Build(_elementGetter, ResolveTag, result);
        }
        //Log.Debug($"BuildSingle exit result={result}");
        return result;
    }

    //ResolveTag tag reference resolution callback, reads from the already built _builtTags
    private Optional<IEnumerable<T>> ResolveTag(Identifier tagId)
    {
        if (_builtTags.TryGetValue(tagId, out var values))
        {
            return Optional<IEnumerable<T>>.Of(values);
        }
        return Optional<IEnumerable<T>>.Empty();
    }

    //BuildAll builds multiple TagFiles in batch, merging according to the replace flag
    //files organized as id -> List<TagFile>, multiple data packs for the same tag are merged
    public Dictionary<Identifier, List<T>> BuildAll(Dictionary<Identifier, List<TagFile>> files)
    {
        Log.Debug($"BuildAll entry files={files}");
        var result = new Dictionary<Identifier, List<T>>();
        foreach (var (tagId, fileList) in files)
        {
            var merged = new List<T>();
            //Log.Debug($"Step 1 build tag tagId={tagId} fileListCount={fileList.Count}");
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

    //LoadDirectory loads every TagFile under a directory, returns id -> List<TagFile>
    //directoryPath absolute path such as data/<namespace>/tags/<category>
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
            //Parse the namespace:path form
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
