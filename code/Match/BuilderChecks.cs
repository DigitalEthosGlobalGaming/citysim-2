using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Cards;

namespace CitySim.Match;

public static class BuilderChecks
{
    static readonly Guid A=new("11111111-1111-1111-1111-111111111111"), B=new("22222222-2222-2222-2222-222222222222");
    sealed class CountingRandom : Random { public int Calls; public override int Next(int maxValue) { Calls++; return 0; } }
    public static IReadOnlyList<string> Run()
    {
        var passed=new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        var settings=new MatchSettings { EnableAuctions=false, EnableContracts=false, DefaultStreets=0, DefaultCommons=0, StubBidSeconds=1, BuildSeconds=30, PayoutSeconds=1 };
        var s=new MatchState(12,12,123,settings); s.AddPlayer(A,"A"); s.AddPlayer(B,"B");
        for(var x=0;x<12;x++) s.SeedStreet(x,6);
        var rounds=new RoundMachine(s); rounds.Start(0); rounds.Tick(1);
        void Place(MatchState state,Guid owner,string id,int x) { if (!state.GiveCard(owner,id) || !state.TryPlace(owner,id,x,5).Success) throw new InvalidOperationException("Setup "+id); }
        Check(!s.ChooseBuilderDefault(A,true),"Builder preference requires an owned Yard");
        Place(s,A,"warehouse",0); Place(s,A,"builders-yard",1); Place(s,B,"builders-yard",8);
        Check(!s.BuilderCommon(A) && !s.BuilderCommon(B),"Builders' Yard defaults to Street for each player");
        Check(s.ChooseBuilderDefault(A,true) && s.BuilderCommon(A) && !s.BuilderCommon(B),"Builder choices are owner-specific");
        var matchId=Guid.NewGuid();
        var projectedA=MatchProjection.For(s,rounds,A,matchId,1,1);
        var projectedB=MatchProjection.For(s,rounds,B,matchId,1,1);
        var spectator=MatchProjection.For(s,rounds,Guid.Empty,matchId,1,1);
        Check(projectedA.Own.BuilderCommon && !projectedB.Own.BuilderCommon && spectator.Own==null &&
            JsonSerializer.Serialize(projectedA.Public)==JsonSerializer.Serialize(projectedB.Public) &&
            !JsonSerializer.Serialize(projectedA.Public).Contains("BuilderCommon"),"Builder preference is visible only in the owner's projection");
        var client=new MatchReplica();
        Check(client.Apply(A,projectedA) && client.State.BuilderCommon(A) && !client.State.BuilderCommon(B),"replica loads only its owner's Builder preference");
        var authority=new MatchAuthority(matchId,s,rounds);
        MatchCommand Request(long sequence,int amount) => new(matchId,sequence,s.Round,s.Phase,MatchCommandKind.BuilderDefault,Amount:amount);
        Check(!authority.Execute(Guid.Empty,Request(1,0),1).Accepted && !authority.Execute(A,Request(1,2),1).Accepted && s.BuilderCommon(A),"Builder command rejects unseated callers and invalid values");
        Check(authority.Execute(A,Request(2,0),1).Accepted && !s.BuilderCommon(A) && !authority.Execute(A,Request(2,1),1).Accepted && !s.BuilderCommon(A),"Builder command accepts owner choice and rejects replay");
        Check(authority.Execute(B,Request(1,1),1).Accepted && !s.BuilderCommon(A) && s.BuilderCommon(B),"authenticated Builder command only changes its caller's choice");
        Check(authority.Execute(A,Request(3,1),1).Accepted && authority.Execute(B,Request(2,0),1).Accepted,"owner choices can be changed independently");
        var wh=s.Get(0,5); s.GiveCard(A,"street"); var before=s.Players[A].Coins;
        for(var i=0;i<3;i++) PlacementScoring.Preview(s,"street",2,5,A);
        Check(s.TriggerUses(wh.Order,1)==0,"Warehouse previews do not consume its cap");
        Check(!s.TryPlace(A,"street",0,5).Success && s.TriggerUses(wh.Order,1)==0,"rejected Street does not consume Warehouse cap");
        Check(s.TryPlace(A,"street",2,5).Success && s.Players[A].Coins==before+1,"Warehouse pays for first Street placement");
        Place(s,A,"street",3); Check(s.Players[A].Coins==before+1,"Warehouse pays at most once per round");
        var stateCopy=JsonSerializer.Deserialize<MatchCheckpoint>(JsonSerializer.Serialize(s.Capture()));
        bool Bad(BuilderChoice[] choices) { try { MatchState.Restore(stateCopy with { BuilderChoices=choices }); return false; } catch (ArgumentException) { return true; } }
        Check(Bad(new BuilderChoice[] { null }) && Bad(new[] { new BuilderChoice(Guid.NewGuid(),true) }) &&
            Bad(new[] { new BuilderChoice(A,true),new BuilderChoice(A,false) }),"Builder checkpoint rejects null, unknown and duplicate preferences");
        var withoutYard=MatchState.Restore(stateCopy with { Cells=stateCopy.Cells.Where(c => !(c.Owner==A && c.CardId=="builders-yard")).ToArray() });
        Check(withoutYard.BuilderCommon(A),"Builder checkpoint retains preference after its Yard is removed");
        var roundCopy=JsonSerializer.Deserialize<RoundCheckpoint>(JsonSerializer.Serialize(rounds.Capture()));
        var restored=MatchState.Restore(stateCopy); var replay=RoundMachine.Restore(restored,roundCopy);
        Check(restored.BuilderCommon(A) && !restored.BuilderCommon(B),"Builder preference survives checkpoint restore");
        rounds.Ready(A,2); rounds.Ready(B,2); replay.Ready(A,2); replay.Ready(B,2); rounds.Tick(3); replay.Tick(3);
        Check(s.Round==2 && restored.Round==2 && s.Players[A].Hand.SequenceEqual(restored.Players[A].Hand) && s.Players[B].Hand.SequenceEqual(restored.Players[B].Hand),"Builder extra draws replay identically after RNG and choice restore");
        Check(s.Players[A].Hand.Count==2 && s.Players[A].Hand[0]=="street" && CardLibrary.Get(s.Players[A].Hand[1]).Rarity==Rarity.Common && s.Players[B].Hand.SequenceEqual(new[]{"street"}),"Warehouse and owner-selected Yard grant defaults separately");
        rounds.Tick(4); Check(s.ChooseBuilderDefault(A,false) && !s.BuilderCommon(A),"Yard choice can change for the following round");
        var nextCoins=s.Players[A].Coins; Check(s.TryPlace(A,"street",4,5).Success && s.Players[A].Coins==nextCoins+1,"Warehouse cap resets next round");
        while(s.Players[A].Hand.Count<settings.HandLimit) s.GiveCard(A,"street");
        var random=new CountingRandom(); s.ChooseBuilderDefault(A,true); var full=s.Players[A].Hand.Count;
        s.BuilderDefaults(random);
        Check(s.Players[A].Hand.Count==full && random.Calls==0,"full hand skips Builder cards and consumes no Common RNG");
        return passed.AsReadOnly();
    }
}
