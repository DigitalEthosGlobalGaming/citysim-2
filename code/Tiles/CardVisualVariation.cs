using CitySim.Cards;
using CitySim.Match;

namespace CitySim;

/// <summary>Stable, public presentation choices. Never consumes the match's random stream.</summary>
public static class CardVisualVariation
{
	public static int Seed(string id, int x, int y)
	{
		unchecked
		{
			uint hash = 2166136261;
			foreach (var c in id) hash = (hash ^ c) * 16777619;
			hash = (hash ^ (uint)x) * 16777619;
			hash = (hash ^ (uint)y) * 16777619;
			return (int)(hash & 0x7fffffff);
		}
	}

	public static float Yaw(MatchState match, string id, int x, int y, int order = int.MaxValue)
	{
		var seed = Seed(id,x,y);
		// Unstructured planting has no entrance. Orchards and gardens retain a grid layout.
		if (id is "forest" or "sapling") return seed % 360;
		var roads = new List<int>();
		for (var i = 0; i < CityGrid.Directions.Length; i++)
		{
			var d = CityGrid.Directions[i];
			var neighbour = match.Get(x+d.X,y+d.Y);
			// Ignore roads added later, so loading a board preserves the original facing.
			if (neighbour != null && neighbour.Order <= order && CardLibrary.Get(neighbour.CardId).Type == CardType.Road) roads.Add(i);
		}
		return roads.Count > 0 ? CityGrid.YawForDirection(roads[seed % roads.Count]) : seed % 4 * 90;
	}

	public static Color Ground(string id, int x, int y, bool mature = false)
	{
		var color = id switch
		{
			"park" => new Color(0.49f,0.69f,0.36f),
			"forest" => new Color(0.37f,0.56f,0.31f),
			"sapling" => mature ? new Color(0.37f,0.56f,0.31f) : new Color(0.55f,0.71f,0.39f),
			"wild-garden" => new Color(0.59f,0.72f,0.43f),
			"orchard" or "vineyard" or "farm" => new Color(0.64f,0.70f,0.39f),
			"lake" => new Color(0.47f,0.65f,0.49f),
			_ => id != null && CardLibrary.Get(id).Tags.HasFlag(Tag.Nature)
				? new Color(0.55f,0.69f,0.40f) : new Color(0.66f,0.76f,0.46f)
		};
		return (x+y) % 2 == 0 ? color : new Color(color.r+0.03f,color.g+0.03f,color.b+0.03f);
	}
}
