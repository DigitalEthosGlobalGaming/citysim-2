namespace CitySim;

/// <summary>Local tabletop feedback. Gains are relative to the saved effects volume.</summary>
public static class TabletopAudio
{
 static float lastCard = -100, lastMoney = -100;
 static void Play(string family, int variants, float volume, float pitch = 1f)
 {
  if (PlayerPreferences.EffectsVolume <= 0) return;
  Sfx.Play($"sounds/tabletop/{family}-{Game.Random.Int(1,variants)}.sound",Vector3.Zero,pitch,volume);
 }
 public static void Click() => Play("click",3,0.22f);
 public static void Switch() => Play("switch",1,0.24f);
 public static void Card()
 {
  if (Time.Now >= lastCard && Time.Now-lastCard < 0.10f) return;
  lastCard=Time.Now; Play("card-slide",3,0.32f);
 }
 public static void Place(float pitch = 1f) => Play("card-place",3,0.55f,pitch);
 public static void Purchase() => Play("chip-lay",3,0.5f);
 public static void Income(float pitch = 1f)
 {
  if (Time.Now >= lastMoney && Time.Now-lastMoney < 0.08f) return;
  lastMoney=Time.Now; Play("chips-stack",3,0.42f,pitch);
 }
 public static void Fan() => Play("card-fan",1,0.35f);
}
