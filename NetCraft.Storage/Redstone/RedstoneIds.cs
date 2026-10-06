using NetCraft.Registry;

namespace NetCraft.Storage.Redstone;

//RedstoneIds, the registry names referenced by redstone-component checks
//Vanilla relies on static references like Blocks.REDSTONE_BLOCK and Blocks.REDSTONE_WIRE
//NC's redstone blocks live in the Game layer; Storage only knows registry names, so all checks go through here
public static class RedstoneIds
{
    //Wire, redstone wire; both power reads and lock checks must recognize it
    public static readonly Identifier Wire = Identifier.WithDefaultNamespace("redstone_wire");

    //Block, redstone block; it always gives 15 in control input
    public static readonly Identifier Block = Identifier.WithDefaultNamespace("redstone_block");

    //Repeater; redstone wire connects to both of its ports
    public static readonly Identifier Repeater = Identifier.WithDefaultNamespace("repeater");

    //Observer; redstone wire connects only to its output side, landed in P2-6
    public static readonly Identifier Observer = Identifier.WithDefaultNamespace("observer");

    //Hopper; redstone wire can sit on top of it, unlike ordinary support blocks it must be recognized separately
    public static readonly Identifier Hopper = Identifier.WithDefaultNamespace("hopper");

    //TrackedNames, components to track when diagnosing redstone circuits; the whole button and pressure plate families are recognized by suffix
    private static readonly HashSet<string> TrackedNames = new()
    {
        "redstone_wire", "redstone_block", "redstone_torch", "redstone_wall_torch",
        "repeater", "comparator", "lever", "observer", "target", "lightning_rod",
        "tripwire_hook", "tripwire", "daylight_detector",
    };

    //IsRedstoneComponent, whether the block is a redstone component in the circuit, used only to filter update logs
    public static bool IsRedstoneComponent(Identifier id)
        => TrackedNames.Contains(id.Path)
           || id.Path.EndsWith("_button")
           || id.Path.EndsWith("_pressure_plate");
}
