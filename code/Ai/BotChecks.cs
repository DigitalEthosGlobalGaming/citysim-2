using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using CitySim.Match;

namespace CitySim.Ai;

/// <summary>Bounded, transport-independent M6 behavior checks. Full simulation sweeps belong to M8.</summary>
public static class BotChecks
{
 static readonly Guid[] Ids = Enumerable.Range(1,4).Select(i => new Guid($"00000000-0000-0000-0000-{i:000000000000}")).ToArray();
 public static IReadOnlyList<string> Run()
 {
  var passed=new List<string>();
  void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
  string Json<T>(T x) => JsonSerializer.Serialize(x);
  (MatchState State,RoundMachine Rounds,Bot[] Bots) Setup(int players,int side=9, int seed=144, BotDifficulty difficulty=BotDifficulty.Normal)
  {
   var state=new MatchState(side,side,seed,new MatchSettings());
   for (var i=0;i<players;i++) state.AddPlayer(Ids[i],$"Player {i+1}");
   for (var i=0;i<side;i++) { state.SeedStreet(i,side/2); if (i!=side/2) state.SeedStreet(side/2,i); }
   var rounds=new RoundMachine(state);
   return (state,rounds,Enumerable.Range(0,players).Select(i=>new Bot(Ids[i],state,rounds,difficulty,seed+i)).ToArray());
  }
  string Play(int players,int side, int seed,out double seconds)
  {
   var (state,rounds,bots)=Setup(players,side,seed); var watch=Stopwatch.StartNew();
   var offered=Ids.Take(players).ToDictionary(id=>id,id=>rounds.GetContractView(id).Choices.ToArray());
   foreach (var bot in bots) bot.ChooseContract();
   Check(rounds.ContractsChosen && Ids.Take(players).All(id=>offered[id].Contains(rounds.GetContractView(id)?.Selected)),$"{players} bots choose their offered contracts");
   rounds.Start(0); var denied=0; state.Events.PlacementRejected+=_=>denied++;
   for (var step=0;step<2500 && state.Phase!=MatchPhase.EndGame;step++)
   {
    var now=step*0.25;
    foreach (var bot in bots) bot.Update(now);
    foreach (var id in Ids.Take(players))
    {
     var auction=rounds.GetAuctionView(id);
     if (auction!=null && !auction.Resolving && (auction.ReservedCoins<0 || auction.AvailableCoins<0 || auction.ReservedCoins+auction.AvailableCoins!=state.Players[id].Coins))
      throw new InvalidOperationException("Bot exceeded its bid budget.");
    }
    if (state.Players.Values.Any(p=>p.Coins<0)) throw new InvalidOperationException("Bot spent more coins than it owned.");
    rounds.Tick(now);
   }
   watch.Stop(); seconds=watch.Elapsed.TotalSeconds;
   Check(denied==0 && state.Phase==MatchPhase.EndGame && state.Round==state.Settings.Rounds && state.FinalScore?.Players.Count==players && state.ContractReveals.Count==players,$"{players} bots finish an eight-round match with legal actions and final scores");
   return Json(new { Match=state.Capture(), Rounds=rounds.Capture() });
  }
  var (sample,round,bots)=Setup(2); foreach(var bot in bots) bot.ChooseContract(); round.Start(0);
  var initial=round.GetAuctionView(Ids[0]); bots[0].Update(0);
  var immediate=round.GetAuctionView(Ids[0]);
  Check(initial!=null && !immediate.Ready && immediate.Lots.All(l=>l.Amount==0),"bot does not bid or ready on the initial scheduling tick");
  var deadlineState=Json(sample.Capture()); var deadlineBid=Json(round.GetAuctionView(Ids[0]));
  bots[0].Update(sample.PhaseEndsAt);
  Check(deadlineState==Json(sample.Capture()) && deadlineBid==Json(round.GetAuctionView(Ids[0])),"bot submits no action at the auction deadline");
  for (var rivals=1;rivals<=3;rivals++)
  {
   var (s,r,agents)=Setup(rivals+1);
   foreach (var id in Ids.Take(rivals+1)) { var offer=r.GetContractView(id); r.ChooseContract(id,offer.DraftId,offer.Choices[0]); }
   r.Start(0);
   for (var n=0;n<32;n++) foreach (var bot in agents.Skip(1)) bot.Update(n*0.25);
   Check(Ids.Skip(1).Take(rivals).All(id=>r.GetAuctionView(id).Ready) && !r.GetAuctionView(Ids[0]).Ready && s.Phase==MatchPhase.Bid,$"{rivals} rivals independently choose or pass while the market is untimed while the human remains unready");
  }
  (MatchState,RoundMachine,Bot) Placement()
  {
   var settings=new MatchSettings {EnableContracts=false,EnableAuctions=false,DefaultStreets=0,DefaultCommons=0,StubBidSeconds=0};
   var s=new MatchState(9,9,900,settings); s.AddPlayer(Ids[0],"Planner");
   for (var i=0;i<9;i++) { s.SeedStreet(i,4); if (i!=4) s.SeedStreet(4,i); }
   s.SetPhase(MatchPhase.Build,1,40); s.GiveCard(Ids[0],"house"); s.TryPlace(Ids[0],"house",5,3);
   s.SetPhase(MatchPhase.Lobby,0,0); s.GiveCard(Ids[0],"house");
   var r=new RoundMachine(s); var bot=new Bot(Ids[0],s,r,BotDifficulty.Hard,12); r.Start(0); r.Tick(0);
   return (s,r,bot);
  }
  var (planned,plannedRounds,planner)=Placement();
  var (expired,_,lateBot)=Placement(); lateBot.Update(0); var beforeDeadline=Json(expired.Capture()); lateBot.Update(expired.PhaseEndsAt);
  Check(beforeDeadline==Json(expired.Capture()),"bot performs no build action at the deadline");
  var first=Enumerable.Range(0,81).Select(i=>(X:i%9,Y:i/9)).First(p=>planned.PlacementError(Ids[0],"house",p.X,p.Y)==null);
  var firstVp=PlacementScoring.Preview(planned,"house",first.X,first.Y,Ids[0]);
  for (var n=0;n<20 && planned.Players[Ids[0]].Hand.Count>0;n++) planner.Update(n*0.25);
  var chosen=planned.Cells().Single(c=>c.Owner==Ids[0] && c.PlacedRound==1 && (c.X!=5 || c.Y!=3));
  Check(planned.Players[Ids[0]].Vp>firstVp && chosen!=null,"hard bot chooses a VP-producing house placement over the first legal cell");
  for (var n=20;n<40 && !plannedRounds.IsBuildReady(Ids[0]);n++) planner.Update(n*0.25);
  Check(planned.Phase==MatchPhase.Payout || plannedRounds.IsBuildReady(Ids[0]),"bot readies after completing its build actions");
  var endSettings=new MatchSettings {EnableContracts=false,EnableAuctions=false,DefaultStreets=0,DefaultCommons=0};
  var late=new MatchState(9,9,72,endSettings); late.AddPlayer(Ids[0],"Planner");
  late.SeedStreet(4,4); late.SetPhase(MatchPhase.Build,8,40); late.GiveCard(Ids[0],"bank"); late.GiveCard(Ids[0],"monument");
  var lateRounds=new RoundMachine(late); var monumentBot=new Bot(Ids[0],late,lateRounds,BotDifficulty.Hard,12);
  for (int n=0;n<12 && !late.Cells().Any(c=>c.Owner==Ids[0]);n++) monumentBot.Update(n*0.25);
  Check(late.Cells().Any(c=>c.Owner==Ids[0] && c.CardId=="monument") && late.Players[Ids[0]].Hand.Contains("bank"),"late-round bot chooses six-VP Monument over a low-yield bank");
  var scoring=new MatchState(9,9,101,endSettings); scoring.AddPlayer(Ids[0],"A"); scoring.AddPlayer(Ids[1],"B");
  for (var i=0;i<9;i++) { scoring.SeedStreet(i,4); if (i!=4) scoring.SeedStreet(4,i); }
  scoring.SetPhase(MatchPhase.Build,8,40);
  void Place(Guid owner,string card,int x,int y)
  {
   if (!scoring.GiveCard(owner,card) || !scoring.TryPlace(owner,card,x,y).Success) throw new InvalidOperationException("End-score fixture placement failed: "+card);
  }
  Place(Ids[0],"house",2,3); Place(Ids[1],"house",3,2); Place(Ids[0],"factory",3,3);
  Place(Ids[0],"park",6,3); Place(Ids[0],"cafe",5,3); Place(Ids[0],"museum",6,5); Place(Ids[0],"monument",0,0);
  var effects=FinalScoring.CardEffects(scoring);
  Check(effects.Where(l=>l.Player==Ids[0] && l.Reason=="Factory pollution").Sum(l=>l.Vp)==-1 && effects.Where(l=>l.Player==Ids[1] && l.Reason=="Factory pollution").Sum(l=>l.Vp)==-1 &&
   effects.Single(l=>l.Reason=="Monument").Vp==6 && effects.Single(l=>l.Reason=="Cafe").Vp==2 && effects.Single(l=>l.Reason=="Museum").Vp==2,
   "end-card scoring accounts for Monument, park-adjacent Cafe, Culture Museum and pollution on both owners");
  var scored=FinalScoring.Calculate(scoring);
  Check(Ids.Take(2).All(id=>effects.Where(l=>l.Player==id).SequenceEqual(scored.Players.Single(p=>p.Player==id).Lines.Where(l=>l.Source!=null))),"end-card helper agrees with the final score breakdown");
  var replacementState=new MatchState(3,3,778,new MatchSettings {EnableGoals=false,EnableContracts=false,DefaultStreets=0,DefaultCommons=0});
  replacementState.AddPlayer(Ids[0],"Renovator"); replacementState.SeedStreet(1,1); replacementState.SetPhase(MatchPhase.Build,1,40);
  for(var y=0;y<3;y++) for(var x=0;x<3;x++) if(replacementState.Get(x,y)==null) { replacementState.GiveCard(Ids[0],"park"); replacementState.TryPlace(Ids[0],"park",x,y); }
  replacementState.SetPhase(MatchPhase.Build,8,40); replacementState.GiveCard(Ids[0],"renovation"); replacementState.GiveCard(Ids[0],"monument");
  var replacementBot=new Bot(Ids[0],replacementState,new RoundMachine(replacementState),BotDifficulty.Hard,12);
  for(var n=0;n<8 && replacementState.Players[Ids[0]].Hand.Count>0;n++) replacementBot.Update(n*0.25);
  Check(replacementState.Cells().Count()==9 && replacementState.Cells().Count(c=>c.CardId=="monument")==1 && replacementState.Players[Ids[0]].Hand.Count==0,"bot renovates a full board into a useful end-game building through shared rules");
  Check(FinalScoring.CardEffects(replacementState).Single(e=>e.Reason=="Monument").Vp==6,"bot replacement retains the actual six-VP end-game effect");
  var replay=Play(2,9,144,out _); var replayAgain=Play(2,9,144,out _);
  Check(replay==replayAgain,"identical seeds reproduce full bot match decisions and scores");
  Play(3,9,221,out _); Play(4,9,332,out _);
  var stress=Play(4,18,443,out var duration);
  Check(!string.IsNullOrEmpty(stress) && duration<60,$"four bots finish an 18×18 eight-round stress match in {duration:0.00}s");
  return passed.AsReadOnly();
 }
}

