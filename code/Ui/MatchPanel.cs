using CitySim.Match;
using Sandbox.UI;

namespace CitySim;

/// <summary>A Razor panel subscribes only to events relevant to its content.</summary>
public abstract class MatchPanel : Panel
{
	[Parameter] public CityGame City { get; set; }
	protected MatchState BoundMatch { get; private set; }
	CityGame boundCity;
	protected override void OnParametersSet()
	{
		base.OnParametersSet();
		if (City == boundCity) return;
		if (boundCity != null) boundCity.MatchChanged -= Rebind;
		Unsubscribe(); boundCity = City;
		if (boundCity != null) boundCity.MatchChanged += Rebind;
		Rebind();
	}
	void Rebind()
	{
		Unsubscribe(); BoundMatch = City?.Match; Subscribe(); StateHasChanged();
	}
	protected virtual void Subscribe() { }
	protected virtual void Unsubscribe() { }
	protected void Refresh() => StateHasChanged();
	public override void OnDeleted()
	{
		Unsubscribe(); if (boundCity != null) boundCity.MatchChanged -= Rebind;
		base.OnDeleted();
	}
}
