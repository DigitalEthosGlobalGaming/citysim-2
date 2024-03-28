
using System;
using System.Collections.Generic;

namespace Degg.GridSystem
{

	public class NavPoint
	{
		public float G;
		public float H;
		public float F;
		public bool Opened;
		public bool Closed;
		public GridSpace Space;
		public NavPoint Parent;

		public NavPoint( GridSpace space )
		{
			G = 0;
			F = 0;
			Space = space;
			Opened = false;
		}

		public Vector2 GetPosition()
		{
			return Space.GridPosition;
		}
		public float Distance( NavPoint b )
		{
			return GetPosition().Distance( b.GetPosition() );
		}

		public float GetMovementWeight( NavPoint b )
		{
			return Space.GetMovementWeight( b.Space, this );
		}

		public List<GridSpace> BackTrace( List<GridSpace> list )
		{
			list.Add( Space );
			if ( Parent == null )
			{
				return list;
			}
			return Parent.BackTrace( list );
		}
	}

	class NavpointComparer : IComparer<NavPoint>
	{
		public int Compare( NavPoint a, NavPoint b )
		{
			return (int)(a.F - b.F);
		}
	}

	class NavMap
	{

	}

	class NavMesh
	{
		GridMap Map;
		NavPoint[,] Grid;
		public NavMesh( GridMap map )
		{
			Map = map;
			Reset();
		}

		public void Reset()
		{
			Grid = new NavPoint[Map.XSize, Map.YSize];
		}

		public NavPoint GetPointAt( Vector2 pos )
		{
			return GetPointAt( (int)pos.x, (int)pos.y );
		}

		public NavPoint GetPointAt( int x, int y )
		{
			var point = Grid[x, y];

			if ( point == null )
			{
				var space = Map.GetSpace( x, y );
				point = new NavPoint( space );
				Grid[x, y] = point;
			}

			return point;

		}

		public float Heuristic( float dx, float dy )
		{
			return dx + dy;
		}





		public List<GridSpace> BuildPath( Vector2 startV, Vector2 endV )
		{

			var endX = endV.x;
			var endY = endV.y;
			var comparer = (IComparer<NavPoint>)new NavpointComparer();
			PriorityQueue<NavPoint, NavPoint> openList = new( comparer );
			var startNode = GetPointAt( startV );

			openList.Enqueue( startNode, startNode );



			while ( openList.Count > 0 )
			{
				var node = openList.Dequeue();
				node.Closed = true;

				if ( node.GetPosition().Equals( endV ) )
				{
					var results = node.BackTrace( new List<GridSpace>() );
					results.Reverse();
					return results;
				}

				var neighbours = node.Space.GetNeighbours<GridSpace>();
				foreach ( var neighbourSpace in neighbours )
				{

					if ( neighbourSpace == null )
					{
						continue;
					}
					var neighbour = GetPointAt( neighbourSpace.GridPosition );
					var movementWeight = node.GetMovementWeight( neighbour );
					if ( movementWeight < 0 )
					{
						continue;
					}
					var neighbourPosition = neighbour.GetPosition();
					var x = neighbourPosition.x;
					var y = neighbourPosition.y;

					if ( neighbour.Closed )
					{
						continue;
					}

					var ng = node.Distance( neighbour );

					if ( !neighbour.Opened || ng < neighbour.G )
					{
						neighbour.G = ng;
						neighbour.H = movementWeight * Heuristic( Math.Abs( x - endX ), Math.Abs( y - endY ) );
						neighbour.F = neighbour.G + neighbour.H;
						neighbour.Parent = node;

						if ( !neighbour.Opened )
						{
							openList.Enqueue( neighbour, neighbour );
							neighbour.Opened = true;
						}
					}
				}
			}

			return new List<GridSpace>();

		}



	}
}
