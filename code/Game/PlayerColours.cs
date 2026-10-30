namespace CitySim;

/// <summary>Printed player inks in match seat order, shared by board and standings.</summary>
public static class PlayerColours
{
 static readonly Color[] Palette = {
  new(0.906f,0.533f,0.459f), new(0.490f,0.729f,0.792f),
  new(0.549f,0.741f,0.502f), new(0.835f,0.678f,0.365f),
  new(0.729f,0.608f,0.800f), new(0.847f,0.561f,0.702f),
  new(0.831f,0.553f,0.302f), new(0.420f,0.620f,0.620f)
 };
 public static int Seat(CitySim.Match.MatchState match,Guid owner)
 {
  if (match == null || owner == Guid.Empty) return -1;
  var index=0; foreach(var player in match.Players.Keys) { if (player==owner) return index; index++; }
  return -1;
 }
 public static Color For(CitySim.Match.MatchState match,Guid owner)
 {
  var seat=Seat(match,owner);
  return seat<0 ? Color.White : Palette[seat % Palette.Length];
 }
}
