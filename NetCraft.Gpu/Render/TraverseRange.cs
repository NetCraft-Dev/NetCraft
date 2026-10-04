namespace NetCraft.Gpu;

//TraverseRange 遍历范围对标原版支持 blur 分段渲染
public enum TraverseRange
{
    //All 遍历全部 stratum
    All,
    //BeforeBlur 遍历 blur 之前的 stratum
    BeforeBlur,
    //AfterBlur 遍历 blur 之后的 stratum
    AfterBlur
}
