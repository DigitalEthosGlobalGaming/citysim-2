using CitySim.Cards;
using CitySim.Match;

namespace CitySim;

/// <summary>Reviewed, non-authoritative summaries of the subjects mentioned by each card's rules.</summary>
public static class CardPresentation
{
    public static string ParkHelp(MatchSettings settings = null)
    {
        settings ??= new();
        return $"Only the builder scores. Round the park's size up per {settings.ParkPlacerDivisor} size, before and after adding this tile; gain the difference. Stop counting size at {settings.ParkScoringCap}. Joined regions count once. Sapling growth scores only its size increase. Adjacent means shared edges; diagonals do not join parks.";
    }
    static InteractionTarget T(string label, InteractionKind kind, bool own = false) => new(label,kind,own);
    public static IReadOnlyList<InteractionTarget> Targets(CardDef card) => card.Id switch
    {
        "house" => [T("🏠 Home",InteractionKind.Home), T("🏪 Shops",InteractionKind.Shop)],
        "apartment" => [T("🏠 Home tags",InteractionKind.Home), T("🏪 Shops",InteractionKind.Shop)],
        "cottage" => [T("🌳 Parks",InteractionKind.Park), T("🌊 Lakes",InteractionKind.Lake)],
        "shop" => [T("🏠 Home",InteractionKind.Home)],
        "farm" or "orchard" => [T("🌾 Farms / Orchards",InteractionKind.Farm), .. (card.Id == "orchard" ? new[] { T("🌳 Parks",InteractionKind.Park) } : Array.Empty<InteractionTarget>())],
        "park" or "wild-garden" or "fountain" or "sapling" => [T("🌳 Parks",InteractionKind.Park)],
        "street" or "avenue" or "roundabout" or "skyscraper" => [T("🏗 Buildings",InteractionKind.Building)],
        "lake" or "tax-office" or "school" or "airport" => [T("🏠 Home",InteractionKind.Home,card.Id is "tax-office" or "school")],
        "market" => [T("🏪 Shops",InteractionKind.Shop,true)],
        "contractor" => [T("🏠 House",InteractionKind.House), T("🏪 Shop",InteractionKind.Shop)],
        "cafe" => [T("🏠 Home",InteractionKind.Home), T("🏪 Shops",InteractionKind.Shop), T("🌳 Parks",InteractionKind.Park), T("🌊 Lakes",InteractionKind.Lake)],
        "factory" or "stadium" => [T("🏠 Home",InteractionKind.Home), T("☕ Cafes",InteractionKind.Cafe)],
        "town-hall" => [T("🏛 Civic",InteractionKind.Civic,true)],
        "museum" or "tourist-office" => [T("🎭 Culture",InteractionKind.Culture,true)],
        "chamber-of-commerce" => [T("🪙 Trade",InteractionKind.Trade,true)],
        "warehouse" or "builders-yard" or "depot" => [T("🛣 Roads",InteractionKind.Road,card.Id == "depot")],
        "hospital" => [T("🚨 Emergencies",InteractionKind.None)],
        "granary" => [T("🌾 Farms / Orchards",InteractionKind.Farm,true)],
        "industrial-estate" => [T("⚙ Industry",InteractionKind.Industry,true)],
        "housing-authority" => [T("🏠 Home",InteractionKind.Home,true)],
        "eco-council" => [T("🌿 Nature",InteractionKind.Nature,true), T("⚠ ⚙ Industry",InteractionKind.Industry,true)],
        "gardeners-guild" => [T("🌳 Parks",InteractionKind.Park)],
        "architects-studio" or "ribbon-cutting" or "renovation" => [T("🏗 Buildings",InteractionKind.Building)],
        "rush-hour" => [T("🏪 Shops",InteractionKind.Shop,true)],
        "central-station" => [T("🏪 Shops",InteractionKind.Shop,true), T("🏬 Markets",InteractionKind.Market,true)],
        "land-claim" => [T("🚩 Empty tile",InteractionKind.None)],
        "senate" => [T("🏷 Tagged buildings",InteractionKind.Tagged,true)],
        _ => []
    };
    public static string Interactions(CardDef card) => string.Join(" · ",Targets(card).Select(target => target.Label));

    // The Bank limit already appears in the card's requirement line.
    public static string Rules(CardDef card) => card.Id == "bank" ? "Payout: +2 coins." : card.Rules;

    public static string Requirement(CardDef card)
    {
        var road = card.Id == "street" ? "🛣 Place by a road or board edge" : card.NeedsRoad ? "🛣 Requires road" : "";
        var limit = card.OwnershipLimit > 0 ? $"Limit {card.OwnershipLimit}" : "";
        return string.Join(" · ", new[] { road, limit }.Where(s => !string.IsNullOrEmpty(s)));
    }
}
