using Godot;
using System.Collections.Generic;

namespace RPG.Dive;

public partial class DieBody : RigidBody3D
{
	[Export]
	public required AudioStream[] RollSounds { get; set; }
	[Export]
	public required AudioStream[] CollisionSounds { get; set; }

	private static readonly Dictionary<int, Vector3> DefaultFaceLocalDirections = new()
	{
		{ 6, Vector3.Up },
		{ 1, -Vector3.Up },
		{ 2, Vector3.Right },
		{ 5, -Vector3.Right },
		{ 4, -Vector3.Forward },
		{ 3, Vector3.Forward }
	};
	private MeshInstance3D _mesh = null!;
	private AudioStreamPlayer3D _audioPlayer = null!;
	private bool _enabled = false;

	public override void _Ready()
	{
		_mesh = GetNode<MeshInstance3D>("%Cube");
		_audioPlayer = GetNode<AudioStreamPlayer3D>("%AudioPlayer");
	}

	public void Enable()
	{
		_enabled = true;
	}

	public void OnBodyEntered(Node other)
	{
		if (_enabled && RollSounds.Length > 0 && LinearVelocity.Length() > 0.5f)
		{
			float speed = LinearVelocity.Length();
			if (speed > 0.1f)
			{
				if (other is DieBody)
				{
					int index = GD.RandRange(0, CollisionSounds.Length - 1);
					_audioPlayer.Stream = CollisionSounds[index];
				}
				else
				{
					int index = GD.RandRange(0, RollSounds.Length - 1);
					_audioPlayer.Stream = RollSounds[index];
				}

				float volumeFactor = Mathf.Clamp(speed / 10f, 0f, 1f);
				_audioPlayer.VolumeDb = Mathf.LinearToDb(volumeFactor);

				_audioPlayer.Play();
			}
		}
	}

	public int GetTopFaceFromTransform()
	{
		int bestFace = 1;
		float bestDot = -1f;

		foreach (var kv in DefaultFaceLocalDirections)
		{
			int face = kv.Key;
			Vector3 local = kv.Value;

			// Check which local face vector is most aligned with Global Up
			float dot = (_mesh.GlobalTransform.Basis * local).Dot(Vector3.Up);
			if (dot > bestDot)
			{
				bestDot = dot;
				bestFace = face;
			}
		}

		return bestFace;
	}

	public void ReorientMeshForFace(int targetFace)
	{
		int currentFace = GetTopFaceFromTransform();

		Quaternion fix = GetRotationToMakeFaceUp(currentFace, targetFace);

		Basis rotBasis = new(fix);
		Transform3D local = _mesh.Transform;

		// Apply rotation purely to the basis, keeping origin same
		Transform3D rotTransform = new(rotBasis, Vector3.Zero);

		_mesh.Transform = local * rotTransform;
	}

	private static Quaternion AlignVectorToVector(Vector3 from, Vector3 to)
	{
		from = from.Normalized();
		to = to.Normalized();

		float dot = from.Dot(to);

		if (dot > 0.9999f)
			return Quaternion.Identity;

		// 180 degree turn case
		if (dot < -0.9999f)
		{
			Vector3 axis = from.Cross(Vector3.Right);

			if (axis.LengthSquared() < 0.0001f)
			{
				axis = from.Cross(Vector3.Up);
			}

			return new Quaternion(axis.Normalized(), Mathf.Pi);
		}

		Vector3 cross = from.Cross(to).Normalized();
		float angle = Mathf.Acos(dot);
		return new Quaternion(cross, angle);
	}

	private static Quaternion GetRotationToMakeFaceUp(int currentFace, int targetFace)
	{
		if (currentFace == targetFace)
			return Quaternion.Identity;

		Vector3 currentFaceVec = DefaultFaceLocalDirections[currentFace];
		Vector3 targetFaceVec = DefaultFaceLocalDirections[targetFace];
		Quaternion align = AlignVectorToVector(targetFaceVec, currentFaceVec);

		return align.Normalized();
	}
}