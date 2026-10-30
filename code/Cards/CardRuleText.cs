using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            _ => null
        };
    }


    public sealed record Word(string Text, Tag Tag = Tag.None, bool Shop = false);
    static readonly Regex TagReference = new(@"\b(?:(?<tag>Home|Trade|Nature|Industry|Culture|Civic)(?:-tagged)?(?=\s+buildings?\b)|(?<shop>shops?)\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Tag references and specific Shop references become inline chips.</summary>
    public static IReadOnlyList<Word> Words(string text)
    {
        var words = new List<Word>();
        if (string.IsNullOrWhiteSpace(text)) return words;
        var cursor = 0;
        void AddText(string value) { foreach (var word in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)) words.Add(new(word)); }
        foreach (System.Text.RegularExpressions.Match match in TagReference.Matches(text))
        {
            AddText(text[cursor..match.Index]);
            if (match.Groups["shop"].Success) words.Add(new(match.Value, Shop: true));
            else words.Add(new(match.Groups["tag"].Value,Enum.Parse<Tag>(match.Groups["tag"].Value,true)));
            cursor = match.Index + match.Length;
        }
        AddText(text[cursor..]);
        return words;
    }

    static readonly Regex Timing = new(@"(?<!\w)(Payout|End|Place|Play|Round start):\s*", RegexOptions.CultureInvariant);

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

