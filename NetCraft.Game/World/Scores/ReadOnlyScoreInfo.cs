namespace NetCraft.Game.World.Scores;

//ReadOnlyScoreInfo read-only score view, maps to vanilla net.minecraft.world.scores.ReadOnlyScoreInfo
//Vanilla formatValue depends on NumberFormat; the number format system is not wired up in this project, so it is not provided yet
public interface ReadOnlyScoreInfo
{
    //Value score value, maps to vanilla value
    int Value();

    //IsLocked whether it is locked; locked scores reject changes from commands, maps to vanilla isLocked
    bool IsLocked();
}
