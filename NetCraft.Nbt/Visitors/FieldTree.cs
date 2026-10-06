using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//Field selection tree. Mirrors vanilla net.minecraft.nbt.visitors.FieldTree (a Record).
//Holds field selection rules organized by depth: SelectedFields is what to select at the current depth
//and FieldsToRecurse is the subtrees to recurse into.
public sealed class FieldTree
{
    //Current depth (the root is 1).
    public int Depth { get; }

    //Fields to keep at the current depth (name → type).
    public Dictionary<string, TagType> SelectedFields { get; }

    //Subtrees to recurse into (name → child FieldTree).
    public Dictionary<string, FieldTree> FieldsToRecurse { get; }

    private FieldTree(int depth)
    {
        Depth = depth;
        SelectedFields = new Dictionary<string, TagType>();
        FieldsToRecurse = new Dictionary<string, FieldTree>();
    }

    //Create the root FieldTree (depth=1).
    public static FieldTree CreateRoot() => new(1);

    //Add one field selection rule.
    //When the field path is longer than the current depth, recurse into the matching subtree; otherwise select it as a field at the current depth.
    public void AddEntry(FieldSelector field)
    {
        if (Depth <= field.Path.Count)
        {
            // Deeper path left, recurse into the subtree
            var key = field.Path[Depth - 1];
            if (!FieldsToRecurse.TryGetValue(key, out var child))
            {
                child = new FieldTree(Depth + 1);
                FieldsToRecurse[key] = child;
            }
            child.AddEntry(field);
        }
        else
        {
            // Path ends here, select it at this depth
            SelectedFields[field.Name] = field.Type;
        }
    }

    //Check whether the field with the given type and name is selected (the type must match exactly).
    public bool IsSelected(TagType type, string id)
        => SelectedFields.TryGetValue(id, out var t) && t == type;
}

