using Godot;
using RPG.Utils;
using System.Collections.Generic;

namespace RPG.Dive;

public partial class PhysicsTest : Node3D
{
    [Export]
    public PackedScene DieBodyScene { get; set; }

    [Export]
    public int NumberOfDice { get; set; } = 2;

    private List<DieBody> _dieBodies = [];
    private List<Transform3D> _initialTransforms = [];
    private List<Transform3D> _finalTransforms = [];
    private Rid _spaceRid;
    private Rid _diceBoxRid;

    public override async void _Ready()
    {
        _spaceRid = PhysicsServer3D.SpaceCreate();
        _diceBoxRid = GetNode<StaticBody3D>("%DiceBox").GetRid();
        PhysicsServer3D.BodySetSpace(_diceBoxRid, _spaceRid);
        PhysicsServer3D.SpaceSetActive(_spaceRid, false);

        var random = Vector3.Zero.RandomUpperHemisphere();

        var center = new Vector3(0f, 20f, 0f);
        float radius = 10f; // adjust to taste
        float angleStep = Mathf.Tau / NumberOfDice;

        for (int i = 0; i < NumberOfDice; i++)
        {
            float angle = i * angleStep;

            // Position on circle around center
            var offset = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            var dieBody = DieBodyScene.Instantiate<DieBody>();
            dieBody.Position = center;// + offset;

            // Random upward hemisphere rotation
            dieBody.Rotation = random;

            AddChild(dieBody);
            _dieBodies.Add(dieBody);

            PhysicsServer3D.BodySetSpace(dieBody.GetRid(), _spaceRid);
            _initialTransforms.Add(dieBody.GlobalTransform);
        }

        PhysicsServer3D.SpaceFlushQueries(_spaceRid);
        PhysicsServer3D.SpaceStep(_spaceRid, 1f / 60f); // initial steps to avoid immediate collisions

        var allStill = false;
        while (!allStill)
        {
            PhysicsServer3D.SpaceFlushQueries(_spaceRid);
            PhysicsServer3D.SpaceStep(_spaceRid, 1f / 60f);

            allStill = true;
            foreach (var die in _dieBodies)
            {
                if (die.LinearVelocity.Length() > 1e-7 ||
                    die.AngularVelocity.Length() > 1e-7)
                {
                    allStill = false;
                    break;
                }
            }
        }

        foreach (var die in _dieBodies)
        {
            _finalTransforms.Add(die.GlobalTransform);
        }

        PhysicsServer3D.SpaceFlushQueries(_spaceRid);

        for (int i = 0; i < 1; i++)
        {
            var die = _dieBodies[i];

            int targetFace = 6; // desired outcome

            // // Compute correction based on the *final result*
            // var correction = die.GetCorrectionRotation(_finalTransforms[i], targetFace);

            // // Apply this correction to the *initial* orientation*
            // Transform3D t = _initialTransforms[i];
            // t.Basis = new Basis(correction) * t.Basis;

            // die.GlobalTransform = t;

            die.GlobalTransform = _initialTransforms[i];
        }

        PhysicsServer3D.SpaceSetActive(_spaceRid, true);
    }




}
