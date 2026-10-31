using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Cards;

/// <summary>GDD v0.12 card catalog. Forest is Sapling's grown state, not a draw.</summary>
public static class CardLibrary
{
	static CardEffect E( EffectHook hook, string rule, int amount = 0, int range = 0, int cap = 0, int divisor = 1 ) => new( hook, rule, amount, range, cap, divisor );
	static CardDef C( string id, string name, CardType type, Tag tags, Rarity rarity, bool road,
		string icon, string rules, int weight, CardEffect[] effects, bool tray = false, int limit = 0, int park = 0 ) =>
		new( id, name, type, tags, rarity, road, icon, rules, weight, Array.AsReadOnly( effects ), tray, limit, park );

	static IReadOnlyList<CardDef> Create() => Array.AsReadOnly( new[]
	{
		C("street", "Street", CardType.Road, Tag.None, Rarity.Common, false, "🛣️", "Gain 1 VP when a building is placed beside your Streets. Once per building per Street owner.", 14, [E(EffectHook.OwnerPlacedCard,"street-building-vp",1)]),
		C("house", "House", CardType.Residential, Tag.Home, Rarity.Common, false, "🏠", "Place: +1 VP per adjacent Home building, plus 1 VP if beside a road. Payout: +1 coin if a Shop is within 2 tiles.", 10,
			[E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.Payout,"shop-condition",1,2), E(EffectHook.Place,"adjacent-home-tag-vp",1)]),
		C("cottage", "Cottage", CardType.Residential, Tag.Home, Rarity.Common, false, "🏡", "Place: +1 VP per adjacent park tile or Lake. Payout: +1 coin if adjacent to a park tile or Lake.", 4,
			[E(EffectHook.Payout,"beside-park-coins",1), E(EffectHook.Place,"adjacent-park-vp",1)]),
		C("shop", "Shop", CardType.Commerce, Tag.Trade, Rarity.Common, true, "🏪", "Payout: +1 coin per Home building within 2 tiles, max 3.", 6,
			[E(EffectHook.Payout,"nearby-any-homes",1,2,3)]),
		C("farm", "Farm", CardType.Industry, Tag.Nature, Rarity.Common, false, "🌾", "Payout: +1 coin if adjacent to another Farm or Orchard.", 6,
			[E(EffectHook.Payout,"adjacent-farm-condition",1)]),
		C("park", "Park", CardType.Green, Tag.Nature, Rarity.Common, false, "🌳", "Place: +1 VP, plus 1 VP if beside a Nature card.", 8, [E(EffectHook.Place,"park-score",1), E(EffectHook.Place,"beside-nature-vp",1)], park:1),
		C("wild-garden", "Wild Garden", CardType.Green, Tag.Nature, Rarity.Common, false, "🌿", "Place: +1 VP, plus 1 VP if beside a Nature card.", 3, [E(EffectHook.Place,"park-score",1), E(EffectHook.Place,"beside-nature-vp",1)], park:1),
		C("grant", "Grant", CardType.Event, Tag.None, Rarity.Common, false, "🪙", "Play: gain 4 coins, then discard this card.", 2, [E(EffectHook.Place,"coins",4)]),
		C("avenue", "Avenue", CardType.Road, Tag.None, Rarity.Uncommon, false, "🛣️", "Counts as 2 road tiles for Longest Road. Payout: +1 coin if adjacent to at least 2 buildings.", 3, [E(EffectHook.Payout,"avenue-buildings",1,0,2)]),
		C("roundabout", "Roundabout", CardType.Road, Tag.None, Rarity.Uncommon, false, "🔄", "Gain 1 VP whenever anyone places an adjacent building.", 2, [E(EffectHook.OwnerPlacedCard,"roundabout-vp",1)]),
		C("orchard", "Orchard", CardType.Industry, Tag.Nature, Rarity.Uncommon, false, "🍎", "Place: +1 VP, plus 1 VP if beside a Nature card. Payout: +1 coin if adjacent to a Farm or another Orchard.", 2, [E(EffectHook.Payout,"adjacent-farm-condition",1), E(EffectHook.Place,"park-score",1), E(EffectHook.Place,"beside-nature-vp",1)], park:1),
		C("lake", "Lake", CardType.Green, Tag.Nature, Rarity.Uncommon, false, "🌊", "Place: +1 VP, plus 1 VP if beside a Nature card. Payout: each adjacent Home building earns its owner 1 coin. Counts as a park tile.", 2, [E(EffectHook.Place,"park-score",1), E(EffectHook.Place,"beside-nature-vp",1), E(EffectHook.Payout,"lake-houses",1,1)], park:1),
		C("vineyard", "Vineyard", CardType.Growth, Tag.Nature | Tag.Trade, Rarity.Uncommon, false, "🍇", "Payout: 0 coins for the first 2 payouts, then +3 coins each payout.", 2, [E(EffectHook.Payout,"delayed-coins",3,0,2)]),
		C("startup", "Startup", CardType.Growth, Tag.Trade, Rarity.Rare, false, "💡", "Payout: +1 coin, then +2 coins, then +3 coins each payout.", 2, [E(EffectHook.Payout,"ramping-coins",1,0,3)]),
		C("market", "Market", CardType.Commerce, Tag.Trade, Rarity.Rare, true, "🏬", "Payout: +1 coin per Shop you own within 2 tiles, max 4.", 3, [E(EffectHook.Payout,"nearby-shops",1,2,4)], true),
		C("contractor", "Contractor", CardType.Civic, Tag.Industry | Tag.Civic, Rarity.Rare, false, "👷", "Gain 1 coin when you build a House or Shop, max 2 per round.", 2, [E(EffectHook.OwnerPlacedCard,"contractor-coins",1,0,2)], true,1),
		C("apartment", "Apartment", CardType.Residential, Tag.Home, Rarity.Uncommon, true, "🏢", "Place: +2 VP per adjacent Home building. Payout: +2 coins if a Shop is within 2 tiles.", 3,
			[E(EffectHook.Payout,"shop-condition",2,2), E(EffectHook.Place,"adjacent-home-tag-vp",2)]),
		C("cafe", "Cafe", CardType.Commerce, Tag.Trade | Tag.Culture, Rarity.Uncommon, false, "☕", "Place: +1 VP if beside a road. Payout: +1 coin per adjacent Home building or Shop, max 3. End: +2 VP if adjacent to a park tile or Lake.", 4,
			[E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.Payout,"cafe-neighbours",1,1,3), E(EffectHook.EndGame,"beside-park-vp",2)]),
		C("fountain", "Fountain", CardType.Green, Tag.Nature | Tag.Culture, Rarity.Uncommon, false, "⛲", "Place: +2 VP, plus 1 VP if beside a Nature card.", 3, [E(EffectHook.Place,"park-score",2), E(EffectHook.Place,"beside-nature-vp",1)], park:1),
		C("factory", "Factory", CardType.Industry, Tag.Industry, Rarity.Uncommon, true, "🏭", "Payout: +2 coins. End: each adjacent Home building or Cafe loses its owner 1 VP, including yours.", 3,
			[E(EffectHook.Payout,"coins",2), E(EffectHook.EndGame,"pollution",1,1)]),
		C("sapling", "Sapling", CardType.Growth, Tag.Nature, Rarity.Uncommon, false, "🌱", "Place: +1 VP, plus 1 VP if beside a Nature card. Growth: after its second payout, grows into a Forest and earns 2 VP. Counts as one park tile.", 3,
			[E(EffectHook.Growth,"forest",2,0,2), E(EffectHook.Place,"park-score",1), E(EffectHook.Place,"beside-nature-vp",1)], park:1),
		C("bank", "Bank", CardType.Civic, Tag.Trade, Rarity.Rare, false, "🏦", "Payout: +2 coins.", 4, [E(EffectHook.Payout,"coins",2)], true,3),
		C("town-hall", "Town Hall", CardType.Civic, Tag.Civic, Rarity.Rare, true, "🏛️", "Payout: +2 coins if you own at least 3 Civic buildings, including this.", 2,
			[E(EffectHook.Payout,"civic-threshold",2,0,3)], true,1),
		C("museum", "Museum", CardType.Civic, Tag.Culture | Tag.Civic, Rarity.Rare, false, "🖼️", "Place: +1 VP if beside a road. End: +1 VP per other Culture building you own, max 8.", 2,
			[E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.EndGame,"other-culture-vp",1,0,8)], true,1),
		C("chamber-of-commerce", "Chamber of Commerce", CardType.Civic, Tag.Trade | Tag.Civic, Rarity.Rare, true, "🤝", "Payout: +1 coin per 2 Trade buildings you own, including this, max 4 coins.", 2,
			[E(EffectHook.Payout,"trade-pairs",1,0,4,2)], true,1),
		C("warehouse", "Warehouse", CardType.Civic, Tag.Industry, Rarity.Uncommon, true, "📦", "Round start: gain a Street if your hand has room. Gain 1 coin for your first Street each round.", 2, [E(EffectHook.RoundStart,"builder-street",1), E(EffectHook.OwnerPlacedCard,"warehouse-street-coins",1,0,1)], true,1),
		C("builders-yard", "Builders' Yard", CardType.Civic, Tag.Industry, Rarity.Rare, true, "🏗️", "Round start: gain a Street or random Common card if your hand has room. Choose before the round starts.", 2, [E(EffectHook.RoundStart,"builder-choice",1)], true,1),
		C("hospital", "Hospital", CardType.Civic, Tag.Civic, Rarity.Rare, false, "🏥", "Place: +1 VP if beside a road. Each round: one random Home building has an emergency. The nearest Hospital within 4 tiles earns its owner 1 VP. Ties go to the Hospital built first.", 2, [E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.OwnerPlacedCard,"hospital-emergency",1,4)], true,1),
		C("tourist-office", "Tourist Office", CardType.Civic, Tag.Culture | Tag.Civic, Rarity.Rare, false, "🗺️", "Place: +1 VP if beside a road. Payout: +1 coin per Culture building you own, including this, max 3.", 2, [E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.Payout,"culture-count",1,0,3)], true,1),
		C("tax-office", "Tax Office", CardType.Civic, Tag.Civic, Rarity.Uncommon, false, "🧾", "Payout: +1 coin per 4 Home buildings you own.", 2, [E(EffectHook.Payout,"house-groups",1,0,0,4)], true,1),
		C("depot", "Depot", CardType.Civic, Tag.Industry, Rarity.Uncommon, true, "🚉", "Payout: +1 coin per 5 road tiles you own, max 4 coins.", 2, [E(EffectHook.Payout,"road-groups",1,0,4,5)], true,1),
		C("school", "School", CardType.Civic, Tag.Civic | Tag.Culture, Rarity.Uncommon, false, "🏫", "Place: +1 VP if beside a road. Payout: +1 coin per Home building you own within 2 tiles, max 4.", 2, [E(EffectHook.Place,"beside-road-vp",1), E(EffectHook.Payout,"nearby-homes",1,2,4)], true,1),
		C("granary", "Granary", CardType.Civic, Tag.Civic, Rarity.Uncommon, false, "🌾", "Payout: +1 coin per Farm or Orchard you own, max 3.", 2, [E(EffectHook.Payout,"owned-farms",1,0,3)], true,1),
		C("stadium", "Stadium", CardType.Landmark, Tag.Culture | Tag.Civic, Rarity.Rare, true, "🏟️", "Payout: each adjacent Home building earns its owner 1 coin; each adjacent Cafe loses its owner 1 coin. End: +1 VP per adjacent Home building.", 2, [E(EffectHook.Payout,"stadium-neighbours",1,1), E(EffectHook.EndGame,"stadium-house-vp",1,1)]),
		C("airport", "Airport", CardType.Landmark, Tag.Industry | Tag.Civic, Rarity.Rare, true, "✈️", "Payout: +3 coins. End: each Home building within 2 tiles loses its owner 1 VP. Maximum loss: 3 VP per player across all Airports.", 2, [E(EffectHook.Payout,"coins",3), E(EffectHook.EndGame,"airport-noise",1,2,3)]),
		C("skyscraper", "Skyscraper", CardType.Landmark, Tag.Trade | Tag.Civic, Rarity.Rare, true, "🏙️", "Requires at least 2 adjacent buildings. End: +1 VP per other building within 2 tiles, max 6.", 2, [E(EffectHook.Place,"minimum-buildings",0,0,2), E(EffectHook.EndGame,"nearby-building-vp",1,2,6)]),
		C("industrial-estate", "Industrial Estate", CardType.Civic, Tag.Industry | Tag.Civic, Rarity.Rare, true, "🏭", "Payout: +1 coin per Industry building you own, including this, max 5.", 2,
			[E(EffectHook.Payout,"industry-count",1,0,5)], true,1),
		C("housing-authority", "Housing Authority", CardType.Civic, Tag.Home | Tag.Civic, Rarity.Rare, false, "🏘️", "End: +1 VP per 3 Home buildings you own, including this, max 6.", 2,
			[E(EffectHook.EndGame,"home-thirds-vp",1,0,6,3)], true,1),
		C("eco-council", "Eco Council", CardType.Civic, Tag.Nature | Tag.Civic, Rarity.Rare, false, "🌿", "End: +1 VP per 2 Nature buildings you own, including this, max 8. Subtract 2 VP per Industry building you own. This bonus cannot fall below 0.", 2,
			[E(EffectHook.EndGame,"eco-council-vp",1,2,8,2)], true,1),
		C("gardeners-guild", "Gardener's Guild", CardType.Civic, Tag.Nature | Tag.Civic, Rarity.Uncommon, false, "🧑‍🌾", "Gain 1 coin when you build a park tile.", 2,
			[E(EffectHook.OwnerPlacedCard,"park-coins",1)], true,1),
		C("architects-studio", "Architect's Studio", CardType.Civic, Tag.Culture | Tag.Civic, Rarity.Rare, false, "📐", "Gain 2 coins if your first building this round is adjacent to one of your tiles. Can trigger when you build this.", 2,
			[E(EffectHook.OwnerPlacedCard,"first-building-beside-own",2)], true,1),
		C("renovation", "Renovation", CardType.Event, Tag.None, Rarity.Uncommon, false, "🔨", "Play: replace one of your buildings with a different building card from your hand. Discard both cards.", 2, [E(EffectHook.Place,"renovation")]),
		C("ribbon-cutting", "Ribbon Cutting", CardType.Event, Tag.None, Rarity.Common, false, "🎀", "Play: gain 1 coin per building you built this round, max 4. Discard this card.", 1, [E(EffectHook.Place,"round-building-coins",1,0,4)]),
		C("rush-hour", "Rush Hour", CardType.Event, Tag.None, Rarity.Common, false, "🚦", "Play: gain 1 extra coin per Shop you own at the next payout, max 4. Once per round. Discard this card.", 1, [E(EffectHook.Place,"rush-hour",1,0,4)]),
		C("land-claim", "Land Claim", CardType.Event, Tag.None, Rarity.Uncommon, false, "🚩", "Play: reserve an empty tile until the next round starts. Only you may build there. Discard this card.", 2, [E(EffectHook.Reserve,"land-claim",1)]),
		C("central-station", "Central Station", CardType.Landmark, Tag.Trade | Tag.Civic, Rarity.Legendary, true, "🚉", "Payout: +1 coin per Shop or Market you own, max 4.", 1, [E(EffectHook.Payout,"owned-shops-markets",1,0,4)], true),
		C("senate", "Senate", CardType.Landmark, Tag.Civic, Rarity.Legendary, true, "🏛️", "End: +2 VP for each tag shared by at least 3 of your buildings, max 12. Counts this building.", 1, [E(EffectHook.EndGame,"tag-majorities-vp",2,0,12,3)], true),
		C("monument", "Monument", CardType.Landmark, Tag.Culture, Rarity.Legendary, false, "🗿", "End: +6 VP.", 1, [E(EffectHook.EndGame,"vp",6)], true)
	} );

	// Increment when card data changes: the editor preserves static fields across play restarts/hotloads.
	const int DataVersion = 18;
	static int loadedVersion;
	static IReadOnlyList<CardDef> catalog;
	static IReadOnlyDictionary<string,CardDef> byId;
	static void Ensure()
	{
		if (catalog != null && loadedVersion == DataVersion) return;
		catalog = Create(); byId = catalog.ToDictionary(card => card.Id,StringComparer.Ordinal); loadedVersion = DataVersion;
	}
	public static IReadOnlyList<CardDef> All { get { Ensure(); return catalog; } }
	public static CardDef Get( string id ) { Ensure(); return byId.TryGetValue( id ?? "", out var card ) ? card : throw new ArgumentException( $"Unknown card: {id}", nameof(id) ); }
	public static bool TryGet( string id, out CardDef card ) { Ensure(); return byId.TryGetValue( id ?? "", out card ); }
}

