using System;

namespace CitySim.Match;

/// <summary>Deterministic interior crossing for neutral starting streets.</summary>
public static class StartingRoads
{
    public static (int Row, int Column) Choose(int width, int height, int seed)
    {
        if (width < 3 || height < 3) throw new ArgumentOutOfRangeException(nameof(width), "Starting roads require a board at least three tiles wide and high.");
        var random = new Random(seed);
        return (random.Next(1, height - 1), random.Next(1, width - 1));
    }
}
