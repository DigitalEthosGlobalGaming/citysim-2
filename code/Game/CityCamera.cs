namespace CitySim;

/// <summary>
/// Orbit camera over the grid. Controls:
///   WASD / arrows  pan (hold Shift to go faster)
///   Middle mouse   grab and drag the map
///   Mouse wheel    zoom towards the cursor
///   Q / E          rotate 45 degrees (mouse 4 / 5 also work)
///   R              recentre
/// Every motion is eased towards a target, so the camera never snaps and never lags behind the pivot.
/// </summary>
public sealed class CityCamera : Component
{
	const float MinZoom = 70f;
	const float NearPitch = 38f;
	const float FarPitch = 62f;
	const float ZoomStep = 0.88f;

	const float PanRate = 14f;
	const float YawRate = 10f;
	const float ZoomRate = 12f;

	float maxZoom = 300f;

	Vector3 centre;
	Vector3 bounds;

	Vector3 pivot;
	Vector3 targetPivot;
	float yaw;
	float targetYaw;
	float zoom = 300f;
	float targetZoom = 300f;

	Vector2 lastMouse;
	bool dragging;

	float trauma;

	CameraComponent Camera => Components.Get<CameraComponent>();

	/// <summary>Adds screen shake. 0.1 is a nudge, 0.5 a thump, 1 is a slam. Stacks, then decays.</summary>
	public void AddTrauma( float amount ) => trauma = Math.Clamp( trauma + amount, 0f, 1f );

	public void FocusOn( CityGrid grid )
	{
		centre = grid.WorldPosition.WithZ( CityGrid.SurfaceHeight );
		bounds = grid.Size * 0.5f;
		maxZoom = Math.Max( 300f, Math.Max( grid.Width, grid.Height ) * 90f );

		pivot = targetPivot = centre;
		yaw = targetYaw = 0f;
		zoom = targetZoom = maxZoom * 0.85f;
		trauma = 0f;

		Apply();
	}

	/// <summary>Reads input, eases towards the targets and moves the camera. Call once per frame.</summary>
	public void Step( bool acceptInput = true )
	{
		if ( acceptInput ) ReadInput();

		var dt = Time.Delta;
		pivot = Vector3.Lerp( pivot, targetPivot, Ease( PanRate, dt ) );
		yaw = MathX.Lerp( yaw, targetYaw, Ease( YawRate, dt ) );
		zoom = MathX.Lerp( zoom, targetZoom, Ease( ZoomRate, dt ) );

		trauma = Math.Max( 0f, trauma - dt * 1.4f );
		Apply();
	}

	// Frame-rate independent exponential smoothing: the same feel at any FPS.
	static float Ease( float rate, float dt ) => 1f - MathF.Exp( -rate * dt );

	// ---------------------------------------------------------------- input

	void ReadInput()
	{
		if ( Input.Pressed( "Menu" ) || Input.Pressed( "SlotNext" ) ) targetYaw += 45f;
		if ( Input.Pressed( "Use" ) || Input.Pressed( "SlotPrev" ) ) targetYaw -= 45f;
		if ( Input.Pressed( "Reload" ) ) targetPivot = centre;

		var mouse = Mouse.Position;
		var overUi = Hud.PointerOverUi;

		ReadDrag( mouse, overUi );
		ReadZoom( mouse, overUi );
		ReadKeyboardPan();

		targetPivot = ClampToBounds( targetPivot );
		pivot = ClampToBounds( pivot );
		lastMouse = mouse;
	}

	void ReadKeyboardPan()
	{
		var move = Input.AnalogMove;
		if ( move.LengthSquared < 0.001f )
			return;

		// Pan speed follows zoom, so crossing the map takes about the same time at any altitude.
		var speed = zoom * (Input.Down( "Run" ) ? 2.4f : 1.2f);
		var flat = Rotation.FromYaw( targetYaw );
		var dir = (flat.Forward * move.x + flat.Left * move.y).Normal;

		targetPivot += dir * speed * Time.Delta;
	}

	void ReadDrag( Vector2 mouse, bool overUi )
	{
		if ( !Input.Down( "Pan" ) ) { dragging = false; return; }
		if ( Input.Pressed( "Pan" ) ) dragging = !overUi;
		if ( !dragging ) return;

		// The ground point under the cursor stays under the cursor: grab-and-drag.
		if ( GroundUnder( lastMouse ) is { } from && GroundUnder( mouse ) is { } to )
		{
			targetPivot += from - to;
			pivot = targetPivot; // follow the hand directly, smoothing here would feel like dragging through mud
		}
	}

	void ReadZoom( Vector2 mouse, bool overUi )
	{
		var wheel = Input.MouseWheel.y;
		if ( overUi || MathF.Abs( wheel ) < 0.001f )
			return;

		var next = Math.Clamp( targetZoom * MathF.Pow( ZoomStep, wheel ), MinZoom, maxZoom );

		// Scale the view about the point under the cursor, so zooming goes where you are looking.
		if ( GroundUnder( mouse ) is { } anchor )
			targetPivot = anchor + (targetPivot - anchor) * (next / targetZoom);

		targetZoom = next;
	}

	Vector3 ClampToBounds( Vector3 p ) => new(
		Math.Clamp( p.x, centre.x - bounds.x, centre.x + bounds.x ),
		Math.Clamp( p.y, centre.y - bounds.y, centre.y + bounds.y ),
		centre.z );

	Vector3? GroundUnder( Vector2 pixel )
	{
		var camera = Camera;
		if ( camera == null )
			return null;

		var ray = camera.ScreenPixelToRay( pixel );
		if ( ray.Forward.z > -0.001f )
			return null;

		var t = (centre.z - ray.Position.z) / ray.Forward.z;
		return t > 0f ? ray.Position + ray.Forward * t : null;
	}

	// ---------------------------------------------------------------- pose

	void Apply()
	{
		// Look steeper from far away (readable overview), flatter up close (feels like you're in the city).
		var t = MathX.Clamp( (zoom - MinZoom) / (maxZoom - MinZoom), 0f, 1f );
		var rotation = Rotation.From( MathX.Lerp( NearPitch, FarPitch, t ), yaw, 0f );
		var position = pivot - rotation.Forward * zoom;

		if ( trauma > 0f )
		{
			// Quadratic trauma: small hits barely register, big ones punch. Smooth sines, not per-frame random.
			var shake = trauma * trauma;
			var s = Time.Now * 32f;
			var reach = zoom * 0.012f * shake;

			position += rotation.Right * MathF.Sin( s * 1.7f ) * reach + rotation.Up * MathF.Sin( s * 2.3f ) * reach;
			rotation *= Rotation.FromRoll( MathF.Sin( s * 1.1f ) * 1.5f * shake );
		}

		WorldPosition = position;
		WorldRotation = rotation;
	}
}
