using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CitySim.Match;

namespace CitySim.Ai;

public sealed record AiBenchmarkSeat(int Seat,int Actor,bool HumanProxy,BotDifficulty Difficulty,BotStyle Style,int BotSeed,
    int Vp,int Coins,double WinShare,int Purchases,int Spent,int Plays,int[] RoundVp,int[] RoundCoins,int Decisions,int Restarts,
    int StreetVp,int ParkVp,int RoadBonusVp,string[] FinalCards);
public sealed record AiBenchmarkMatch(int Block,int Seed,int Opponents,int Rotation,int Scenario,int BoardSide,
    AiBenchmarkSeat[] Seats,int RejectedActions,int BudgetViolations,double MaximumUpdateMilliseconds,double P95WorkMilliseconds,double P99WorkMilliseconds,int WorkSamples);
public sealed record AiBenchmarkReport(string Policy,int Seed,int BoardSeeds,double StepSeconds,AiBenchmarkMatch[] Matches);

/// <summary>Seat-rotated mixed-difficulty matches against a Normal human proxy. No engine or file I/O.</summary>
public static class AiBenchmark
{
    public const string Policy=BotChoice.PolicyVersion;
    public const double StepSeconds=1.0/30;
    public static AiBenchmarkReport Run(int boardSeeds,int seed,Action<int,int> progress=null)
    {
        if(boardSeeds<1 || boardSeeds>1000) throw new ArgumentOutOfRangeException(nameof(boardSeeds));
        var results=new List<AiBenchmarkMatch>();
        foreach(var block in Enumerable.Range(0,boardSeeds))
        foreach(var opponents in new[]{1,2,3,4})
        {
            var scenarios=opponents switch {
                1=>new[]{new[]{BotDifficulty.Easy},new[]{BotDifficulty.Normal},new[]{BotDifficulty.Hard}},
                2=>new[]{new[]{BotDifficulty.Easy,BotDifficulty.Normal},new[]{BotDifficulty.Normal,BotDifficulty.Hard},new[]{BotDifficulty.Hard,BotDifficulty.Easy}},
                3=>new[]{new[]{BotDifficulty.Easy,BotDifficulty.Normal,BotDifficulty.Hard}},
                _=>new[]{new[]{BotDifficulty.Easy,BotDifficulty.Normal,BotDifficulty.Hard,BotDifficulty.Normal}}
            };
            for(var scenario=0;scenario<scenarios.Length;scenario++)
            for(var rotation=0;rotation<=opponents;rotation++)
            {
                var matchSeed=unchecked(seed+block*104729);
                var settings=new MatchSettings();var players=opponents+1;
                var side=Math.Clamp((int)Math.Ceiling(Math.Sqrt(settings.TilesPerPlayer*players)),settings.MinimumBoardSide,settings.MaximumBoardSide);
                var state=new MatchState(side,side,matchSeed,settings);
                var ids=Enumerable.Range(1,players).Select(i=>new Guid($"00000000-0000-0000-0000-{i:000000000000}")).ToArray();
                foreach(var id in ids) state.AddPlayer(id,"Seat");
                var (row,column)=StartingRoads.Choose(side,side,matchSeed);
                for(var x=0;x<side;x++) state.SeedStreet(x,row);
                for(var y=0;y<side;y++) state.SeedStreet(column,y);
                var rounds=new RoundMachine(state);
                var actors=Enumerable.Range(0,players).Select(i=>(i+rotation)%players).ToArray();
                var levels=actors.Select(a=>a==0?BotDifficulty.Normal:scenarios[scenario][a-1]).ToArray();
                var styles=actors.Select(a=>a==0?BotStyle.Balanced:BotStyles.ForSeat(matchSeed,a-1)).ToArray();
                var botSeeds=actors.Select(a=>unchecked(matchSeed^((a+1)*73856093))).ToArray();
                var proxy=ids[Array.IndexOf(actors,0)];
                var bots=ids.Select((id,i)=>new Bot(id,state,rounds,levels[i],botSeeds[i],style:styles[i],
                    finishBuildingQuickly:actors[i]==0?null:()=>state.Players[proxy].Hand.Count==0 || rounds.IsBuildReady(proxy))).ToArray();
                var purchased=new int[players];var spent=new int[players];var played=new int[players];
                var streetVp=new int[players];var parkVp=new int[players];var roadVp=new int[players];
                void Awards(IEnumerable<RuleAward> awards) {foreach(var award in awards){var i=Array.IndexOf(ids,award.Player);if(i<0)continue;
                    if(award.Reason=="Street · building placed") streetVp[i]+=award.Vp;
                    if(award.Reason is "Park placement" or "Beside Nature" or "Forest growth") parkVp[i]+=award.Vp;
                    if(award.Reason=="Beside road") roadVp[i]+=award.Vp;}}
                state.Events.PlacementResolved+=e=>Awards(e.Awards);
                state.Events.BuildingGrown+=e=>Awards(e.Park.Awards);
                state.Events.CardPurchased+=e=>{var i=Array.IndexOf(ids,e.Player);purchased[i]++;spent[i]+=e.Price;};
                state.Events.CardPlaced+=e=>{var i=Array.IndexOf(ids,e.Owner);if(i>=0) played[i]++;};
                state.Events.EventPlayed+=e=>played[Array.IndexOf(ids,e.Player)]++;
                var vp=ids.Select(_=>new int[settings.Rounds]).ToArray();var coins=ids.Select(_=>new int[settings.Rounds]).ToArray();
                var recorded=0;var rejected=0;var budget=0;state.Events.PlacementRejected+=_=>rejected++;
                var samples=new List<double>();double maximum=0;
                foreach(var bot in bots) bot.ChooseContract();rounds.Start(0);
                for(var step=0;step<30000 && state.Phase!=MatchPhase.EndGame;step++)
                {
                    var now=step*StepSeconds;
                    foreach(var bot in bots)
                    {
                        var start=Stopwatch.GetTimestamp();bot.Update(now);
                        var elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                        maximum=Math.Max(maximum,elapsed);if(elapsed>=0.1) samples.Add(elapsed);
                        SimulationHarness.CaptureCompletedPayout(state,ids,vp,coins,ref recorded);
                    }
                    foreach(var id in ids)
                    {
                        var v=rounds.GetAuctionView(id);
                        if(state.Players[id].Coins<0 || v!=null && !v.Resolving && v.AvailableCoins+v.ReservedCoins!=state.Players[id].Coins) budget++;
                    }
                    rounds.Tick(now);SimulationHarness.CaptureCompletedPayout(state,ids,vp,coins,ref recorded);
                }
                rejected=Math.Max(rejected,bots.Sum(b=>b.RejectedActions));
                if(state.FinalScore==null || recorded!=8 || rejected!=0 || budget!=0) throw new InvalidOperationException($"AI benchmark did not finish legally: block {block}, opponents {opponents}, scenario {scenario}, rotation {rotation}.");
                var seats=ids.Select((id,i)=>{
                    var score=state.FinalScore.Players.Single(p=>p.Player==id);
                    return new AiBenchmarkSeat(i,actors[i],actors[i]==0,levels[i],styles[i],botSeeds[i],score.TotalVp,score.Coins,
                        state.FinalScore.Winners.Contains(id)?1.0/state.FinalScore.Winners.Count:0,purchased[i],spent[i],played[i],vp[i],coins[i],bots[i].DecisionsCompleted,bots[i].SearchRestarts,
                        streetVp[i],parkVp[i],roadVp[i],state.Cells().Where(c=>c.Owner==id).Select(c=>c.CardId).ToArray());
                }).ToArray();
                samples.Sort();double Percentile(double p)=>samples.Count==0?0:samples[Math.Min(samples.Count-1,(int)Math.Ceiling(samples.Count*p)-1)];
                results.Add(new(block,matchSeed,opponents,rotation,scenario,side,seats,rejected,budget,maximum,Percentile(0.95),Percentile(0.99),samples.Count));
                progress?.Invoke(results.Count,boardSeeds*24);
            }
        }
        return new(Policy,seed,boardSeeds,StepSeconds,results.ToArray());
    }
}
