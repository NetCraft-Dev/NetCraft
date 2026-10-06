using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using IntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.World.Level;

//WeatherCycle weather state machine, maps to vanilla ServerLevel.advanceWeatherCycle
//Rain and thunder each have a timer line; hitting zero flips the state and resamples the duration for the current state; the forced clear-weather countdown has the highest priority
public static class WeatherCycle
{
    //Timing constants align with the four UniformInts in vanilla ServerLevel
    public static readonly IntProvider RainDelay = UniformInt.Of(12000, 180000);
    public static readonly IntProvider RainDuration = UniformInt.Of(12000, 24000);
    public static readonly IntProvider ThunderDelay = UniformInt.Of(12000, 180000);
    public static readonly IntProvider ThunderDuration = UniformInt.Of(3600, 15600);

    //AdvanceCycle advance one tick of weather timers and target states, maps to the state section of vanilla advanceWeatherCycle
    //Callers should not invoke this when the advanceWeather rule is off; vanilla wraps the whole block in a rule check
    public static void AdvanceCycle(WeatherData data, RandomSource random)
    {
        var clearWeatherTime = data.ClearWeatherTime;
        var thunderTime = data.ThunderTime;
        var rainTime = data.RainTime;
        var thundering = data.Thundering;
        var raining = data.Raining;
        if (clearWeatherTime > 0)
        {
            //During the clear-weather countdown both timer lines are clamped to 0/1 and the rain/thunder states are flattened
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
                //When a timer line bottoms out, resample the next duration based on whether it is currently raining/thundering
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

    //AdvanceLevel advance one tick of rain and thunder levels, maps to the interpolation section of vanilla advanceWeatherCycle
    //Each tick steps 0.01 toward the target up to 1, so it takes around 20 ticks for a state change to become visible to players
    public static void AdvanceLevel(ServerLevel level, WeatherData data)
    {
        level.OThunderLevel = level.ThunderLevel;
        level.ThunderLevel = Mth.Clamp(level.ThunderLevel + (data.Thundering ? 0.01f : -0.01f), 0.0f, 1.0f);
        level.ORainLevel = level.RainLevel;
        level.RainLevel = Mth.Clamp(level.RainLevel + (data.Raining ? 0.01f : -0.01f), 0.0f, 1.0f);
    }
}
