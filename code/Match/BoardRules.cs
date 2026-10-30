using System;
using CitySim.Cards;

namespace CitySim.Match;

public static class BoardRules
{
    // Tile range includes diagonals; adjacency uses shared edges.
    public static int Distance(BoardCell a, BoardCell b) => Math.Max(Math.Abs(a.X-b.X), Math.Abs(a.Y-b.Y));
    public static bool IsHome(BoardCell cell) => CardLibrary.Get(cell.CardId).Tags.HasFlag(Tag.Home);
    public static bool IsFarm(BoardCell cell) => cell.CardId is "farm" or "orchard";
    public static bool IsPark(BoardCell cell) => CardLibrary.Get(cell.CardId).ParkSize > 0;
}
