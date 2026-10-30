using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Ai;

namespace CitySim.Match;

public static class EventChecks
{
 static readonly Guid A=new("aaaaaaaa-0000-0000-0000-000000000001"), B=new("bbbbbbbb-0000-0000-0000-000000000002");
 public static IReadOnlyList<string> Run()
 {
  var passed=new List<string>();
  void Check(bool value,string label) { if (!value) throw new InvalidOperationException(label); passed.Add("PASS "+label); }
  T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
  var s=new MatchState(18,9,17,new MatchSettings {EnableContracts=false,EnableAuctions=false,DefaultStreets=0,DefaultCommons=0,RareBuildCost=0,LegendaryBuildCost=0});
  s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); for (int x=0;x<18;x++) s.SeedStreet(x,4);
  s.SetPhase(MatchPhase.Build,1,40);
  void Place(Guid owner,string card,int x,int y)
  {
   if (!s.GiveCard(owner,card) || !s.TryPlace(owner,card,x,y).Success) throw new InvalidOperationException($"Invalid event fixture placement: {card} {x},{y}");
  }
  for (int x=1;x<=6;x++) { Place(A,"house",x,3); Place(A,"shop",x,5); }
  Place(B,"house",10,3); Place(A,"street",12,3);
  Check(s.BuildingsPlacedThisRound(A)==12 && s.BuildingsPlacedThisRound(B)==1,"round activity counts committed buildings per owner and excludes roads");
  s.GiveCard(A,"ribbon-cutting"); var before=s.Players[A].Coins; var ribbon=s.Players[A].Hand.ToList().IndexOf("ribbon-cutting");
  Check(s.PlayEvent(A,ribbon) && s.Players[A].Coins-before==4 && !s.Players[A].Hand.Contains("ribbon-cutting"),"Ribbon Cutting pays one per own building capped at four and is consumed");
  s.GiveCard(A,"rush-hour"); s.GiveCard(A,"rush-hour"); var rush=s.Players[A].Hand.ToList().IndexOf("rush-hour");
  Check(s.PlayEvent(A,rush) && s.RushHourActive(A) && !s.RushHourActive(B),"Rush Hour activates only its owner");
  var retained=s.Players[A].Hand.Count; rush=s.Players[A].Hand.ToList().IndexOf("rush-hour");
  Check(!s.PlayEvent(A,rush) && s.Players[A].Hand.Count==retained,"a redundant Rush Hour cannot stack or consume the card");
  var payout=Payout.Calculate(s);
  var bonus=payout.Entries.Where(e=>e.Player==A && e.Reason=="Rush Hour").Sum(e=>e.Coins);
  Check(bonus==4,"Rush Hour pays per owned Shop capped at four");
  var matchId=Guid.NewGuid(); var rounds=new RoundMachine(s); var projection=MatchProjection.For(s,rounds,B,matchId,1,0); var replica=new MatchReplica();
  Check(replica.Apply(B,Copy(projection)) && replica.State.BuildingsPlacedThisRound(A)==12 && replica.State.RushHourActive(A) && replica.State.Players[A].Hand.Count==0,"recipient projection restores public activity without exposing rival hand");
  var checkpoint=Copy(s.Capture()); var restored=MatchState.Restore(checkpoint);
  Check(restored.BuildingsPlacedThisRound(A)==12 && restored.RushHourActive(A) && Payout.Calculate(restored).CoinsFor(A)==payout.CoinsFor(A),"checkpoint preserves round activity and payout modifiers");
  var removed=checkpoint with {Cells=checkpoint.Cells.Where(c=>!(c.Owner==A && c.CardId=="house")).ToArray()};
  Check(MatchState.Restore(removed).BuildingsPlacedThisRound(A)==12,"building history survives later board removal");
  var authority=new MatchAuthority(matchId,s,rounds); var slot=s.Players[A].Hand.ToList().IndexOf("rush-hour");
  var cmd=new MatchCommand(matchId,1,1,MatchPhase.Build,MatchCommandKind.PlayEvent,Slot:slot,HandVersion:s.Players[A].HandVersion,CardId:"rush-hour");
  Check(!authority.Execute(A,cmd with {HandVersion=cmd.HandVersion+1},0).Accepted && !authority.Execute(B,cmd with {Sequence=2},0).Accepted && !authority.Execute(A,cmd with {Sequence=2,CardId="grant"},0).Accepted,"event command rejects stale hand, wrong owner and wrong card");
  Check(!authority.Execute(A,cmd with {Sequence=3},0).Accepted && !authority.Execute(A,cmd with {Sequence=3},0).Accepted && s.Players[A].Hand.Count==retained,"redundant event and command replay do not consume the card");
  s.GiveCard(B,"ribbon-cutting"); var bslot=s.Players[B].Hand.ToList().IndexOf("ribbon-cutting"); var bcoins=s.Players[B].Coins;
  var valid=cmd with {Sequence=3,Slot=bslot,HandVersion=s.Players[B].HandVersion,CardId="ribbon-cutting"};
  Check(authority.Execute(B,valid,0).Accepted && !authority.Execute(B,valid,0).Accepted && s.Players[B].Coins==bcoins+1,"valid owner event pays the uncapped building count exactly once despite replay");
  Place(B,"shop",10,5); Place(B,"bank",11,5); s.GiveCard(B,"rush-hour"); s.PlayEvent(B,s.Players[B].Hand.ToList().IndexOf("rush-hour"));
  var bpay=Payout.Calculate(s);
  Check(bpay.Entries.Where(e=>e.Player==B && e.Reason=="Rush Hour").Sum(e=>e.Coins)==1 && bpay.Entries.Any(e=>e.Player==B && e.Reason=="Bank"),"Rush Hour counts only owned Shops, not Bank income");
  s.SetPhase(MatchPhase.RoundStart,2,50);
  Check(s.BuildingsPlacedThisRound(A)==0 && !s.RushHourActive(A) && s.Players[A].Hand.Contains("rush-hour"),"new round expires activity and retained Rush Hour can be used again");
  s.SetPhase(MatchPhase.Build,2,80); slot=s.Players[A].Hand.ToList().IndexOf("rush-hour");
  Check(s.PlayEvent(A,slot),"retained Rush Hour becomes playable in the next round");
  var botState=new MatchState(9,9,88,new MatchSettings {EnableContracts=false,EnableAuctions=false,StubBidSeconds=0,DefaultStreets=0,DefaultCommons=0});
  botState.AddPlayer(A,"Bot"); botState.SeedStreet(4,4); botState.GiveCard(A,"house"); botState.GiveCard(A,"ribbon-cutting"); botState.GiveCard(A,"rush-hour");
  var botRounds=new RoundMachine(botState); var bot=new Bot(A,botState,botRounds,BotDifficulty.Hard,5); var actions=new List<string>();
  botState.Events.CardPlaced+=cell=>{ if (cell.Owner==A) actions.Add(cell.CardId); };
  botState.Events.EventPlayed+=ev=>actions.Add(ev.CardId); botRounds.Start(0); botRounds.Tick(0);
  for (int n=0;n<100 && botState.Phase==MatchPhase.Build;n++) bot.Update(n*0.25);
  Check(actions.SequenceEqual(new[]{"house","ribbon-cutting","rush-hour"}) && botState.Phase==MatchPhase.Payout,"bot places buildings before Ribbon Cutting, plays Rush Hour, then readies");
  return passed.AsReadOnly();
 }
}
