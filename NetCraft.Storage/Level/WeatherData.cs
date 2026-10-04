using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//WeatherData 天气状态存档对应原版 net.minecraft.world.level.saveddata.WeatherData
//存 data/minecraft/weather.dat 服务器级一份 雨与雷各带一条剩余刻数与当前目标状态
public sealed class WeatherData : SavedData
{
    //TypeId 存档标识对应原版 SavedDataType 的 minecraft:weather
    private const string TypeId = "minecraft:weather";

    //Type SavedDataType 工厂
    public static readonly SavedDataType<WeatherData> Type = new WeatherDataType();

    public override string Id => TypeId;

    //ClearWeatherTime 强制放晴剩余刻数 大于零时雨与雷都停
    public int ClearWeatherTime { get; private set; }

    //RainTime 下雨状态剩余刻数 归零即翻转
    public int RainTime { get; private set; }

    //ThunderTime 雷暴状态剩余刻数 归零即翻转
    public int ThunderTime { get; private set; }

    //Raining 天气目标是否下雨
    public bool Raining { get; private set; }

    //Thundering 天气目标是否雷暴
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

    //Save 字段名对齐原版 WeatherData codec
    public override CompoundTag Save(CompoundTag tag)
    {
        tag.PutInt("clear_weather_time", ClearWeatherTime);
        tag.PutInt("rain_time", RainTime);
        tag.PutInt("thunder_time", ThunderTime);
        tag.PutBoolean("raining", Raining);
        tag.PutBoolean("thundering", Thundering);
        return tag;
    }

    //WeatherDataType SavedDataType 实现 空标签即全新天气 字段缺失按默认值兜底
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
