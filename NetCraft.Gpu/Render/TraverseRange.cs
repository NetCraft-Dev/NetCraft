namespace NetCraft.Gpu;

//TraverseRange traversal range, maps to vanilla, supporting blur-segmented rendering
public enum TraverseRange
{
    //All traverses all strata
    All,
    //BeforeBlur traverses the strata before blur
    BeforeBlur,
    //AfterBlur traverses the strata after blur
    AfterBlur
}
