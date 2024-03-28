using Sandbox;

namespace Degg.GridSystem
{

	[Category( "Degg.GridSystem" )]
	public abstract class GridItem:Component
	{
		// An offset is where this item would sit on the grid.
		// When porting to SBox this will allow me to have an item on a "grid" but then it could be slightly left or right of the centre.
		public Vector3 offset { get; set; }
		public GridSpace Space { get; set; }

		public void Remove()
		{
			Space = null;
			OnRemove();
		}

		public GridMap GetMap()
		{
			return Space?.Map;
		}

		public bool Move( Vector2 newPosition )
		{
			var map = GetMap();
			if ( map == null )
			{
				return false;
			}

			return map.MoveItem( this, newPosition );
		}
		public bool MoveBy( Vector2 changes )
		{
			var currentPosition = GetGridPosition();
			var newPosition = new Vector2( currentPosition.x, currentPosition.y ) + changes;
			return this.Move( newPosition );
		}

		public Vector2 GetGridPosition()
		{
			return Space.GridPosition;
		}

		public virtual void OnRemove()
		{

		}
		public virtual void OnAdded()
		{

		}
		public virtual void OnMove( Vector2 newPosition, Vector2 oldPosition )
		{

		}
	}
}
