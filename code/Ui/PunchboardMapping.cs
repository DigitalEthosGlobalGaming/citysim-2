using CitySim.Cards;
using CitySim.Punchboard;

namespace CitySim;

public static class PunchboardMapping
{
    public static PbRarity Rarity(Rarity rarity) => (PbRarity)(int)rarity;
    public static PbTag Tag(Tag tag) => tag switch { Cards.Tag.Trade => PbTag.Trade, Cards.Tag.Nature => PbTag.Nature, Cards.Tag.Industry => PbTag.Industry, Cards.Tag.Culture => PbTag.Culture, Cards.Tag.Civic => PbTag.Civic, _ => PbTag.Home };
    public static Color Accent(Tag tags) => TagChip.Values(tags).FirstOrDefault() switch {
        Cards.Tag.Home => new Color(0.85f, 0.28f, 0.23f), Cards.Tag.Trade => new Color(0.94f, 0.72f, 0.23f),
        Cards.Tag.Nature => new Color(0.29f, 0.65f, 0.39f), Cards.Tag.Industry => new Color(0.54f, 0.52f, 0.47f),
        Cards.Tag.Culture => new Color(0.64f, 0.41f, 0.78f), Cards.Tag.Civic => new Color(0.23f, 0.55f, 0.82f),
        _ => new Color(0.94f, 0.72f, 0.23f) };
}
