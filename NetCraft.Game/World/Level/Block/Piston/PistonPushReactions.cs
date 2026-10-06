using NetCraft.Registry.Enums;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonPushReactions block push reactions, maps to the PushReaction written per block in vanilla Blocks.java
//Vanilla attaches the reaction to block properties; here it comes from the push= section of the embedded block table and is injected into BlockBehaviour at registration
//Anything not listed is normal, matching the vanilla Properties.pushReaction default
internal static class PistonPushReactions
{
    //Of gets the push reaction for the state
    public static PushReaction Of(BlockState state) => state.Owner.PushReaction;
}
