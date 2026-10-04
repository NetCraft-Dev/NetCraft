namespace NetCraft.Util.Parsing.Packrat;

//延迟异常工厂对应原版net.minecraft.util.parsing.packrat.DelayedException
//解析失败时不立即抛出而记录位置后由调用方按需抛出
//T对应Exception子类C#用Exception约束
public delegate Exception DelayedException<out T>(string contents, int position) where T : Exception;
