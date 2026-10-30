namespace CitySim;

/// <summary>
/// Sound helper that never throws, so a missing sound event can't break gameplay.
/// </summary>
public static class Sfx
{
	public static void Play( string name, Vector3 position, float pitch = 1f, float volume = 1f )
	{
		try
		{
			var handle = Sound.Play( name, position );
			if (handle != null) { handle.Pitch = pitch; handle.Volume *= PlayerPreferences.EffectsVolume * Math.Clamp(volume,0f,1f); }
		}
		catch ( Exception )
		{
		}
	}
}
