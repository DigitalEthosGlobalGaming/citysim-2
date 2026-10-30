using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record GrowthResult( BoardCell Before, BoardCell After, ParkScoreResult Park );

public static class Growth
{
	public static bool IsMature(BoardCell cell) => CardLibrary.Get(cell.CardId).Effects.Any(e => e.Hook == EffectHook.Growth && cell.Age >= e.Cap);
	internal static void Advance(MatchState state)
	{
		foreach (var before in state.Cells().Where(c => c.PlacedRound > 0).OrderBy(c => c.Order).ToArray())
		{
			var after = before with { Age = before.Age+1 };
			state.UpdateCell(after);
			if (IsMature(before) || !IsMature(after)) continue;
			var park = ParkScoring.OnTileAdded(state,after,before);
			foreach (var award in park.Awards) state.ApplyAward(award);
			state.Events.Park(park);
			state.Events.Growth(new(before,after,park));
		}
	}
}
