using CitySim.Ai;

namespace CitySim;

/// <summary>Setup preferences are separate from audio/display preferences and match rules.</summary>
public static class SoloSetupPreferences
{
    const string Key="citysim.solo-setup.v1";
    public static SoloSetupData Load()
    {
        SoloSetupData saved=null;
        try { saved=Game.Cookies.Get<SoloSetupData>(Key,null); }
        catch(Exception e) { Log.Warning($"Could not load game setup: {e.Message}"); }
        return SoloSetup.Normalize(saved,new Random().Next());
    }
    public static void Save(SoloSetupData value)
    {
        try { Game.Cookies.Set(Key,value); }
        catch(Exception e) { Log.Warning($"Could not save game setup: {e.Message}"); }
    }
}
