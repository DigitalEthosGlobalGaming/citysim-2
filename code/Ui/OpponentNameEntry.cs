using CitySim.Ai;
using Sandbox.UI;

namespace CitySim;

/// <summary>Keep the caret stable during setup refreshes; report native edit events.</summary>
public sealed class OpponentNameEntry : TextEntry
{
    [Parameter] public CityGame City { get; set; }
    [Parameter] public OpponentSetup Opponent { get; set; }
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        MaxLength=SoloSetup.MaximumNameLength;
        Placeholder="Name";
        AddClass("opponent-name");
        if (!HasFocus && Opponent!=null && Text!=Opponent.Name) Text=Opponent.Name;
        OnTextEdited=value=> { if(Opponent!=null) City.EditOpponent(Opponent.Seat,value,Opponent.Difficulty); };
    }
}
