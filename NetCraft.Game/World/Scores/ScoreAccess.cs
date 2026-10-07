using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//ScoreAccess writable score view, maps to vanilla net.minecraft.world.scores.ScoreAccess
//Commands get it to read and write scores; the implementation obtained from a read-only objective blocks writes
public interface ScoreAccess
{
    //Get reads the current score, maps to vanilla get
    int Get();

    //Set writes the score, maps to vanilla set
    void Set(int value);

    //Add adds a delta and returns the new value, maps to vanilla add
    int Add(int amount)
    {
        var value = Get() + amount;
        Set(value);
        return value;
    }

    //Increment increments by one and returns the new value, maps to vanilla increment
    int Increment() => Add(1);

    //Reset resets the score to zero, maps to vanilla reset
    void Reset() => Set(0);

    //Locked whether the score is locked, maps to vanilla locked
    bool Locked();

    //Unlock unlocks the score, maps to vanilla unlock
    void Unlock();

    //Lock locks the score, maps to vanilla lock
    void Lock();

    //Display reads the custom display text, null when none, maps to vanilla display
    Component? Display();

    //Display writes the custom display text, maps to vanilla display
    void Display(Component? display);
}
