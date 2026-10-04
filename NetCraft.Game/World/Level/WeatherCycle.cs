using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using IntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.World.Level;

//WeatherCycle 天气状态机对应原版 ServerLevel.advanceWeatherCycle
//雨与雷各一条计时线 归零即翻转状态并按当前状态重新采样时长 强制放晴倒计时优先级最高
public static class WeatherCycle
{
    //时序常量对齐原版 ServerLevel 的四个 UniformInt
    public static readonly IntProvider RainDelay = UniformInt.Of(12000, 180000);
    public static readonly IntProvider RainDuration = UniformInt.Of(12000, 24000);
    public static readonly IntProvider ThunderDelay = UniformInt.Of(12000, 180000);
    public static readonly IntProvider ThunderDuration = UniformInt.Of(3600, 15600);

    //AdvanceCycle 推进一刻的天气计时与目标状态 对应原版 advanceWeatherCycle 的状态段
    //advanceWeather 规则关闭时调用方不该调它 原版就是整段包在规则判断里
    public static void AdvanceCycle(WeatherData data, RandomSource random)
    {
        var clearWeatherTime = data.ClearWeatherTime;
        var thunderTime = data.ThunderTime;
        var rainTime = data.RainTime;
        var thundering = data.Thundering;
        var raining = data.Raining;
        if (clearWeatherTime > 0)
        {
            //放晴倒计时期间把两条计时线收到 0/1 雨雷状态一并压平
            clearWeatherTime--;
            thunderTime = thundering ? 0 : 1;
            rainTime = raining ? 0 : 1;
            thundering = false;
            raining = false;
        }
        else
        {
            if (thunderTime > 0)
            {
                thunderTime--;
                if (thunderTime == 0) thundering = !thundering;
            }
            else
            {
                //计时线见底时按当前是否在下雨/打雷采样下一段时长
                thunderTime = thundering ? ThunderDuration.Sample(random) : ThunderDelay.Sample(random);
            }
            if (rainTime > 0)
            {
                rainTime--;
                if (rainTime == 0) raining = !raining;
            }
            else
            {
                rainTime = raining ? RainDuration.Sample(random) : RainDelay.Sample(random);
            }
        }
        data.SetThunderTime(thunderTime);
        data.SetRainTime(rainTime);
        data.SetClearWeatherTime(clearWeatherTime);
        data.SetThundering(thundering);
        data.SetRaining(raining);
    }

    //AdvanceLevel 推进一刻的雨量与雷声等级 对应原版 advanceWeatherCycle 的渐变段
    //每刻向目标走 0.01 到 1 为止 所以切换状态到玩家看见要 20 刻左右
    public static void AdvanceLevel(ServerLevel level, WeatherData data)
    {
        level.OThunderLevel = level.ThunderLevel;
        level.ThunderLevel = Mth.Clamp(level.ThunderLevel + (data.Thundering ? 0.01f : -0.01f), 0.0f, 1.0f);
        level.ORainLevel = level.RainLevel;
        level.RainLevel = Mth.Clamp(level.RainLevel + (data.Raining ? 0.01f : -0.01f), 0.0f, 1.0f);
    }
}
