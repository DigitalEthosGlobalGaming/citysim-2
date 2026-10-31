using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Cards;
using CitySim.Match;

namespace CitySim.Ai;

public static class AiRedesignChecks
{
    static readonly Guid A=new("a1111111-0000-0000-0000-000000000001"),B=new("b1111111-0000-0000-0000-000000000002");
    public static IReadOnlyList<string> Run()
    {
        var results=new List<string>();
        void Check(bool ok,string text) {if(!ok) throw new InvalidOperationException(text);results.Add("PASS "+text);}
        var setup=SoloSetup.Create(144);
        Check(Enumerable.Range(0,100).All(seed=>SoloSetup.Create(seed).Opponents.Select(o=>o.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()==4),"random opponent names are unique within each four-slot setup");
        Check(JsonSerializer.Serialize(setup)==JsonSerializer.Serialize(SoloSetup.Create(144)),"name generation is reproducible with its own seed");
        Check(SoloSetup.Resize(setup,0).OpponentCount==1 && SoloSetup.Resize(setup,99).OpponentCount==4,"solo setup enforces one to four AI opponents");
        setup=SoloSetup.Edit(SoloSetup.Resize(setup,4),3,"  Captain\n Casey  ",BotDifficulty.Hard);
        var restored=SoloSetup.Resize(SoloSetup.Resize(setup,1),4);
        Check(restored.Opponents[3].Name=="Captain Casey" && restored.Opponents[3].Difficulty==BotDifficulty.Hard,"removing and restoring a slot preserves its edited name and difficulty");
        var frozen=SoloSetup.StartRoster(setup,1);
        var changed=SoloSetup.Edit(setup,3,"Another",BotDifficulty.Easy);
        Check(frozen[3].Name=="Captain Casey" && frozen[3].Difficulty==BotDifficulty.Hard && changed.Opponents[3].Name=="Another","starting a match freezes an independent roster snapshot");
        var serialized=JsonSerializer.Deserialize<SoloSetupData>(JsonSerializer.Serialize(setup));
        Check(JsonSerializer.Serialize(SoloSetup.Normalize(serialized,99))==JsonSerializer.Serialize(setup),"names and per-opponent difficulties survive setup serialization");
        var rerolled=SoloSetup.Randomize(setup,97);
        Check(rerolled.Opponents.Select(o=>o.Difficulty).SequenceEqual(setup.Opponents.Select(o=>o.Difficulty)) && rerolled.Opponents.Select(o=>o.Name).Distinct().Count()==4,"randomizing names preserves each opponent's difficulty");
        var invalid=new SoloSetupData(8,new[]{new OpponentSetup(0,"",(BotDifficulty)99)});
        Check(SoloSetup.Normalize(invalid,4).OpponentCount==4 && SoloSetup.Normalize(invalid,4).Opponents.All(o=>Enum.IsDefined(o.Difficulty) && o.Name.Length>0),"invalid saved setup falls back to legal names and difficulties");
        Check(SoloSetup.CleanName(new string('a',40)).Length==24 && SoloSetup.CleanName("\t\n")=="","opponent names have a bounded length and no control characters");
        Check(Enumerable.Range(0,4).Select(SoloSetup.PlayerId).Distinct().Count()==4 && Enumerable.Range(0,4).All(i=>SoloSetup.PlayerId(i)!=A),"all four opponent slots have stable distinct gameplay IDs");
        Check(Enumerable.Range(0,4).Select(i=>BotStyles.ForSeat(17,i)).Distinct().Count()==4,"four opponents receive distinct seeded playing styles");
        var settings=new MatchSettings();
        Check(Math.Clamp((int)Math.Ceiling(Math.Sqrt(settings.TilesPerPlayer*5)),settings.MinimumBoardSide,settings.MaximumBoardSide)==17,"five-player solo matches retain the existing space-per-player target on a 17 by 17 board");

        MatchState Board(int round=2)
        {
            var s=new MatchState(9,9,1,new MatchSettings {EnableContracts=false,EnableGoals=false,StartingCoins=100});
            s.AddPlayer(A,"A");s.AddPlayer(B,"B");
            for(var x=0;x<9;x++) s.SeedStreet(x,4);
            s.SetPhase(MatchPhase.Build,round,40);return s;
        }
        void Place(MatchState s,Guid id,string card,int x,int y)
        {if(!s.GiveCard(id,card) || !s.TryPlace(id,card,x,y).Success) throw new InvalidOperationException($"AI fixture cannot place {card}");}
        var street=Board();Place(street,A,"house",4,3);street.GiveCard(A,"street");
        var evaluator=new BotEvaluation(A,BotDifficulty.Hard,BotStyle.Balanced,null);
        var before=evaluator.Score(street);var after=BotEvaluation.PublicClone(street,A);after.TryPlace(A,"street",3,3);
        Check(Payout.Calculate(after).Entries.Where(e=>e.Player==A).Sum(e=>e.Vp)==0 && evaluator.Score(after)-before<4,"AI does not invent recurring Street VP beside existing buildings");
        var homes=Board(8);Place(homes,A,"house",3,3);Place(homes,B,"house",0,3);homes.GiveCard(A,"house");
        var homesAfter=BotEvaluation.PublicClone(homes,A);var homeMove=homesAfter.TryPlace(A,"house",4,3);
        Check(homeMove.Success && evaluator.Score(homesAfter)-evaluator.Score(homes)>=2.8-0.000001 &&
            FinalScoring.Calculate(homesAfter).Players.Single(p=>p.Player==A).Lines.Single(l=>l.Reason=="Full House").Vp==4,
            "AI values winning Full House and placement VP while accounting for lost catch-up income");
        var cash=Board(1);
        Check(Math.Abs(evaluator.CashValue(1010,cash)-evaluator.CashValue(1000,cash)-2)<0.00001,"excess cash has only the real five-coins-per-VP value");
        Check(evaluator.CashValue(6,cash)>1.2,"cash needed for future purchases carries an early spending value");
        var privacy=Board();Place(privacy,A,"house",4,3);privacy.GiveCard(A,"shop");privacy.GiveCard(B,"monument");
        var publicClone=BotEvaluation.PublicClone(privacy,A);
        Check(publicClone.Players[A].Hand.SequenceEqual(privacy.Players[A].Hand) && publicClone.Players[B].Hand.Count==0 && publicClone.Seed==0,"planning clones include only the AI's own hand and omit the hidden match seed");
        var privacyScore=evaluator.Score(privacy);privacy.Discard(B,0);privacy.GiveCard(B,"bank");
        Check(evaluator.Score(privacy)==privacyScore,"rival hidden-hand changes do not affect AI evaluation");
        var search=new BotSearch(BotEvaluation.PublicClone(privacy,A),A,evaluator,BotDifficulty.Hard);
        search.Step();Check(!search.Complete && search.EvaluatedMoves==0,"planning yields during site generation before expensive scoring");
        for(var i=0;i<5000 && !search.Complete;i++) search.Step();
        Check(search.Complete && search.Best!=null && search.Scores.Count<=14,"bounded planning finishes with a finite candidate shortlist");

        BotSearch Finish(BotSearch job)
        {for(var step=0;step<5000 && !job.Complete;step++) job.Step();if(!job.Complete) throw new InvalidOperationException("Planning fixture did not finish.");return job;}
        var combo=new MatchState(7,7,19,new MatchSettings {EnableContracts=false,EnableGoals=false});
        combo.AddPlayer(A,"Planner");combo.AddPlayer(B,"Other");combo.SetPhase(MatchPhase.Build,1,40);
        combo.GiveCard(A,"street");combo.GiveCard(A,"apartment");
        var immediate=Finish(new BotSearch(BotEvaluation.PublicClone(combo,A),A,evaluator,BotDifficulty.Hard,lookahead:false));
        var sequence=Finish(new BotSearch(BotEvaluation.PublicClone(combo,A),A,evaluator,BotDifficulty.Hard));
        Check(sequence.Best.Move.Card=="street" && sequence.Best.Value>immediate.Best.Value+1,
            "Hard plans a Street followed by a held Apartment when the Apartment is initially blocked");

        var stale=Board();stale.GiveCard(A,"house");
        var staleJob=Finish(new BotSearch(BotEvaluation.PublicClone(stale,A),A,evaluator,BotDifficulty.Hard));
        var blocked=staleJob.Best.Move;Place(stale,B,"house",blocked.X,blocked.Y);
        Check(!blocked.Valid(stale,A) && staleJob.Scores.Any(s=>s.Move.Valid(stale,A)),
            "a rival occupying the planned tile invalidates that move while legal alternatives remain");

        string RenamedDecision(string name,string rivalCard)
        {
            var s=new MatchState(7,7,244,new MatchSettings {EnableContracts=false,EnableGoals=false});
            s.AddPlayer(A,name);s.AddPlayer(B,"Rival "+name);s.SeedStreet(3,3);s.SetPhase(MatchPhase.Build,2,40);
            s.GiveCard(A,"house");s.GiveCard(A,"park");s.GiveCard(B,rivalCard);
            var job=Finish(new BotSearch(BotEvaluation.PublicClone(s,A),A,new(A,BotDifficulty.Easy,BotStyle.Housing,null),BotDifficulty.Easy));
            return JsonSerializer.Serialize(job.Choose(new Random(912)).Move);
        }
        Check(RenamedDecision("Alex","bank")==RenamedDecision("A different name","monument"),
            "display names and rival private cards do not change a seeded AI decision");

        var contractBoard=Board();
        var contractEvaluator=new BotEvaluation(A,BotDifficulty.Normal,BotStyle.Housing,Contracts.Get("suburbia"));
        var noContract=new BotEvaluation(A,BotDifficulty.Normal,BotStyle.Housing,null);
        var contractAfter=BotEvaluation.PublicClone(contractBoard,A);Place(contractAfter,A,"house",3,3);
        Check(contractEvaluator.Score(contractAfter)-contractEvaluator.Score(contractBoard)>noContract.Score(contractAfter)-noContract.Score(contractBoard),
            "an AI values progress toward its own Home contract");

        var passBoard=new MatchState(5,5,99,new MatchSettings {EnableContracts=false,EnableGoals=false,DefaultCommons=0,StartingCoins=20});
        passBoard.AddPlayer(A,"Buyer");passBoard.AddPlayer(B,"Other");passBoard.SeedStreet(2,2);
        var passRounds=new RoundMachine(passBoard);passRounds.Start(0);
        var checkpoint=passRounds.Capture();
        passRounds=RoundMachine.Restore(passBoard,checkpoint with {Auction=checkpoint.Auction with {Lots=new[]{new AuctionLot(0,"grant",11)}}});
        var passer=new Bot(A,passBoard,passRounds,BotDifficulty.Hard,72);
        for(var step=0;step<600 && !passRounds.GetAuctionView(A).Ready;step++) passer.Update(step/60.0);
        Check(passRounds.GetAuctionView(A).Ready && passBoard.Players[A].Coins==20 && passBoard.Players[A].Hand.Count==0,
            "AI passes instead of spending eleven coins on a smaller cash grant");

        foreach(var card in CardLibrary.All)
        {
            var s=new MatchState(15,15,91,new MatchSettings {EnableContracts=false,EnableGoals=false,StartingCoins=100});s.AddPlayer(A,"A");s.AddPlayer(B,"B");
            for(var x=0;x<15;x++) s.SeedStreet(x,7);
            for(var y=0;y<15;y++) s.SeedStreet(7,y);
            s.SetPhase(MatchPhase.Build,3,40);
            Place(s,A,"house",6,6);Place(s,B,"house",5,6);Place(s,A,"shop",8,6);
            Place(s,A,"farm",9,6);Place(s,A,"farm",10,6);Place(s,A,"park",11,6);
            Place(s,A,"cottage",11,5);Place(s,A,"cottage",6,5);Place(s,A,"cottage",9,5);
            s.GiveCard(A,card.Id);if(card.Id=="renovation") s.GiveCard(A,"monument");
            var job=new BotSearch(BotEvaluation.PublicClone(s,A),A,new(A,BotDifficulty.Hard,BotStyle.Balanced,null),BotDifficulty.Hard,card.Id,lookahead:false);
            for(var step=0;step<5000 && !job.Complete;step++) job.Step();
            Check(job.Complete && job.Best!=null && double.IsFinite(job.Best.Value),$"AI scenario covers {card.Name} with a legal finite-valued move");
        }
        return results;
    }
}
