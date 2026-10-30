using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Match;

namespace CitySim.Ai;

public static class SimulationChecks
{
    public static IReadOnlyList<string> Run()
    {
        var checks=new List<string>();
        void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); checks.Add("PASS "+message); }
        Check(Enumerable.Range(0,100).All(seed => { var (row,column)=StartingRoads.Choose(11,15,seed); return row>0 && row<14 && column>0 && column<10 && (row,column)==StartingRoads.Choose(11,15,seed); }), "starting road crossing is interior and deterministic");
        var a=SimulationHarness.Run(4,7381); var b=SimulationHarness.Run(4,7381);
        Check(JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"simulation seed reproduces match provenance, scores and curves");
        Check(a.Strategies.Length==4 && a.Strategies.All(s=>s.Games==4) && a.Matches.SelectMany(m=>m.Seats).GroupBy(s=>s.Strategy).All(g=>g.Select(s=>s.Seat).Distinct().Count()==4),"each strategy plays every seat in a four-match rotation");
        Check(a.BoardSide==15 && a.Matches.All(m=>m.BoardSide==15 && m.MatchSeed==7381) && a.Settings.TilesPerPlayer==55,"rotation shares match seed and uses the live 15 by 15 board");
        Check(a.Matches.SelectMany(m=>m.Seats).GroupBy(s=>s.Strategy).All(g=>g.Select(s=>s.BotSeed).Distinct().Count()==1),"each strategy retains its bot seed across paired seats");
        Check(a.Matches.All(m=>m.Seats.Length==4 && m.Seats.All(s=>s.RoundVp.Length==8 && s.RoundCoins.Length==8) && Math.Abs(m.Seats.Sum(s=>s.WinShare)-1)<0.00001),"matches have eight round curves and fractional win shares summing to one");
        Check(a.Matches.All(m=>m.InvalidActions==0 && m.BudgetViolations==0) && Math.Abs(a.Strategies.Sum(s=>s.Wins)-4)<0.00001,"simulated matches preserve legal actions, budgets and total wins");
        var cursor=0; var visited=new List<int>();
        for(var frame=0;frame<6;frame++) BotFrameScheduler.Run(3,ref cursor,4,i=>visited.Add(i),()=>5);
        Check(visited.SequenceEqual(new[]{0,1,2,0,1,2}),"AI budget stops after a completed update and resumes fairly next frame");
        var invalidBudget=false; try { _=new MatchState(3,3,1,new MatchSettings { AiFrameBudgetMilliseconds=double.NaN }); } catch(ArgumentException) { invalidBudget=true; }
        var zeroBudget=false; try { _=new MatchState(3,3,1,new MatchSettings { AiFrameBudgetMilliseconds=0 }); } catch(ArgumentException) { zeroBudget=true; }
        Check(invalidBudget && zeroBudget,"AI frame budget rejects nonfinite and nonpositive values");
        var bidderId=Guid.NewGuid(); var otherId=Guid.NewGuid();
        var bidState=new MatchState(5,5,7,new MatchSettings { EnableContracts=false,EnableGoals=false,DefaultStreets=0,DefaultCommons=0,StartingCoins=12 });
        bidState.AddPlayer(bidderId,"Bidder"); bidState.AddPlayer(otherId,"Other"); bidState.SeedStreet(2,2);
        var machine=new RoundMachine(bidState); machine.Start(0);
        var checkpoint=machine.Capture(); machine=RoundMachine.Restore(bidState,checkpoint with { Auction=checkpoint.Auction with { Lots=Enumerable.Range(0,3).Select(i=>new AuctionLot(i,"monument",5)).ToArray() } });
        var bot=new Bot(bidderId,bidState,machine,BotDifficulty.Hard,12); bot.Update(0); bot.Update(0.7); bot.Update(0.71);
        Check(machine.GetAuctionView(bidderId).Lots.All(l=>l.Amount==0) && !machine.GetAuctionView(bidderId).Ready,"incremental bidding evaluates one lot per update without submitting early");
        bot.Update(0.72);
        Check(machine.GetAuctionView(bidderId).Lots.Count(l=>l.Amount>0)==1 && machine.GetAuctionView(bidderId).Ready,"bot purchases after all cards are scored");
        var deadlineState=new MatchState(5,5,7,new MatchSettings { EnableContracts=false,EnableGoals=false,DefaultStreets=0,DefaultCommons=0,StartingCoins=12 });
        deadlineState.AddPlayer(bidderId,"Bidder"); deadlineState.AddPlayer(otherId,"Other"); deadlineState.SeedStreet(2,2);
        var deadlineRounds=new RoundMachine(deadlineState); deadlineRounds.Start(0);
        var saved=deadlineRounds.Capture(); deadlineRounds=RoundMachine.Restore(deadlineState,saved with { Auction=saved.Auction with { Lots=Enumerable.Range(0,3).Select(i=>new AuctionLot(i,"monument",5)).ToArray() } });
        deadlineRounds.BuyMarketCard(otherId,deadlineRounds.GetAuctionView(otherId).Id,0,0); deadlineRounds.HurryAuction(otherId,deadlineRounds.GetAuctionView(otherId).Id,0);
        var deadlineBot=new Bot(bidderId,deadlineState,deadlineRounds,BotDifficulty.Hard,12); deadlineBot.Update(0); deadlineBot.Update(deadlineState.PhaseEndsAt-0.05);
        Check(deadlineRounds.GetAuctionView(bidderId).Lots.Count(l=>l.Amount>0)==1,"near-deadline bidding submits the best evaluated lot before expiration");
        var deadlineSnapshot=JsonSerializer.Serialize(deadlineState.Capture()); deadlineBot.Update(deadlineState.PhaseEndsAt);
        Check(deadlineSnapshot==JsonSerializer.Serialize(deadlineState.Capture()),"incremental bidder never submits at the deadline");
        var cancelState=new MatchState(5,5,7,new MatchSettings { EnableContracts=false,EnableGoals=false,DefaultStreets=0,DefaultCommons=0,StartingCoins=12 });
        cancelState.AddPlayer(bidderId,"Bidder"); cancelState.AddPlayer(otherId,"Other"); cancelState.SeedStreet(2,2);
        var cancelRounds=new RoundMachine(cancelState); cancelRounds.Start(0);
        var cancelSaved=cancelRounds.Capture(); cancelRounds=RoundMachine.Restore(cancelState,cancelSaved with { Auction=cancelSaved.Auction with { Lots=Enumerable.Range(0,3).Select(i=>new AuctionLot(i,"monument",5)).ToArray() } });
        var cancelBot=new Bot(bidderId,cancelState,cancelRounds,BotDifficulty.Hard,12); cancelBot.Update(0); cancelBot.Update(0.7);
        var oldId=cancelRounds.GetAuctionView(bidderId).Id;
        cancelRounds.BuyMarketCard(otherId,oldId,0,0); cancelRounds.HurryAuction(otherId,oldId,0); cancelRounds.Tick(cancelState.PhaseEndsAt); cancelRounds.Tick(cancelState.PhaseEndsAt);
        var next=cancelRounds.GetAuctionView(bidderId); cancelBot.Update(11);
        Check(next!=null && !next.Resolving && next.Id!=oldId && next.Lots.All(l=>l.Amount==0),"auction change discards work that had evaluated only one lot");
        var id=Guid.NewGuid(); var fixture=new MatchState(5,5,9,new MatchSettings()); fixture.AddPlayer(id,"A");
        fixture.SetPhase(MatchPhase.Build,1,10); fixture.GiveCard(id,"sapling"); fixture.TryPlace(id,"sapling",1,1);
        var v=new[]{new int[8]}; var c=new[]{new int[8]}; var recorded=0;
        fixture.SetPhase(MatchPhase.Payout,1,10); Payout.Apply(fixture,Payout.Calculate(fixture));
        SimulationHarness.CaptureCompletedPayout(fixture,new[]{id},v,c,ref recorded);
        fixture.SetPhase(MatchPhase.Payout,2,20); Payout.Apply(fixture,Payout.Calculate(fixture));
        SimulationHarness.CaptureCompletedPayout(fixture,new[]{id},v,c,ref recorded);
        Check(fixture.Get(1,1).Age==2 && v[0][1]==fixture.Players[id].Vp && v[0][1]>v[0][0],"round curve includes Sapling growth VP after payout");
        return checks.AsReadOnly();
    }
}
