using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//WeatherData, weather state save data, maps to vanilla net.minecraft.world.level.saveddata.WeatherData
//Stored in data/minecraft/weather.dat, one per server; rain and thunder each carry remaining ticks and a current target state
public sealed class WeatherData : SavedData
{
    //TypeId, the save identifier, maps to the minecraft:weather of the vanilla SavedDataType
    private const string TypeId = "minecraft:weather";

    //Type, the SavedDataType factory
    public static readonly SavedDataType<WeatherData> Type = new WeatherDataType();

    public override string Id => TypeId;

    //ClearWeatherTime, remaining ticks of forced clear weather; while above zero both rain and thunder stop
    public int ClearWeatherTime { get; private set; }

    //RainTime, remaining ticks of the rain state; flips when it reaches zero
    public int RainTime { get; private set; }

    //ThunderTime, remaining ticks of the thunder state; flips when it reaches zero
    public int ThunderTime { get; private set; }

    //Raining, whether the weather target is raining
    public bool Raining { get; private set; }

    //Thundering, whether the weather target is thundering
    public bool Thundering { get; private set; }

    public void SetClearWeatherTime(int clearWeatherTime)
    {
        ClearWeatherTime = clearWeatherTime;
        SetDirty();
    }

    public void SetRainTime(int rainTime)
    {
        RainTime = rainTime;
        SetDirty();
    }

    public void SetThunderTime(int thunderTime)
    {
        ThunderTime = thunderTime;
        SetDirty();
    }

    public void SetRaining(bool raining)
    {
        Raining = raining;
        SetDirty();
    }

    public void SetThundering(bool thundering)
    {
        Thundering = thundering;
        SetDirty();
    }

    //Save field names align with the vanilla WeatherData codec
    public override CompoundTag Save(CompoundTag tag)
    {
        tag.PutInt("clear_weather_time", ClearWeatherTime);
        tag.PutInt("rain_time", RainTime);
        tag.PutInt("thunder_time", ThunderTime);
        tag.PutBoolean("raining", Raining);
        tag.PutBoolean("thundering", Thundering);
        return tag;
    }

    //WeatherDataType SavedDataType implementation; an empty tag is fresh weather, missing fields fall back to defaults
    private sealed class WeatherDataType : SavedDataType<WeatherData>
    {
        public string Id => TypeId;

        public WeatherData Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var data = new WeatherData();
            if (tag.Count == 0) return data;
            data.ClearWeatherTime = tag.GetIntValue("clear_weather_time");
            data.RainTime = tag.GetIntValue("rain_time");
            data.ThunderTime = tag.GetIntValue("thunder_time");
            data.Raining = tag.GetBooleanOr("raining", false);
            data.Thundering = tag.GetBooleanOr("thundering", false);
            return data;
        }
    }
}
