using Godot;
using System;

namespace RPG.Dive;

public partial class PhysicsTest : Node3D
{
    private DiceBody DieBody;
    private DiceBody DieBody2;

    public override async void _Ready()
    {
        DieBody = GetNode<DiceBody>("%DiceBody");
        DieBody2 = GetNode<DiceBody>("%DiceBody2");

        await DieBody.RollWithPredefinedOutcomeAsync(5, impulseStrength: 20f, torqueStrength: 10f);
    }

}
