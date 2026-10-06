namespace NetCraft.Util.Parsing.Packrat;

//Deferred exception factory, maps to vanilla net.minecraft.util.parsing.packrat.DelayedException
//On parse failure it records the position instead of throwing immediately, letting the caller throw on demand
//T corresponds to an Exception subclass; C# constrains it to Exception
public delegate Exception DelayedException<out T>(string contents, int position) where T : Exception;
