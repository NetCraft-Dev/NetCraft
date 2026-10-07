namespace NetCraft.Gpu;

//Divisor division helper, maps to vanilla com.mojang.math.Divisor
//Splits total into parts shares with the remainder going to the first few; each NextInt returns one share
//Used by GridLayout to divide the height/width of multi-row/column elements across rows and columns
public struct Divisor
{
    private readonly int _total;
    private readonly int _parts;
    private int _given;

    public Divisor(int total, int parts)
    {
        _total = total;
        _parts = parts;
        _given = 0;
    }

    //NextInt returns the next share; baseShare=total/parts with the remainder spread over the first remainder shares
    //The sum over all parts calls equals total
    public int NextInt()
    {
        if (_given >= _parts) return 0;
        int baseShare = _total / _parts;
        int remainder = _total % _parts;
        int result = baseShare + (_given < remainder ? 1 : 0);
        _given++;
        return result;
    }
}
