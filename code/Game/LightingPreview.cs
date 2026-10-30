namespace CitySim;

/// <summary>Local tabletop lighting with session-only comparison controls.</summary>
public sealed class LightingPreview
{
 readonly Tonemapping tone;
 readonly Bloom bloom;
 readonly DirectionalLight sun;
 readonly AmbientLight fill;
 readonly GameObject rig;
 readonly (DirectionalLight Light,bool Enabled)[] sceneSuns;
 readonly Tonemapping.TonemappingMode originalMode;
 readonly float originalExposure, originalBloom;
 readonly bool originalToneEnabled, originalBloomEnabled, originalAutoExposure;
 readonly Color originalSky, originalSun;
 readonly Rotation originalSunRotation;
 static readonly int[] SunLevels = { 0, 50, 75, 100, 150, 200, 300 };
 static readonly int[] SunDirections = { 0, 45, 90, 135, 180, 225, 270, 315 };
 int sunLevel=3, sunDirection;
 public string SunBrightness => $"{SunLevels[sunLevel]}%";
 public string SunDirection => sunDirection==0 ? "Default" : $"+{SunDirections[sunDirection]}°";
 public int Index { get; private set; }
 static readonly string[] Names = { "Boardgame", "Original", "Brighter", "Bright sky", "ACES", "Linear", "No tone map / bloom" };
 public string Name => Names[Index];
 public LightingPreview(Scene scene)
 {
  tone=scene.GetAllComponents<Tonemapping>().FirstOrDefault();
  bloom=scene.GetAllComponents<Bloom>().FirstOrDefault();
  sceneSuns=scene.GetAllComponents<DirectionalLight>().Select(s=>(s,s.Enabled)).ToArray();
  var source=sceneSuns.Select(s=>s.Light).FirstOrDefault();
  originalSky=source.IsValid() ? source.SkyColor : new Color(0.25f,0.32f,0.35f);
  originalSun=source.IsValid() ? source.LightColor : Color.White;
  originalSunRotation=source.IsValid() ? source.GameObject.WorldRotation : Rotation.From(55,30,0);
  if (tone.IsValid()) { originalMode=tone.Mode; originalExposure=tone.ExposureCompensation; originalToneEnabled=tone.Enabled; originalAutoExposure=tone.AutoExposureEnabled; }
  if (bloom.IsValid()) { originalBloom=bloom.Strength; originalBloomEnabled=bloom.Enabled; }
  // Own the light transform locally: network scene transforms cannot overwrite the preview.
  rig=new GameObject(true,"Tabletop lighting") { Flags=GameObjectFlags.NotNetworked };
  sun=rig.Components.Create<DirectionalLight>();
  sun.Shadows=true; sun.ShadowHardness=0; sun.ContactShadows=false;
  fill=rig.Components.Create<AmbientLight>();
  foreach(var item in sceneSuns) item.Light.Enabled=false;
  Apply();
 }
 public void Step(int delta) { Index=(Index+delta+Names.Length)%Names.Length; Apply(); }
 public void StepSunBrightness(int delta) { sunLevel=(sunLevel+delta+SunLevels.Length)%SunLevels.Length; Apply(); }
 public void StepSunDirection(int delta) { sunDirection=(sunDirection+delta+SunDirections.Length)%SunDirections.Length; Apply(); }
 public void Reset() { Index=0; sunLevel=3; sunDirection=0; Apply(); }
 public void Apply()
 {
  var tabletop=Index==0;
  if (tone.IsValid())
  {
   tone.Enabled=Index!=6;
   tone.AutoExposureEnabled=false;
   tone.Mode=tabletop || Index==4 ? Tonemapping.TonemappingMode.ACES : Index==5 ? Tonemapping.TonemappingMode.Linear : originalMode;
   tone.ExposureCompensation=tabletop ? 0.4f : originalExposure+(Index is 2 or 3 ? 0.7f : 0f);
  }
  if (sun.IsValid())
  {
   // Warm key and neutral fill keep the cardboard inks legible in shadow.
   sun.SkyColor=tabletop ? new Color(0.45f,0.46f,0.44f) : Index==3 ? new Color(0.5f,0.55f,0.58f) : originalSky;
   fill.Enabled=tabletop; fill.Color=new Color(0.35f,0.34f,0.31f);
   var colour=tabletop ? new Color(1.4f,1.32f,1.18f) : originalSun;
   var intensity=SunLevels[sunLevel]/100f;
   sun.LightColor=new Color(colour.r*intensity,colour.g*intensity,colour.b*intensity,1);
   sun.GameObject.WorldRotation=Rotation.FromYaw(SunDirections[sunDirection])*(tabletop ? Rotation.From(55,30,0) : originalSunRotation);
  }
  if (bloom.IsValid()) { bloom.Enabled=Index!=6 && !tabletop && originalBloomEnabled; bloom.Strength=originalBloom; }
 }
 public void Dispose()
 {
  if (rig.IsValid()) rig.Destroy();
  foreach(var item in sceneSuns) if (item.Light.IsValid()) item.Light.Enabled=item.Enabled;
  if (tone.IsValid()) { tone.Enabled=originalToneEnabled; tone.Mode=originalMode; tone.AutoExposureEnabled=originalAutoExposure; tone.ExposureCompensation=originalExposure; }
  if (bloom.IsValid()) { bloom.Enabled=originalBloomEnabled; bloom.Strength=originalBloom; }
 }
}
