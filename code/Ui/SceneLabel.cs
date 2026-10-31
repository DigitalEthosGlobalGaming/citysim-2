using CitySim.Feel;
using CitySim.Match;

namespace CitySim;

/// <summary>Non-interactive text rendered in the scene, below screen-space HUD panels.</summary>
public sealed class SceneLabel
{
 readonly GameObject obj;
 readonly TextRenderer text;
 public SceneLabel(CityGame city, string name, float scale)
 {
  obj = new GameObject(true, name) { Parent = city.GameObject, NetworkMode = NetworkMode.Never };
  text = obj.Components.Create<TextRenderer>();
  text.RenderOptions.Overlay = false;
  text.RenderOptions.AfterUI = false;
  text.HorizontalAlignment = TextRenderer.HAlignment.Center;
  text.Billboard = TextRenderer.BillboardMode.Always;
  // Measured glyphs are scaled in world units. Keep labels around 14–20 units
  // high so they remain readable from the board's default overview camera.
  text.Scale = scale * 3f;
  text.BlendMode = BlendMode.Normal;
  text.FogStrength = 0;
  text.FontSize = 48;
  text.FontWeight = 700;
  var scope = text.TextScope;
  scope.Outline = new TextRendering.Outline { Enabled = true, Size = 3, Color = new Color(0.12f,0.09f,0.06f) };
  text.TextScope = scope;
  text.Color = new Color(1f, 0.98f, 0.92f);
  text.Enabled = false;
 }
 public void Set(Vector3? position, string value)
 {
  if (!obj.IsValid()) return;
  text.Enabled = position.HasValue && !string.IsNullOrEmpty(value);
  if (!position.HasValue) return;
  obj.WorldPosition = position.Value;
  if (text.Text != value) text.Text = value;
 }
 public void Destroy() { if (obj.IsValid()) obj.Destroy(); }
 public static void Spawn(CityGame city, VisualCue cue)
 {
  if (city == null || city.State != GameState.Playing || city.Screens.Top != GameScreen.Match) return;
  var existing = city.Scene.GetAllComponents<SceneLabelLifetime>().Where(e => e.Owner == city).ToArray();
  if (existing.Length >= 30) existing[0].GameObject.Destroy();
  var label = new SceneLabel(city, "Board feedback", 0.12f);
  var tile = city.Grid.WorldToTile(cue.Position);
  var anchor = AboveTile(tile, cue.Position);
  // Cues on the same cell retain the source vertical stagger supplied by Feedback.
  var sourceOffset = tile == null ? 0 : Math.Max(0, cue.Position.z - tile.Center.z);
  label.Set(anchor + Vector3.Up * (18 + sourceOffset), cue.Text);
  var lifetime = label.obj.Components.Create<SceneLabelLifetime>();
  lifetime.Owner = city;
  lifetime.StartPosition = label.obj.WorldPosition;
  lifetime.Duration = cue.PayoutCoin ? 0.85f : 1.4f;
  lifetime.PlacementVp = cue.PlacementVp;
  if (cue.PlacementVp) label.text.Scale = 0.12f * 3f * 0.65f;
 }
 public static Vector3 AboveTile(Tile tile, Vector3 fallback)
 {
  if (tile == null) return fallback;
  return AboveVisual(tile.Building, tile.Center + Vector3.Up * CityGrid.SurfaceHeight);
 }
 public static Vector3 AboveVisual(GameObject visual, Vector3 fallback)
 {
  var top = fallback.z;
  void Visit(GameObject part)
  {
   if (!part.IsValid()) return;
   var renderer = part.Components.Get<ModelRenderer>();
   if (renderer.IsValid() && renderer.Enabled && renderer.Model != null)
    top = Math.Max(top, part.WorldPosition.z + renderer.Model.Bounds.Maxs.z * part.WorldScale.z);
   foreach (var child in part.Children) Visit(child);
  }
  Visit(visual);
  return new Vector3(fallback.x, fallback.y, top);
 }
 public static void ClearTransient(CityGame city)
 {
  if (city == null || !city.IsValid()) return;
  foreach (var effect in city.Scene.GetAllComponents<SceneLabelLifetime>().ToArray())
   if (effect.Owner == city && effect.GameObject.IsValid()) effect.GameObject.Destroy();
 }
}

public sealed class SceneLabelLifetime : Component
{
 public CityGame Owner { get; set; }
 public Vector3 StartPosition { get; set; }
 public float Duration { get; set; } = 1.4f;
 public bool PlacementVp { get; set; }
 float age;
 protected override void OnUpdate()
 {
  if (!Owner.IsValid() || Owner.State != GameState.Playing || Owner.Screens.Top != GameScreen.Match) { GameObject.Destroy(); return; }
  age += Time.Delta;
  if (age >= Duration) { GameObject.Destroy(); return; }
  var t = Math.Clamp(age / Duration, 0, 1);
  WorldPosition = StartPosition + Vector3.Up * (12 * (1 - MathF.Pow(1-t, 3)));
  var renderer = Components.Get<TextRenderer>();
  if (renderer.IsValid())
  {
   if (PlacementVp)
   {
    var pop = Math.Clamp(age / 0.24f, 0, 1) - 1;
    var ease = 1 + 2.70158f * pop * pop * pop + 1.70158f * pop * pop;
    var exit = Math.Clamp((Duration - age) / 0.22f, 0, 1);
    renderer.Scale = 0.12f * 3f * (0.65f + 0.35f * ease) * (exit * exit * (3 - 2 * exit));
   }
   else renderer.Color = new Color(1f, 0.98f, 0.92f, Math.Clamp((Duration-age)*3,0,1));
  }
 }
}


