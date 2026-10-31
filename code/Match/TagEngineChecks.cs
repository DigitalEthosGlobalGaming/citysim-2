using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

/// <summary>Shared, engine-independent tests for tag-based Civic engines.</summary>
public static class TagEngineChecks
{
    static readonly Guid A = new("11111111-1111-1111-1111-111111111111");
    static readonly Guid B = new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed = new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS " + name); }
        MatchState Board()
        {
            var s = new MatchState(25,25,1); s.AddPlayer(A,"A"); s.AddPlayer(B,"B");
            for (var x = 0; x < 25; x++) s.SeedStreet(x,12);
            s.SetPhase(MatchPhase.Build,1,40); return s;
        }
        void Place(MatchState s,Guid owner,string id,int x,int y)
        {
            if (!s.GiveCard(owner,id) || !s.TryPlace(owner,id,x,y).Success) throw new InvalidOperationException($"Cannot place {id} at {x},{y}");
        }
        int End(MatchState s,string name) => FinalScoring.CardEffects(s).Single(l => l.Player == A && l.Reason == name).Vp;
        foreach (var id in new[] { "industrial-estate", "housing-authority", "eco-council" })
        {
            var card = CardLibrary.Get(id);
            Check(card.Rarity == Rarity.Rare && card.Type == CardType.Civic && card.Tags.HasFlag(Tag.Civic) && card.NeedsRoad==(id=="industrial-estate") && card.EffectTray && card.OwnershipLimit == 1 && card.Weight == 2,$"{card.Name} metadata");
        }
        var estate = Board(); Place(estate,A,"industrial-estate",0,11);
        Check(Payout.Calculate(estate).Entries.Single(e => e.Source?.CardId == "industrial-estate").Coins == 1,"Industrial Estate counts itself");
        Place(estate,B,"factory",1,11);
        Check(Payout.Calculate(estate).Entries.Single(e => e.Source?.CardId == "industrial-estate").Coins == 1,"Industrial Estate excludes rival Industry");
        for (var x = 2; x < 8; x++) Place(estate,A,"factory",x,11);
        Check(Payout.Calculate(estate).Entries.Single(e => e.Source?.CardId == "industrial-estate").Coins == 5,"Industrial Estate income caps at five");
        estate.GiveCard(A,"industrial-estate");
        Check(!estate.TryPlace(A,"industrial-estate",8,11).Success,"Industrial Estate ownership limit is enforced");

        var adjacency = Board();
        Place(adjacency,A,"housing-authority",0,11);
        Place(adjacency,B,"house",2,11);
        var beforeApartment = adjacency.Players[A].Vp;
        Check(PlacementScoring.Preview(adjacency,"apartment",1,11,A) == 4,"Apartment preview counts both own and rival adjacent Home-tagged buildings");
        Place(adjacency,A,"apartment",1,11);
        Check(adjacency.Players[A].Vp - beforeApartment == 4,"Apartment placement awards two VP per Home-tagged neighbour");
        var homeTarget = new InteractionTarget("Home",InteractionKind.Home);
        var ownHomeTarget = new InteractionTarget("Own Home",InteractionKind.Home,true);
        Check(InteractionMatcher.Matches(homeTarget,adjacency.Get(0,11),A) && InteractionMatcher.Matches(homeTarget,adjacency.Get(2,11),A) && !InteractionMatcher.Matches(ownHomeTarget,adjacency.Get(2,11),A),"tag highlights respect category and ownership");
        Check(InteractionMatcher.Matches(new("House",InteractionKind.House),adjacency.Get(2,11),A) && !InteractionMatcher.Matches(new("House",InteractionKind.House),adjacency.Get(1,11),A),"specific House target excludes Apartment");

        var housing = Board(); Place(housing,A,"housing-authority",0,11);
        Check(BoardRules.IsHome(housing.Get(0,11)),"Housing Authority participates in Home interactions");
        Place(housing,A,"house",1,11);
        Check(PlacementScoring.Preview(housing,"house",2,11,A) == 2,"House preview includes its Home neighbour and road bonus");
        Check(End(housing,"Housing Authority") == 0,"Housing Authority rounds down below three Homes");
        Place(housing,A,"house",2,11);
        Check(End(housing,"Housing Authority") == 1,"Housing Authority counts itself in each group of three");
        Place(housing,B,"house",3,11);
        Check(End(housing,"Housing Authority") == 1,"Housing Authority excludes rival Homes");
        for (var x = 4; x < 23; x++) Place(housing,A,"house",x,11);
        Check(End(housing,"Housing Authority") == 6,"Housing Authority caps at six VP");

        var eco = Board(); Place(eco,A,"eco-council",0,11);
        Check(End(eco,"Eco Council") == 0,"Eco Council rounds down its own single Nature tag");
        Place(eco,A,"park",0,0);
        Check(End(eco,"Eco Council") == 1,"Eco Council counts its own Nature tag");
        Place(eco,B,"park",1,0);
        Check(End(eco,"Eco Council") == 1,"Eco Council excludes rival Nature");
        for (var x = 2; x < 19; x++) Place(eco,A,"park",x,0);
        Check(End(eco,"Eco Council") == 8,"Eco Council Nature bonus caps at eight");
        Place(eco,A,"factory",1,11);
        Check(End(eco,"Eco Council") == 6,"Eco Council applies Industry penalty after the Nature cap");
        for (var x = 2; x < 6; x++) Place(eco,A,"factory",x,11);
        Check(End(eco,"Eco Council") == 0,"Eco Council clamps the Industry penalty to zero");
        var final = FinalScoring.Calculate(eco).Players.Single(p => p.Player == A).Lines.Single(l => l.Reason == "Eco Council");
        Check(final.Vp == End(eco,"Eco Council"),"final scoring uses the same Eco Council card effect");
        return passed.AsReadOnly();
    }
}
