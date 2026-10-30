using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

/// <summary>Engine-independent checks, shared by the console command and the headless runner.</summary>
public static class FoundationChecks
{
	public static IReadOnlyList<string> Run()
	{
		var results = new List<string>();
		void Check( bool ok, string name ) { if (!ok) throw new InvalidOperationException(name); results.Add($"PASS {name}"); }
		Check(CardLibrary.All.Select(c => c.Id).Distinct().Count() == CardLibrary.All.Count,"unique catalog card IDs");
		var cafeRules = CardRuleText.Split(CardLibrary.Get("cafe").Rules);
		Check(cafeRules.Count == 2 && cafeRules[0].Label == "Payout" && cafeRules[1].Label == "End" && cafeRules.All(part => !string.IsNullOrEmpty(part.Help)),"card timing labels split into explained payout and end sections");
		var houseRules = CardRuleText.Split(CardLibrary.Get("house").Rules);
		Check(houseRules.Count == 2 && houseRules[0].Label == "Place" && houseRules[1].Label == "Payout" && houseRules[1].Text.Contains("Shop"),"House explains payout and placement separately");
		Check(CardRuleText.Split("A plain rule.").Single().Text == "A plain rule.","plain card text remains intact");
		var apartmentWords = CardRuleText.Words(CardRuleText.Split(CardLibrary.Get("apartment").Rules).First().Text);
		Check(apartmentWords.Any(w => w.Tag == Tag.Home) && apartmentWords.Any(w => w.Text == "building."),"Apartment description renders its Home-tag target as a tag");
		var tagWords = CardRuleText.Words(CardLibrary.Get("eco-council").Rules);
		Check(tagWords.Any(w => w.Tag == Tag.Nature) && tagWords.Any(w => w.Tag == Tag.Industry),"multiple tag references produce distinct inline chips");
		Check(CardRuleText.Words(CardLibrary.Get("house").Rules).Any(w => w.Shop && w.Text == "Shop") && CardRuleText.Words(CardLibrary.Get("apartment").Rules).Any(w => w.Shop && w.Text == "Shop"),"singular and plural Shop references become chips");
		Check(CardRuleText.Words(CardLibrary.Get("contractor").Rules).All(w => w.Tag == Tag.None),"specific House and Shop targets do not become category tags");
		var expected = new[] { "street", "house", "cottage", "shop", "farm", "park", "wild-garden", "grant", "apartment", "cafe", "fountain", "factory", "sapling", "bank", "town-hall", "museum", "chamber-of-commerce", "gardeners-guild", "architects-studio", "land-claim", "monument" };
		Check(expected.All(id => CardLibrary.TryGet(id,out _)),"original GDD section 5.6 playtest cards remain available");
		Check(CardLibrary.All.All(c => c.Weight > 0 && !string.IsNullOrWhiteSpace(c.Rules) && !string.IsNullOrWhiteSpace(c.Icon)),"card metadata is populated");
		Check(CardLibrary.All.Count(c => c.Rarity == Rarity.Common) == 10,"ten Common cards including the two low-weight economy events");
		Check(CardLibrary.Get("bank").OwnershipLimit == 3 && CardLibrary.Get("museum").OwnershipLimit == 1,"Civic ownership limits");
		Check(CardLibrary.Get("fountain").ParkSize == 2 && CardLibrary.Get("sapling").ParkSize == 1,"park size metadata");
		Check(!CardLibrary.TryGet("missing",out _),"unknown cards rejected");
		var match = new MatchState(5,5,123);
		var id = new Guid("11111111-1111-1111-1111-111111111111");
		var player = match.AddPlayer(id,"Test mayor");
		var placed = 0; var rejected = 0; var hands = 0; var phases = 0; var coins = 0;
		match.Events.CardPlaced += _ => placed++;
		match.Events.PlacementRejected += _ => rejected++;
		match.Events.HandChanged += _ => hands++;
		match.Events.PhaseChanged += _ => phases++;
		match.Events.CoinsChanged += _ => coins++;
		Check(player.Coins == 8,"starting coins");
		Check(match.SeedStreet(2,2),"neutral seed road");
		Check(match.GiveCard(id,"house"),"deal a House");
		Check(!match.TryPlace(id,"house",2,1).Success && player.Hand.Count == 1,"no placement outside Build");
		match.SetPhase(MatchPhase.Build,1,40);
		Check(!match.TryPlace(id,"house",0,0).Success && player.Hand.Count == 1,"road requirement preserves hand");
		var placement = match.TryPlace(id,"house",2,1);
		Check(placement.Success && placement.Cell.Owner == id && player.Hand.Count == 0,"place House from hand");
		Check(placement.Cell.Order == 2 && placement.Cell.Age == 0 && placement.Cell.PlacedRound == 1,"placement order and age");
		match.GiveCard(id,"park");
		Check(!match.TryPlace(id,"park",2,1).Success && match.Get(2,1).CardId == "house","occupied tile cannot be overwritten");
		Check(!match.TryPlace(id,"park",-1,0).Success,"out of bounds rejected");
		Check(match.TryPlace(id,"park",0,0).Success,"Park needs no road");
		match.GiveCard(id,"cottage");
		Check(match.TryPlace(id,"cottage",4,4).Success,"Cottage needs no road despite Home tag");
		match.GiveCard(id,"grant");
		Check(!match.TryPlace(id,"grant",4,3).Success,"events do not occupy board cells");
		Check(!match.TryPlace(Guid.NewGuid(),"park",3,3).Success,"unknown player rejected");
		Check(!match.SeedStreet(3,3),"cannot seed roads during Build");
		Check(!match.ChangeCoins(id,-9) && player.Coins == 8 && coins == 0,"overspending cannot mutate coins or emit a change");
		Check(match.ChangeCoins(id,3) && player.Coins == 11 && coins == 1,"coin changes emit one event");
		Check(placed == 4 && rejected == 6 && phases == 1 && hands == 7,"exactly one notification per successful mutation or rejection");
		while (player.Hand.Count < match.Settings.HandLimit) match.GiveCard(id,"street");
		Check(!match.GiveCard(id,"street") && player.Hand.Count == 5,"hand limit enforced");
		Check(match.Cells().Count() == 4 && match.Get(2,2).Owner == Guid.Empty,"board enumeration and neutral ownership");
		var limits = new MatchState(5,5,settings: new MatchSettings { RareBuildCost=0 });
		limits.AddPlayer(id,"Limits");
		limits.SetPhase(MatchPhase.Build,1,40);
		for ( var x = 0; x < 3; x++ ) { limits.GiveCard(id,"bank"); Check(limits.TryPlace(id,"bank",x,0).Success,$"Bank stack {x+1}"); }
		limits.GiveCard(id,"bank");
		Check(!limits.TryPlace(id,"bank",3,0).Success && limits.Players[id].Hand.Count == 1,"fourth Bank rejected without consuming card");
		return results.AsReadOnly();
	}
}
