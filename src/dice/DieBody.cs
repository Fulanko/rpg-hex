using Godot;
using RPG.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPG.Dive;

public partial class DieBody : RigidBody3D
{
	// Mapping of die face number -> local direction that points out of that face
	// Assumption: Godot's local axes: +Y = up, +X = right, +Z = forward
	// Default mapping assumes the die's default orientation has face 6 on +Y (up).
	// Opposite faces should sum to 7 (standard die): 6<->1, 5<->2, 4<->3
	private static readonly Dictionary<int, Vector3> DefaultFaceLocalDirections = new()
	{
		{ 6, Vector3.Up },        // top
		{ 1, -Vector3.Up },       // bottom
		{ 2, Vector3.Right },     // right
		{ 5, -Vector3.Right },    // left
		{ 4, -Vector3.Forward },  // back (choose -Forward so mapping is consistent)
		{ 3, Vector3.Forward }    // front
	};

	public Quaternion GetCorrectionRotation(Transform3D finalTransform, int desiredTopFace)
	{
		// Which face actually ended up on top in the simulation?
		int actualTop = GetTopFaceFromTransform(finalTransform);

		if (actualTop == desiredTopFace)
			return Quaternion.Identity;

		// World-space direction of actual top
		Vector3 actualWorld = finalTransform.Basis * DefaultFaceLocalDirections[actualTop];

		// World-space direction of desired top
		Vector3 desiredWorld = Vector3.Up;

		// Rotation needed to send actual top → desired top
		return RotationFromTo(actualWorld, desiredWorld);
	}

	private int GetTopFaceFromTransform(Transform3D t)
	{
		int bestFace = 1;
		float bestDot = -1f;

		foreach (var kv in DefaultFaceLocalDirections)
		{
			int face = kv.Key;
			Vector3 local = kv.Value;

			float dot = (t.Basis * local).Dot(Vector3.Up);
			if (dot > bestDot)
			{
				bestDot = dot;
				bestFace = face;
			}
		}

		return bestFace;
	}

	private static Quaternion RotationFromTo(Vector3 from, Vector3 to)
	{
		from = from.Normalized();
		to = to.Normalized();

		float dot = from.Dot(to);

		// If vectors are almost identical → no rotation needed
		if (dot > 0.9999f)
			return Quaternion.Identity;

		// If vectors are opposite → choose any perpendicular axis
		if (dot < -0.9999f)
		{
			Vector3 axis = from.Cross(Vector3.Right);
			if (axis.LengthSquared() < 0.0001f)
				axis = from.Cross(Vector3.Up);

			axis = axis.Normalized();
			return new Quaternion(axis, Mathf.Pi); // 180 degrees
		}

		// General case: use cross product
		Vector3 axisCross = from.Cross(to);
		float angle = Mathf.Acos(dot);

		return new Quaternion(axisCross.Normalized(), angle);
	}

}
