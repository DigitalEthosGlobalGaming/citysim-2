using CitySim.Match;

namespace CitySim.Feel;

public enum FeedbackTier { Small, Medium, Large }
public sealed record FeedbackPreset( float Trauma, float Duration, string Sound );
public sealed record VisualCue( string Text, Vector3 Position, FeedbackTier Tier, bool Banner = false, string CardId = null, int Lot = -1, IReadOnlyList<BoardCell> Region = null, bool Confetti = false, Vector3? Partner = null, bool PlacementVp = false, bool PayoutCoin = false, bool PayoutReceipt = false );

/// <summary>Transient presentation only: no waits, time scale changes or rule mutations.</summary>
public sealed class Feedback : Component
{
 public static FeedbackPreset Preset(FeedbackTier tier) => tier switch { FeedbackTier.Small => new(0,0.18f,"sounds/feedback/tick.sound"), FeedbackTier.Medium => new(0.3f,0.35f,"sounds/feedback/place.sound"), _ => new(0.5f,0.6f,"sounds/feedback/chime.sound") };
 public bool ReduceShake { get; set; }
 public bool ReduceFlashing { get; set; }
 public event Action<VisualCue> Cue;
 public event Action TallySkipped;
 public event Action Cleared;
 public void ClearPresentation() { ResetPayout(); Cleared?.Invoke(); }
 CityGame game;
 MatchState match;
 readonly Queue<(PayoutEntry Entry, int Index)> tally = new();
 public int? PresentedCoins { get; private set; }
 readonly Queue<(float At,int Coins)> arrivals = new();
 readonly List<(GameObject Object,Vector3 Scale,float At)> payoutPops = new();
 float payoutAge;
 string payoutReceipt;
 float untilTally, tallyInterval, sincePlace = 100;
 int streak, lastCountdown = -1, lastPurchaseMarket = -1;
 MatchPhase? previousPhase;
 public void Bind(CityGame city) => game = city;
 public void Notice(string text) { Sfx.Play("sounds/feedback/deny.sound",game.Player.WorldPosition); Cue?.Invoke(new(text,default,FeedbackTier.Small,true)); }
 public void Attach(MatchState state)
 {
  if (match != null) {
   match.Events.PlacementResolved -= Placed; match.Events.PlacementRejected -= Denied;
   match.Events.ParkScored -= Park; match.Events.BuildingGrown -= Grown; match.Events.MatchScored -= Scored;
   match.Events.EventPlayed -= EventPlayed; match.Events.GoalsClaimed -= Goals; match.Events.HospitalEmergency -= Emergency;
   match.Events.PhaseChanged -= Phase; match.Events.PayoutResolved -= Payout;
   match.Events.BidChanged -= Bid; match.Events.PlayerReadyChanged -= Ready; match.Events.AuctionResolved -= Auction;
  }
  ClearPresentation(); match=state; lastCountdown=-1; lastPurchaseMarket=-1; previousPhase=null;
  if (match != null) {
   match.Events.PlacementResolved += Placed; match.Events.PlacementRejected += Denied;
   match.Events.ParkScored += Park; match.Events.BuildingGrown += Grown; match.Events.MatchScored += Scored;
   match.Events.EventPlayed += EventPlayed; match.Events.GoalsClaimed += Goals; match.Events.HospitalEmergency += Emergency;
   match.Events.PhaseChanged += Phase; match.Events.PayoutResolved += Payout;
   match.Events.BidChanged += Bid; match.Events.PlayerReadyChanged += Ready; match.Events.AuctionResolved += Auction;
  }
 }
 void Shake(float trauma) { if (!ReduceShake) game.Player.View.AddTrauma(trauma); }
 void Placed(PlacementEffects effects)
 {
  var cell=effects.Cell; if (cell.PlacedRound==0) return;
  streak=sincePlace<2f ? Math.Min(streak+1,6) : 0; sincePlace=0;
  var position=game.Grid.CellToWorld(cell.X,cell.Y);
  TabletopAudio.Place(1+streak*0.025f);
  if (effects.Park==null) Shake(Preset(FeedbackTier.Medium).Trauma);
  var vp=effects.Awards.Where(a=>a.Player==cell.Owner && !(effects.Park?.Awards.Contains(a) ?? false)).Sum(a=>a.Vp);
  var coins=effects.Awards.Where(a=>a.Player==cell.Owner).Sum(a=>a.Coins);
  if (vp != 0) Cue?.Invoke(new($"{vp:+#;-#} VP",position,FeedbackTier.Small,PlacementVp:true));
  if (coins != 0) Cue?.Invoke(new($"{coins:+#;-#} 🪙",position+Vector3.Up*15,FeedbackTier.Small));
  foreach(var award in effects.Awards.Where(a=>a.Player!=cell.Owner && a.Vp!=0).GroupBy(a=>a.Player))
  {
   var source=award.First().Source ?? cell;
   Cue?.Invoke(new($"{award.Sum(a=>a.Vp):+#;-#} VP",game.Grid.CellToWorld(source.X,source.Y),FeedbackTier.Small,PlacementVp:true));
  }
 }
 void Park(ParkScoreResult park)
 {
  var position=game.Grid.CellToWorld(park.Added.X,park.Added.Y);
  Shake(Math.Min(0.3f,0.12f+park.ScoringSize*0.015f)); Cue?.Invoke(new("",position,FeedbackTier.Medium,Region:park.Cells));
  var index=0; foreach (var award in park.Awards.GroupBy(a=>a.Player)) Cue?.Invoke(new($"{award.Sum(a=>a.Vp):+#;-#} VP",position+Vector3.Up*(index++*20),FeedbackTier.Small,PlacementVp:true));
 }
 void Grown(GrowthResult result) { Sfx.Play("sounds/feedback/chime.sound",game.Grid.CellToWorld(result.After.X,result.After.Y)); Cue?.Invoke(new("🌳 Forest grown",game.Grid.CellToWorld(result.After.X,result.After.Y)+Vector3.Up*30,FeedbackTier.Medium)); }
 void EventPlayed(CardEventResult result)
 {
  var position=result.Claim is { } claim ? game.Grid.CellToWorld(claim.X,claim.Y) : game.Grid.WorldPosition;
  Sfx.Play(Preset(FeedbackTier.Small).Sound,position); Cue?.Invoke(new(result.Claim != null ? "🚩 Reserved" : result.CardId == "renovation" ? "Renovated" : result.CardId == "rush-hour" ? "Rush Hour" : $"+{result.Coins} 🪙",position,FeedbackTier.Small));
 }
 void Emergency(HospitalEmergencyResult result)
 {
  if (result?.House==null) return;
  var target=result.Hospital ?? result.House; var position=game.Grid.CellToWorld(target.X,target.Y);
  Sfx.Play("sounds/feedback/chime.sound",position);
  Cue?.Invoke(new(result.Answered ? $"🚑 Hospital response · +{result.Award} VP" : "🚑 Emergency · no hospital in range",position,result.Answered ? FeedbackTier.Medium : FeedbackTier.Small));
 }
 void Scored(EndScore result) => ClearPresentation();
 public void EndRevealBeat(bool contract=false) => Sfx.Play(contract ? "sounds/feedback/chime.sound" : "sounds/feedback/tick.sound",game.Player.WorldPosition);
 public void CelebrateEnd(bool won)
 {
  Sfx.Play("sounds/feedback/chime.sound",game.Player.WorldPosition,won ? 1f : 0.85f);
  if (won) Cue?.Invoke(new("✦",default,FeedbackTier.Large,Confetti:true));
 }
 void Goals(GoalsResult result)
 {
  var names=string.Join(" · ",result.Claims.Select(c=>GoalLibrary.Get(c.GoalId).Name));
  var vp=result.Claims.SelectMany(c=>c.Awards).Where(a=>a.Player==CityGame.Me).Sum(a=>a.Vp);
  Shake(Preset(FeedbackTier.Large).Trauma); Sfx.Play(Preset(FeedbackTier.Large).Sound,game.Player.WorldPosition);
  Cue?.Invoke(new($"✓ {names}"+(vp>0 ? $" · +{vp} VP" : " claimed"),default,FeedbackTier.Large,true));
 }
 public void RevealContract() => TabletopAudio.Place();
 void Denied(PlacementResult result) { Sfx.Play("sounds/feedback/deny.sound",game.Player.WorldPosition); game.Player.Deny(); Shake(0.1f); Cue?.Invoke(new(result.Reason,game.Player.WorldPosition,FeedbackTier.Small,true)); }
 void Phase(PhaseChange phase)
 {
  if (phase.Phase is MatchPhase.RoundStart or MatchPhase.EndGame) return;
  if (phase.Phase == MatchPhase.Bid) { ResetPayout(); TallySkipped?.Invoke(); }
  lastCountdown=-1; if (previousPhase==phase.Phase) return; previousPhase=phase.Phase;
  var text=phase.Phase switch { MatchPhase.Bid => $"Round {phase.Round} · Market", MatchPhase.Build => "Build your city", MatchPhase.Payout => "Your city pays out", MatchPhase.EndGame => "Eight rounds complete", _ => phase.Phase.ToString() };
  Cue?.Invoke(new(text,default,FeedbackTier.Small,true)); if (phase.Phase == MatchPhase.Bid) TabletopAudio.Fan(); else TabletopAudio.Switch();
 }
 void Bid(Guid owner)
 {
  if (owner!=CityGame.Me || game.OwnAuction is not { } view || view.Resolving || view.Players.FirstOrDefault(p=>p.Player==owner)?.HasBid != true) return;
  if (lastPurchaseMarket == view.Id) return; lastPurchaseMarket = view.Id;
  TabletopAudio.Purchase();
 }
 void Ready(Guid owner) { if (owner==CityGame.Me) TabletopAudio.Switch(); }
 void Auction(AuctionResult result)
 {
  var own=result.Wins.FirstOrDefault(w=>w.Player==CityGame.Me);
  if (own!=null) { var card=Cards.CardLibrary.Get(own.CardId); Cue?.Invoke(new($"{card.Icon} {card.Name}",default,FeedbackTier.Medium,CardId:own.CardId,Lot:own.Lot)); TabletopAudio.Card(); }
  if (result.Accounts.FirstOrDefault(a=>a.Player==CityGame.Me)?.Refunded>0) TabletopAudio.Income(0.95f);
 }
 public void RevealCard(Cards.Rarity rarity) { if (rarity==Cards.Rarity.Legendary) { Shake(Preset(FeedbackTier.Large).Trauma); Sfx.Play(Preset(FeedbackTier.Large).Sound,game.Player.WorldPosition,0.85f); } else TabletopAudio.Card(); }
 void Payout(PayoutResult result)
 {
  ResetPayout();
  // Every player has base income. Only narrate the local player's entries on this client.
  var own=result.Entries.Where(e=>e.Player==CityGame.Me && e.Coins!=0).ToArray();
  if (game.LocalPlayer != null)
  {
   var total=own.Sum(e=>e.Coins);
   PresentedCoins=game.LocalPlayer.Coins-total;
   var groups=own.GroupBy(e=>(e.Reason,e.Source,e.Partner))
    .Select(g=>g.First() with { Coins=g.Sum(e=>e.Coins) }).ToList();
   // Guaranteed income is one beat, followed by the sources that earned the rest.
   groups=groups.OrderBy(e=>e.Reason=="Base income" ? 0 : e.Source==null ? 2 : 1).ToList();
   if (groups.Count>7)
   {
    var tail=groups.Skip(6).ToArray(); groups=groups.Take(6).ToList();
    groups.Add(new(CityGame.Me,tail.Sum(e=>e.Coins),0,$"{tail.Length} other income sources"));
   }
   var index=0; foreach (var entry in groups) tally.Enqueue((entry,index++));
   tallyInterval=(float)Math.Min(0.48,Math.Max(0.05,(match.Settings.PayoutSeconds-1.8)/Math.Max(1,tally.Count)));
   untilTally=0.35f;
   var breakdown=string.Join("\n",own.GroupBy(e=>e.Reason).Select(g=>$"{PayoutReason(g.Key)}\t{g.Sum(e=>e.Coins):+#;-#;0}"));
   payoutReceipt=$"Round {result.Round} · {total:+#;-#;0} coins\n{breakdown}";
  }
 }
 static string PayoutReason(string reason) => reason;
 void ResetPayout()
 {
  foreach (var pop in payoutPops) if (pop.Object.IsValid()) pop.Object.WorldScale=pop.Scale;
  payoutPops.Clear(); arrivals.Clear(); tally.Clear();
  PresentedCoins=null; payoutReceipt=null; payoutAge=0;
 }
 public void SkipTally() { var receipt=payoutReceipt; ResetPayout(); TallySkipped?.Invoke(); if (receipt!=null) Cue?.Invoke(new(receipt,default,FeedbackTier.Medium,true,PayoutReceipt:true)); }
 protected override void OnUpdate()
 {
  sincePlace+=Time.Delta;
  payoutAge+=Time.Delta;
  for (var i=payoutPops.Count-1;i>=0;i--)
  {
   var pop=payoutPops[i]; var t=Math.Clamp((payoutAge-pop.At)/0.4f,0,1);
   if (pop.Object.IsValid()) pop.Object.WorldScale=pop.Scale*(1+0.09f*MathF.Sin(t*MathF.PI)*(1-t));
   if (t>=1 || !pop.Object.IsValid()) payoutPops.RemoveAt(i);
  }
  while (arrivals.Count>0 && arrivals.Peek().At<=payoutAge)
  {
   var arrival=arrivals.Dequeue(); PresentedCoins+=arrival.Coins;
   TabletopAudio.Income(arrival.Coins>=0 ? 1.05f : 0.95f);
  }
  if (PresentedCoins.HasValue && tally.Count==0 && arrivals.Count==0)
  {
   PresentedCoins=null;
   if (payoutReceipt!=null) { Cue?.Invoke(new(payoutReceipt,default,FeedbackTier.Medium,true,PayoutReceipt:true)); payoutReceipt=null; Sfx.Play("sounds/feedback/chime.sound",game.Player.WorldPosition); }
  }
  if (PresentedCoins.HasValue && match?.Phase!=MatchPhase.Payout) ResetPayout();
  if (tally.Count>0) { untilTally-=Time.Delta; while (tally.Count>0 && untilTally<=0) {
   var (entry,index)=tally.Dequeue(); var position=entry.Source is { } cell ? game.Grid.CellToWorld(cell.X,cell.Y) : game.Grid.WorldPosition;
   Vector3? partner=null;
   var reason=entry.Reason;
   Cue?.Invoke(new($"{entry.Coins:+#;-#;0} 🪙 · {reason}",position,FeedbackTier.Small,Partner:partner,PayoutCoin:true));
   arrivals.Enqueue((payoutAge+0.85f,entry.Coins));
   if (entry.Source is { } source)
   {
    var building=game.Grid.Get(source.X,source.Y)?.Building;
    if (building.IsValid() && !payoutPops.Any(p=>p.Object==building)) payoutPops.Add((building,building.WorldScale,payoutAge));
   }
   untilTally+=tallyInterval;
  } }
  if (match==null || game.Screens.Top!=GameScreen.Match || match.Phase is not (MatchPhase.Bid or MatchPhase.Build)) return;
  var seconds=(int)Math.Ceiling(match.PhaseEndsAt-game.Clock); if (seconds is >0 and <=5 && seconds!=lastCountdown) { lastCountdown=seconds; Sfx.Play("sounds/feedback/tick.sound",game.Player.WorldPosition); }
 }
 protected override void OnDestroy() => Attach(null);
}
