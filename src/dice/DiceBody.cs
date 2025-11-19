using Godot;
using RPG.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPG.Dive;

public partial class DiceBody : CharacterBody3D
{
	// Mapping of die face number -> local direction that points out of that face
	// Assumption: Godot's local axes: +Y = up, +X = right, +Z = forward
	// Default mapping assumes the die's default orientation has face 6 on +Y (up).
	// Opposite faces should sum to 7 (standard die): 6<->1, 5<->2, 4<->3
	private static readonly Dictionary<int, Vector3> DefaultFaceLocalDirections = new()
	{
		{ 6, Vector3.Up },        // top
		{ 1, -Vector3.Up },       // bottom
		{ 5, Vector3.Right },     // right
		{ 2, -Vector3.Right },    // left
		{ 4, -Vector3.Forward },  // back (choose -Forward so mapping is consistent)
		{ 3, Vector3.Forward }    // front
	};

	// You can replace this with a custom mapping if your die model uses a different orientation.
	public Dictionary<int, Vector3> FaceLocalDirections { get; private set; } = DefaultFaceLocalDirections;
	private bool _calculated = false;
	private List<Transform3D> _recordedTransforms = new();
	private int _currentTransformIndex = 0;
	private bool _triggerSimulate = false;
	private Vector3 _initialVelocity;
	private Vector3 _initialAngularVelocity;


	public override void _PhysicsProcess(double delta)
	{
		if (_triggerSimulate)
		{
			_triggerSimulate = false;
			_calculated = false;
			_recordedTransforms.Clear();
			_currentTransformIndex = 0;
			ThrowDie(_initialVelocity, _initialAngularVelocity);
		}

		if (_calculated && _recordedTransforms.Count > 0)
		{
			GlobalTransform = _recordedTransforms[_currentTransformIndex];
			_currentTransformIndex = (_currentTransformIndex + 1) % _recordedTransforms.Count;
		}
	}

	public int ThrowDie(
		Vector3 initialVelocity,
		Vector3 initialAngularVelocity,
		float dt = 0.016f,
		int maxSteps = 10000)
	{
		Vector3 v = initialVelocity;
		Vector3 w = initialAngularVelocity;
		Vector3 gravity = new Vector3(0, -9.81f, 0);

		const float restitution = 0.35f;
		const float friction = 0.45f;
		const float angularFriction = 0.98f;
		const float drag = 0.995f;

		const float restLinear = 0.02f;
		const float restAngular = 0.02f;
		const int stableFramesRequired = 40;

		int stableFrames = 0;

		for (int i = 0; i < maxSteps; i++)
		{
			// Apply gravity
			v += gravity * dt;

			// Move & collide
			var collision = MoveAndCollide(v * dt);
			_recordedTransforms.Add(GlobalTransform);
			var grounded = false;

			if (collision != null)
			{
				Vector3 n = collision.GetNormal();
				GD.Print(i, collision.GetCollider().GetType());
				grounded = true;

				// Split velocity into normal + tangent components
				Vector3 vn = n * v.Dot(n);
				Vector3 vt = v - vn;

				// Bounce
				Vector3 newVn = -vn * restitution;
				Vector3 newVt = vt * friction;

				v = newVn + newVt;

				// Stronger angular impulse — rolls the die off edges
				w += n.Cross(v) * 1.5f;
			}

			// Apply damping
			v *= drag;
			w *= angularFriction;

			// ---------- Orientation update ----------
			float speed = w.Length();
			if (speed > 1e-6f)
			{
				Vector3 axis = w / speed;
				float angle = speed * dt;

				Quaternion dq = new Quaternion(axis, angle).Normalized();
				Basis newBasis = new Basis(dq) * GlobalTransform.Basis;

				GlobalTransform = new Transform3D(newBasis, GlobalTransform.Origin);
			}

			// ---------- Gravity torque to force roll-off from edges ----------
			if (grounded)
				GD.Print(IsOnFloor());
			if (IsOnFloor())
			{
				Vector3 up = GlobalTransform.Basis.Y;
				float tiltAngle = Mathf.Acos(Mathf.Clamp(up.Dot(Vector3.Up), -1f, 1f));

				if (tiltAngle > 0.001f)
				{
					Vector3 tiltAxis = up.Cross(Vector3.Up);
					if (tiltAxis.LengthSquared() > 1e-6f)
					{
						tiltAxis = tiltAxis.Normalized();
						w += tiltAxis * (tiltAngle * 2.0f);
					}
				}
			}

			// ---------- Rest detection ----------
			bool lowMotion = (v.Length() < restLinear && w.Length() < restAngular);

			if (lowMotion && IsDieFlatPrecise())
			{
				stableFrames++;
				if (stableFrames >= stableFramesRequired)
				{
					_calculated = true;
					return GetTopFace();
				}
			}
			else
			{
				stableFrames = 0;
			}
		}

		// Fallback
		_calculated = true;
		return GetTopFace();
	}

	// ----------------------------------------------
	// FACE-UP CHECK
	// ----------------------------------------------

	private const float FACE_UP_TOLERANCE = 0.174533f; // 10 degrees in radians

	private bool IsDieFlatPrecise()
	{
		foreach (var kv in DefaultFaceLocalDirections)
		{
			Vector3 localNormal = kv.Value;
			Vector3 worldNormal = GlobalTransform.Basis * localNormal;

			float dot = worldNormal.Dot(Vector3.Up);

			// Face is "up" within ~10 degrees
			if (dot > Mathf.Cos(FACE_UP_TOLERANCE))
				return true;
		}
		return false;
	}

	// ----------------------------------------------
	// GET TOP FACE
	// ----------------------------------------------

	private int GetTopFace()
	{
		int bestFace = 1;
		float bestDot = -1f;

		foreach (var kv in DefaultFaceLocalDirections)
		{
			int face = kv.Key;
			Vector3 localNormal = kv.Value;

			Vector3 worldNormal = GlobalTransform.Basis * localNormal;
			float dot = worldNormal.Dot(Vector3.Up);

			if (dot > bestDot)
			{
				bestDot = dot;
				bestFace = face;
			}
		}

		return bestFace;
	}

	public async Task RollWithPredefinedOutcomeAsync(int desiredFace, int substepsPerPhysicsFrame = 1, int maxSimulationFrames = 1000, float impulseStrength = 6f, float torqueStrength = 2f)
	{
		if (!FaceLocalDirections.ContainsKey(desiredFace))
			throw new ArgumentException($"Unknown face {desiredFace} in FaceLocalDirections mapping.");

		var velocity = Vector3.Zero.RandomUpperHemisphere() * 20f;
		var angular = Vector3.Zero.RandomUpperHemisphere() * 20f;
		_initialVelocity = velocity;
		_initialAngularVelocity = angular;
		_triggerSimulate = true;
	}
}
