namespace CitySim;

/// <summary>Small per-device preferences, saved by the engine between sessions.</summary>
public static class PlayerPreferences
{
	const string Key="citysim.preferences.v1";
	public sealed record Data(int EffectsLevel=4,bool ReduceShake=false,bool ReduceFlashing=false,bool HintsDismissed=false,int UiScale=80);
	static Data current=new();
	public static Data Current => current;
	public static float UiScale => current.UiScale/100f;
	public static float EffectsVolume => current.EffectsLevel/4f;
	public static string EffectsLabel => new[] {"Muted","Low","Medium","High","Full"}[current.EffectsLevel];
	public static void Load()
	{
		try { current=Game.Cookies.Get(Key,new Data()) ?? new Data(); } catch (Exception) { current=new Data(); }
		current=current with {EffectsLevel=Math.Clamp(current.EffectsLevel,0,4),UiScale=Math.Clamp(current.UiScale == 0 ? 80 : current.UiScale,60,120)};
	}
	public static void Set(Data value)
	{
		current=value with {EffectsLevel=Math.Clamp(value.EffectsLevel,0,4),UiScale=Math.Clamp(value.UiScale,60,120)};
		try { Game.Cookies.Set(Key,current); } catch (Exception e) { Log.Warning($"Could not save City Sim preferences: {e.Message}"); }
	}
}
