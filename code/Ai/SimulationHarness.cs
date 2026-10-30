using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Match;

namespace CitySim.Ai;

public sealed record SimulationSeat(int Seat, BotStrategy Strategy, int BotSeed, int FinalVp, int FinalCoins, double WinShare, int[] RoundVp, int[] RoundCoins);
public sealed record SimulationMatch(int Index, int MatchSeed, int BoardSide, int SeedRoadRow, int SeedRoadColumn, SimulationSeat[] Seats, int InvalidActions, int BudgetViolations);
public sealed record SimulationStrategy(BotStrategy Strategy, int Games, double Wins, double WinRate, double AverageFinalVp, double[] AverageRoundVp, double[] AverageRoundCoins);
public sealed record SimulationReport(int Seed, int MatchCount, int BoardSide, MatchSettings Settings, SimulationMatch[] Matches, SimulationStrategy[] Strategies);

/// <summary>Deterministic, transport-free four-strategy matches. No engine or file I/O.</summary>
public static class SimulationHarness
{
    static readonly Guid[] Ids = Enumerable.Range(1,4).Select(i=>new Guid($"00000000-0000-0000-0000-{i:000000000000}")).ToArray();
    // Called only after Update/Tick returns: Payout.Apply has then finished Growth.Advance.
    internal static void CaptureCompletedPayout(MatchState state, Guid[] ids, int[][] vp, int[][] coins, ref int lastRecordedRound)
    {
        if (state.Phase != MatchPhase.Payout || state.LastPaidRound != state.Round || state.Round <= lastRecordedRound) return;
        for(var i=0;i<ids.Length;i++) { vp[i][state.Round-1]=state.Players[ids[i]].Vp; coins[i][state.Round-1]=state.Players[ids[i]].Coins; }
        lastRecordedRound=state.Round;
    }

    public static SimulationReport Run(int matches, int seed, Action<int,int> progress = null)
    {
        if (matches < 1 || matches > 10000) throw new ArgumentOutOfRangeException(nameof(matches));
        var results=new List<SimulationMatch>(matches);
        for(var m=0;m<matches;m++)
        {
            var matchSeed=unchecked(seed + (m / 4) * 104729);
            var settings=new MatchSettings();
            var side=Math.Clamp((int)Math.Ceiling(Math.Sqrt(settings.TilesPerPlayer * 4)),settings.MinimumBoardSide,settings.MaximumBoardSide);
            var state=new MatchState(side,side,matchSeed,settings);
            for(var i=0;i<4;i++) state.AddPlayer(Ids[i],$"Seat {i+1}");
            var (row, column)=StartingRoads.Choose(side,side,matchSeed);
            for(var x=0;x<side;x++) state.SeedStreet(x,row);
            for(var y=0;y<side;y++) state.SeedStreet(column,y);
            var rounds=new RoundMachine(state);
            var strategies=Enumerable.Range(0,4).Select(i=>(BotStrategy)((i+m)%4)).ToArray();
            var seeds=strategies.Select(x=>unchecked(matchSeed ^ (int)((uint)((int)x+1)*0x9e3779b9u))).ToArray();
            var bots=Enumerable.Range(0,4).Select(i=>new Bot(Ids[i],state,rounds,BotDifficulty.Normal,seeds[i],strategies[i])).ToArray();
            var vp=Enumerable.Range(0,4).Select(_=>new int[settings.Rounds]).ToArray();
            var coins=Enumerable.Range(0,4).Select(_=>new int[settings.Rounds]).ToArray();
            var lastRecordedRound=0;
            var invalid=0; var budget=0; state.Events.PlacementRejected+=_=>invalid++;
            foreach(var bot in bots) bot.ChooseContract();
            if(!rounds.ContractsChosen) throw new InvalidOperationException("Simulation bots did not choose contracts.");
            rounds.Start(0);
            for(var step=0;step<2500 && state.Phase!=MatchPhase.EndGame;step++)
            {
                double now=step*0.25;
                foreach(var bot in bots) { bot.Update(now); CaptureCompletedPayout(state,Ids,vp,coins,ref lastRecordedRound); }
                foreach(var id in Ids)
                {
                    var view=rounds.GetAuctionView(id);
                    if(view!=null && !view.Resolving && (view.AvailableCoins<0 || view.ReservedCoins<0 || view.AvailableCoins+view.ReservedCoins!=state.Players[id].Coins)) budget++;
                    if(state.Players[id].Coins<0) budget++;
                }
                rounds.Tick(now);
                CaptureCompletedPayout(state,Ids,vp,coins,ref lastRecordedRound);
            }
            if(state.Phase!=MatchPhase.EndGame || state.FinalScore==null || state.Round!=settings.Rounds || lastRecordedRound!=settings.Rounds) throw new InvalidOperationException($"Match {m} did not finish.");
            var winners=state.FinalScore.Winners;
            if(winners.Count==0) throw new InvalidOperationException("Simulation produced no winner.");
            var seats=Enumerable.Range(0,4).Select(i=>{
                var score=state.FinalScore.Players.Single(p=>p.Player==Ids[i]);
                return new SimulationSeat(i,strategies[i],seeds[i],score.TotalVp,score.Coins,winners.Contains(Ids[i])?1.0/winners.Count:0,vp[i],coins[i]);
            }).ToArray();
            invalid = Math.Max(invalid,bots.Sum(b=>b.RejectedActions));
            results.Add(new(m,matchSeed,side,row,column,seats,invalid,budget));
            if(invalid!=0 || budget!=0) throw new InvalidOperationException($"Match {m}: {invalid} rejected placements, {budget} budget violations.");
            progress?.Invoke(m+1,matches);
        }
        var aggregates=Enum.GetValues<BotStrategy>().Select(strategy=>{
            var seats=results.SelectMany(r=>r.Seats).Where(s=>s.Strategy==strategy).ToArray();
            var wins=seats.Sum(s=>s.WinShare);
            return new SimulationStrategy(strategy,seats.Length,wins,wins/seats.Length,seats.Average(s=>s.FinalVp),
                Enumerable.Range(0,8).Select(r=>seats.Average(s=>s.RoundVp[r])).ToArray(),
                Enumerable.Range(0,8).Select(r=>seats.Average(s=>s.RoundCoins[r])).ToArray());
        }).ToArray();
        var provenanceSettings=new MatchSettings();
        var boardSide=Math.Clamp((int)Math.Ceiling(Math.Sqrt(provenanceSettings.TilesPerPlayer*4)),provenanceSettings.MinimumBoardSide,provenanceSettings.MaximumBoardSide);
        return new(seed,matches,boardSide,provenanceSettings,results.ToArray(),aggregates);
    }
}
