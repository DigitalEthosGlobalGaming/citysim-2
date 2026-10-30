using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CitySim.Match;

/// <summary>Pure replacement validation, atomicity, scoring and persistence.</summary>
public static class RenovationChecks
{
 public static IReadOnlyList<string> Run()
 {
  var passed=new List<string>();
  void Check(bool condition,string label) { if (!condition) throw new InvalidOperationException(label); passed.Add("PASS "+label); }
  string Json<T>(T value) => JsonSerializer.Serialize(value);
  var a=new Guid("aaaaaaaa-0000-0000-0000-000000000001"); var b=new Guid("bbbbbbbb-0000-0000-0000-000000000002");
  var s=new MatchState(9,9,44,new MatchSettings {EnableContracts=false,DefaultStreets=0,DefaultCommons=0,RareBuildCost=0,LegendaryBuildCost=0});
  s.AddPlayer(a,"A"); s.AddPlayer(b,"B"); for(var x=0;x<9;x++) s.SeedStreet(x,4); s.SetPhase(MatchPhase.Build,1,40);
  void Place(Guid owner,string id,int x,int y) { if (!s.GiveCard(owner,id) || !s.TryPlace(owner,id,x,y).Success) throw new InvalidOperationException("Bad fixture: "+id); }
  Place(a,"bank",2,3); Place(b,"house",5,3); Place(a,"town-hall",3,3);
  s.GiveCard(a,"renovation"); s.GiveCard(a,"monument"); s.GiveCard(a,"bank"); s.GiveCard(a,"town-hall"); s.GiveCard(a,"street");
  var before=Json(s.Capture()); var handChanges=0; var placements=0; var replacements=0;
  s.Events.HandChanged+=_=>handChanges++; s.Events.CardPlaced+=_=>placements++; s.Events.BuildingReplaced+=_=>replacements++;
  Check(s.RenovationError(a,0,1,5,3)!=null && s.RenovationError(a,0,1,2,4)!=null && s.RenovationError(a,0,1,99,2)!=null,"Renovation rejects rival, road and out-of-bounds targets");
  Check(s.RenovationError(a,0,0,2,3)!=null && s.RenovationError(a,0,2,2,3)!=null && s.RenovationError(a,0,4,2,3)!=null,"Renovation rejects the same slot, same building type and a road replacement");
  Check(s.RenovationError(a,0,3,2,3)!=null && s.RenovationError(a,1,0,2,3)!=null && s.RenovationError(a,99,1,2,3)!=null,"Renovation rejects ownership-limit violations and stale event slots");
  Check(!s.TryRenovate(a,0,3,2,3).Success && Json(s.Capture())==before && handChanges==0 && placements==0 && replacements==0,"invalid replacement leaves cards, board and events unchanged");
  var preview=s.PreviewRenovation(a,0,1,2,3);
  Check(preview.HasValue && Json(s.Capture())==before && handChanges==0 && placements==0 && replacements==0,"Renovation preview leaves checkpoint and events unchanged");
  var old=s.Get(2,3); var oldVp=s.Players[a].Vp; var oldVersion=s.Players[a].HandVersion; var oldCount=s.BuildingsPlacedThisRound(a);
  Check(Payout.Calculate(s).Entries.Any(e=>e.Player==a && e.Reason=="Bank"),"fixture starts with the bank payout effect");
  var result=s.TryRenovate(a,0,1,2,3);
  Check(result.Success && result.Cell.CardId=="monument" && result.Cell.Order>old.Order && result.Cell.Age==0 && result.Cell.PlacedRound==1 && s.Get(2,3)==result.Cell,"Renovation commits a fresh replacement on the same tile");
  Check(!s.Players[a].Hand.Contains("renovation") && !s.Players[a].Hand.Contains("monument") && s.Players[a].HandVersion==oldVersion+1 && handChanges==1 && replacements==1 && placements==1,"Renovation consumes exactly two cards and emits one replacement and placement");
  Check(s.Players[a].Vp==oldVp && s.BuildingsPlacedThisRound(a)==oldCount+1 && !Payout.Calculate(s).Entries.Any(e=>e.Player==a && e.Reason=="Bank"),"Renovation preserves earned VP, records new construction and removes the old effect");
  var restored=MatchState.Restore(JsonSerializer.Deserialize<MatchCheckpoint>(Json(s.Capture())));
  Check(restored.Get(2,3)==result.Cell && restored.BuildingsPlacedThisRound(a)==s.BuildingsPlacedThisRound(a),"Renovation replacement and building history survive a checkpoint");
  Check(!s.TryRenovate(a,0,1,2,3).Success,"stale event slot cannot trigger another replacement");
  var t=new MatchState(9,9,55,new MatchSettings {EnableContracts=false}); t.AddPlayer(a,"A"); t.SeedStreet(4,4); t.SetPhase(MatchPhase.Build,1,40);
  t.GiveCard(a,"bank"); t.TryPlace(a,"bank",0,0); t.GiveCard(a,"renovation"); t.GiveCard(a,"house");
  Check(t.RenovationError(a,0,1,0,0)!=null,"replacement requiring a road is rejected without a neighbouring road");
  t.Discard(a,1); t.Discard(a,0);
  t.GiveCard(a,"contractor"); t.TryPlace(a,"contractor",4,3); t.GiveCard(a,"renovation"); t.GiveCard(a,"house");
  var prior=t.Players[a].Coins;
  Check(t.TryRenovate(a,0,1,4,3).Success && t.Players[a].Coins==prior,"removed Contractor cannot trigger on its own replacement");
  t.GiveCard(a,"park"); t.TryPlace(a,"park",1,0); t.GiveCard(a,"renovation"); t.GiveCard(a,"park");
  var vp=t.Players[a].Vp; var parkPreview=t.PreviewRenovation(a,0,1,0,0);
  Check(parkPreview==0 && t.TryRenovate(a,0,1,0,0).Success && t.Players[a].Vp-vp==parkPreview,"park replacement from size one to two applies its zero-point preview");
  var net=new MatchState(9,9,66,new MatchSettings {EnableContracts=false,EnableAuctions=false,StubBidSeconds=0,DefaultStreets=0,DefaultCommons=0});
  net.AddPlayer(a,"A"); net.AddPlayer(b,"B"); for(var x=0;x<9;x++) net.SeedStreet(x,4);
  var rounds=new RoundMachine(net); rounds.Start(0); rounds.Tick(0);
  foreach(var p in new[]{(a,2),(b,5)}) { net.GiveCard(p.Item1,"bank"); if (!net.TryPlace(p.Item1,"bank",p.Item2,3).Success) throw new InvalidOperationException("Invalid protocol fixture."); net.GiveCard(p.Item1,"renovation"); net.GiveCard(p.Item1,"shop"); }
  var id=Guid.NewGuid(); var authority=new MatchAuthority(id,net,rounds); var replica=new MatchReplica();
  var first=MatchProjection.For(net,rounds,b,id,1,0); if (!replica.Apply(b,first)) throw new InvalidOperationException("Initial replica rejected.");
  var original=net.Get(2,3); BoardReplacement change=null; var replicaPlaced=0;
  replica.State.Events.BuildingReplaced+=value=>change=value; replica.State.Events.CardPlaced+=_=>replicaPlaced++;
  var cmd=new MatchCommand(id,1,1,MatchPhase.Build,MatchCommandKind.Renovate,Slot:0,HandVersion:net.Players[a].HandVersion,CardId:"renovation",X:2,Y:3,ReplacementSlot:1);
  Check(!authority.Execute(b,cmd with {HandVersion=net.Players[b].HandVersion},0).Accepted && net.Get(2,3)==original,"authenticated rival cannot renovate another player's building");
  Check(rounds.Ready(b,0) && !authority.Execute(b,cmd with {Sequence=2,HandVersion=net.Players[b].HandVersion,X=5},0).Accepted,"a ready player cannot renovate their own building");
  Check(!authority.Execute(a,cmd with {HandVersion=cmd.HandVersion+1},0).Accepted && net.Get(2,3)==original,"stale hand version is rejected before renovation");
  var valid=cmd with {Sequence=2}; var accepted=authority.Execute(a,valid,0);
  Check(accepted.Accepted && !authority.Execute(a,valid,0).Accepted && net.Get(2,3).CardId=="shop" && net.Players[a].Hand.Count==0,"valid renovation is accepted once and replay cannot consume or replace again");
  Check(replica.Apply(b,MatchProjection.For(net,rounds,b,id,2,0)) && change?.Before==original && change.After==net.Get(2,3) && replica.State.Get(2,3)==change.After && replicaPlaced==1 && replica.State.Players[a].Hand.Count==0,"replica emits old-to-new replacement and one placement without exposing rival hand");
  bool RejectsSettings(MatchSettings settings) { try { _=new MatchState(5,5,settings:settings); return false; } catch (ArgumentException) { return true; } }
  Check(RejectsSettings(new MatchSettings { RareBuildCost=-1 }) && RejectsSettings(new MatchSettings { LegendaryBuildCost=-1 }),"negative build costs are rejected");
  var paid=new MatchState(5,5,77,new MatchSettings { StartingCoins=4, EnableContracts=false, RareBuildCost=2, LegendaryBuildCost=4 });
  paid.AddPlayer(a,"A"); paid.SeedStreet(2,2); paid.SetPhase(MatchPhase.Build,1,40);
  paid.GiveCard(a,"monument"); var initial=Json(paid.Capture());
  Check(!paid.TryPlace(a,"monument",2,2).Success && Json(paid.Capture())==initial,"failed placement does not charge or mutate state");
  Check(paid.TryPlace(a,"monument",1,1).Success && paid.Players[a].Coins==0,"successful legendary placement charges exactly once");
  paid.GiveCard(a,"renovation"); paid.GiveCard(a,"monument"); paid.GiveCard(a,"bank");
  var poor=Json(paid.Capture());
  Check(!paid.TryPlace(a,"bank",1,2).Success && Json(paid.Capture())==poor,"insufficient funds leave placement and hand unchanged");
  Check(!paid.TryRenovate(a,0,2,1,1).Success && Json(paid.Capture())==poor,"insufficient funds leave renovation unchanged");
  paid.ChangeCoins(a,2); var paidBefore=paid.Players[a].Coins;
  Check(paid.TryRenovate(a,0,2,1,1).Success && paid.Players[a].Coins==paidBefore-2,"renovation charges replacement build cost once");
  var afterPaid=Json(paid.Capture());
  Check(!paid.TryRenovate(a,0,1,1,1).Success && Json(paid.Capture())==afterPaid,"renovation replay cannot charge twice");
  var restoredPaid=MatchState.Restore(JsonSerializer.Deserialize<MatchCheckpoint>(afterPaid));
  Check(restoredPaid.Players[a].Coins==paid.Players[a].Coins,"checkpoint preserves charged balance");
  return passed.AsReadOnly();
 }
}


