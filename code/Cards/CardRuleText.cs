using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CitySim.Match;

namespace CitySim.Cards;

/// <summary>Presentation-only timing sections; the card's original rules remain authoritative.</summary>
public static class CardRuleText
{
    public sealed record Section(string Label, string Text)
    {
        public string Help => Label switch
        {
            "Payout" => "This effect happens during the payout phase each round.",
            "End" => "This effect is scored at the end of the game.",
            "Place" => "This effect happens when the card is placed.",
            "Play" => "Play this event during Build, then discard it.",
            "Round start" => "This effect happens at the start of each round while you own this building.",
            "Each round" => "This effect happens once each round while you own this building.",
            "Growth" => "This effect happens once, after the stated number of payouts.",
            _ => null
        };
    }


    public sealed record Word(string Text, Tag Tag = Tag.None, bool Shop = false, bool Park = false,
        InteractionKind Kind = InteractionKind.None, string CardId = null);
    static readonly Regex Reference = new(@"\b(?:(?<tag>Home|Trade|Nature|Industry|Culture|Civic)(?:-tagged)?(?=\s+(?:buildings?|cards?|tiles?)\b)|(?<shop>shops?)\b|(?<park>parks?)\b|(?<building>buildings?)\b|(?<road>Longest Road|road tiles?|roads?)\b|(?<card>Streets?|Houses?|Farms?|Orchards?|Lakes?|Cafes?|Markets?|Hospitals?|Airports?)\b|(?<tagged>tags?)\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Inline references use the same board targets as the interaction chips.</summary>
    public static IReadOnlyList<Word> Words(string text)
    {
        var words = new List<Word>();
        if (string.IsNullOrWhiteSpace(text)) return words;
        var cursor = 0;
        var afterTag = false;
        void AddText(string value) { foreach (var word in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)) words.Add(new(word)); }
        foreach (System.Text.RegularExpressions.Match match in Reference.Matches(text))
        {
            var gap = text[cursor..match.Index];
            AddText(gap);
            if (match.Groups["tag"].Success) words.Add(new(match.Value, Enum.Parse<Tag>(match.Value.Replace("-tagged", ""), true)));
            else if (match.Groups["shop"].Success) words.Add(new(match.Value, Shop: true));
            else if (match.Groups["park"].Success) words.Add(new(match.Value, Park: true));
            else if (match.Groups["building"].Success && afterTag && string.IsNullOrWhiteSpace(gap)) AddText(match.Value);
            else if (match.Groups["building"].Success) words.Add(new(match.Value, Kind: InteractionKind.Building));
            else if (match.Groups["road"].Success) words.Add(new(match.Value, Kind: InteractionKind.Road));
            else if (match.Groups["tagged"].Success) words.Add(new(match.Value, Kind: InteractionKind.Tagged));
            else
            {
                var id = match.Value.ToLowerInvariant();
                if (id.EndsWith("s")) id = id[..^1];
                words.Add(new(match.Value, Kind: InteractionKind.Card, CardId: id));
            }
            afterTag = match.Groups["tag"].Success;
            cursor = match.Index + match.Length;
        }
        AddText(text[cursor..]);
        return words;
    }

    static readonly Regex Timing = new(@"(?<!\w)(Payout|End|Place|Play|Round start|Each round|Growth):\s*", RegexOptions.CultureInvariant);

    public static IReadOnlyList<Section> Split(string text)
    {
        var sections = new List<Section>();
        if (string.IsNullOrWhiteSpace(text)) return sections;
        var matches = Timing.Matches(text);
        if (matches.Count == 0) { sections.Add(new(null,text.Trim())); return sections; }
        var prefix = text[..matches[0].Index].Trim();
        if (prefix.Length > 0) sections.Add(new(null,prefix));
        for (var i=0; i<matches.Count; i++)
        {
            var match=matches[i];
            var end=i+1 < matches.Count ? matches[i+1].Index : text.Length;
            sections.Add(new(match.Groups[1].Value,text[(match.Index+match.Length)..end].Trim()));
        }
        return sections;
    }
}

