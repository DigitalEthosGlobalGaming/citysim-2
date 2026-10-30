using System.Collections.Generic;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record RaritySettings( Rarity Rarity, int MinimumBid, int RevealWeight, int UnlockRound );

/// <summary>GDD tunables. A match captures its own settings; presentation never changes these.</summary>
public sealed record MatchSettings
{
	public int Rounds { get; init; } = 8;
	public double MiniAuctionSeconds { get; init; } = 10;
	public double StubBidSeconds { get; init; } = 2;
	public bool EnableAuctions { get; init; } = true;
	public double AuctionResolveSeconds { get; init; } = 1.2;
	public int StubOpponentBid { get; init; } = 2;
	public double StubThinkSeconds { get; init; } = 0.6;
	public double AiFrameBudgetMilliseconds { get; init; } = 4;
	public double BuildSeconds { get; init; } = 40;
	public double HospitalEmergencyFraction { get; init; } = 0.5;
	public double PayoutSeconds { get; init; } = 5;
	public int ExtraLots { get; init; } = 1;
	public int HandTarget { get; init; } = 3;
	public int MiniAuctionCap { get; init; } = 3;
	public int HandLimit { get; init; } = 5;
	public int DefaultStreets { get; init; } = 0;
	public int DefaultCommons { get; init; } = 1;
	public int BaseIncome { get; init; } = 2;
	// Optional research settings. The standard market pays all costs when buying.
	public int RareBuildCost { get; init; } = 0;
	public int LegendaryBuildCost { get; init; } = 0;
	public int BuildCost(CardDef card) => card.Type is CardType.Road or CardType.Event ? 0 : card.Rarity switch { Rarity.Rare => RareBuildCost, Rarity.Legendary => LegendaryBuildCost, _ => 0 };
	public int CatchUpCoins { get; init; } = 1;
	public int StartingCoins { get; init; } = 8;
	public int TilesPerPlayer { get; init; } = 55;
	public int MinimumBoardSide { get; init; } = 11;
	public int MaximumBoardSide { get; init; } = 18;
	public int ParkScoringCap { get; init; } = 10;
	public int ParkPlacerDivisor { get; init; } = 2;
	public int CoinsPerEndVp { get; init; } = 5;
	public int PublicGoalCount { get; init; } = 3;
	public bool EnableGoals { get; init; } = true;
	public bool EnableContracts { get; init; } = true;
	public int GoalVp { get; init; } = 5;
	public int TiedGoalVp { get; init; } = 3;
	public int ContractChoices { get; init; } = 2;
	public int RoadBonusVp { get; init; } = 6;
	public int ParkBonusVp { get; init; } = 6;
	public int HousesBonusVp { get; init; } = 4;
	public IReadOnlyList<RaritySettings> Rarities { get; init; } = System.Array.AsReadOnly( new[]
	{
		new RaritySettings(Rarity.Common,1,55,1), new RaritySettings(Rarity.Uncommon,4,28,1),
		new RaritySettings(Rarity.Rare,7,14,2), new RaritySettings(Rarity.Legendary,11,3,4)
	} );
}
